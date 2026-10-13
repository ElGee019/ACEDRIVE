using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay;

/// <summary>
/// EVO shared-memory reader (physics block). All byte offsets live here and
/// nowhere else. Read-only; coexists with PitVox/SimHub.
/// Offsets per live-telemetry-evo docs/SHARED_MEMORY.md (_pack_ = 4).
/// Logs attach/loss transitions only — never per tick (hot-path rule).
/// TryRead never throws (it runs on MainViewModel's 10 Hz poll, where an
/// exception would shut the overlay down); every external call is guarded.
/// </summary>
internal sealed class ShmReader : ITelemetrySource
{
    private const int OffPacketId = 0;
    private const int OffGas = 4;
    private const int OffBrake = 8;
    private const int OffGear = 16;   // 0=R, 1=N, 2..N=forward
    private const int OffRpms = 20;
    private const int OffSteer = 24;  // rad, -=left
    private const int OffSpeedKmh = 28;

    private const string PhysicsMap = @"Local\acevo_pmf_physics";
    private const string GraphicsMap = @"Local\acevo_pmf_graphics";
    private const string StaticMap = @"Local\acevo_pmf_static";

    private readonly ILog _log;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private bool _wasPresent;

    public ShmReader(ILog log) => _log = log;

    public bool GameRunning { get; private set; }
    public bool PhysicsPresent { get; private set; }
    public bool GraphicsPresent { get; private set; }
    public bool StaticPresent { get; private set; }

    public void Dispose()
    {
        _view?.Dispose();
        _mmf?.Dispose();
        _view = null;
        _mmf = null;
    }

    private static bool Probe(string name)
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            return true;
        }
        // WHY: OpenExisting reports "game not running" as FileNotFound, but ACL
        // and shutdown races surface as the siblings below. All mean "not
        // readable right now" — never a reason to throw out of the 10 Hz poll.
        // (Specific types only; no bare Exception catch per CA1031.)
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsGameProcessRunning()
    {
        try
        {
            return Process.GetProcessesByName("AssettoCorsaEVO").Length > 0;
        }
        // WHY: enumeration can race a process exit or hit access limits; report
        // "not running" and let the SHM probes decide instead of killing the poll.
        // (Specific types only; no bare Exception catch per CA1031.)
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Null when the game/mapping is unavailable. Never throws.</summary>
    public TelemetrySample? TryRead()
    {
        // Fast path: attached and reading — zero opens, zero process scans.
        if (_view is not null)
        {
            var fast = ReadSample();
            if (fast is not null)
            {
                // WHY: readable physics implies the game runs; rescanning the
                // process list at 10 Hz wastes handles on the hot path.
                GameRunning = true;
                return fast;
            }

            if (_view is not null)
            {
                // WHY: torn read skipped silently inside ReadFields; the cached
                // handle is still good, so sit this tick out without slow-path churn.
                return null;
            }
            // WHY: else ReadSample already warned and Reset; fall through to re-attach.
        }

        // Slow path: not attached (or the handle just died) — rediscover, then attach.
        RefreshPresence();

        if (!PhysicsPresent)
        {
            Reset();
            return null;
        }

        try
        {
            _mmf ??= MemoryMappedFile.OpenExisting(PhysicsMap, MemoryMappedFileRights.Read);
            _view ??= _mmf.CreateViewAccessor(0, 64, MemoryMappedFileAccess.Read);
        }
        // WHY: attach races game shutdown; warn once and retry on the next tick.
        // (Same guarded set as Probe/ReadSample; no bare Exception per CA1031.
        // InvalidOperationException covers a raced ObjectDisposedException.)
        catch (FileNotFoundException ex)
        {
            return FailRead(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailRead(ex);
        }
        catch (IOException ex)
        {
            return FailRead(ex);
        }
        catch (ArgumentException ex)
        {
            return FailRead(ex);
        }
        catch (InvalidOperationException ex)
        {
            return FailRead(ex);
        }

        return ReadSample();
    }

    private void RefreshPresence()
    {
        GameRunning = IsGameProcessRunning();
        PhysicsPresent = Probe(PhysicsMap);
        GraphicsPresent = Probe(GraphicsMap);
        StaticPresent = Probe(StaticMap);

        // NOTE: a static-page sm_version guard slots in here beside the
        // StaticPresent probe once its EVO offset is known (doc not vendored;
        // no offset invented). Treat it as versioned per the telemetry rules.
        if (PhysicsPresent != _wasPresent)
        {
            _wasPresent = PhysicsPresent;
            _log.LogInfo(PhysicsPresent
                ? $"EVO SHM attached (graphics={GraphicsPresent} static={StaticPresent})"
                : "EVO SHM lost");
        }
    }

    private TelemetrySample? ReadSample()
    {
        var view = _view;
        if (view is null)
        {
            return null;
        }

        try
        {
            return ReadFields(view);
        }
        // WHY: same failure modes as Probe/attach (mapping closed mid-read,
        // torn handle). Warn once and drop the cached handle so the slow path
        // re-attaches. (No bare Exception catch per CA1031.)
        catch (FileNotFoundException ex)
        {
            return FailRead(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailRead(ex);
        }
        catch (IOException ex)
        {
            return FailRead(ex);
        }
        catch (ArgumentException ex)
        {
            return FailRead(ex);
        }
        catch (InvalidOperationException ex)
        {
            return FailRead(ex);
        }
    }

    private static TelemetrySample? ReadFields(MemoryMappedViewAccessor view)
    {
        int packet = view.ReadInt32(OffPacketId);
        float gas = view.ReadSingle(OffGas);
        float brake = view.ReadSingle(OffBrake);
        int gear = view.ReadInt32(OffGear);
        int rpm = view.ReadInt32(OffRpms);
        float steer = view.ReadSingle(OffSteer);
        float speed = view.ReadSingle(OffSpeedKmh);

        if (packet != view.ReadInt32(OffPacketId))
        {
            // WHY: the writer updated the block mid-read (torn read); one full
            // re-read usually lands on a stable frame.
            packet = view.ReadInt32(OffPacketId);
            gas = view.ReadSingle(OffGas);
            brake = view.ReadSingle(OffBrake);
            gear = view.ReadInt32(OffGear);
            rpm = view.ReadInt32(OffRpms);
            steer = view.ReadSingle(OffSteer);
            speed = view.ReadSingle(OffSpeedKmh);

            if (packet != view.ReadInt32(OffPacketId))
            {
                // WHY: still torn — skip this tick silently; the next 100 ms
                // poll retries (no per-tick logging per the hot-path rule).
                return null;
            }
        }

        return new TelemetrySample(packet, gas, brake, gear, rpm, steer, speed);
    }

    private TelemetrySample? FailRead(Exception ex)
    {
        _log.LogWarning($"EVO SHM read failed ({ex.GetType().Name}); resetting");
        Reset();
        return null;
    }

    private void Reset()
    {
        _view?.Dispose();
        _mmf?.Dispose();
        _view = null;
        _mmf = null;
    }
}
