using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

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

    // Graphics/static offsets (live-telemetry-evo docs/SHARED_MEMORY.md,
    // layout v1.x). WHY hardcoded here: the repo keeps every byte offset in
    // this one reader so an EVO update only has one blast radius. The attach
    // log prints sm_version so a future layout change is visible immediately.
    private const int OffGraphicsPacketId = 0;
    private const int OffGraphicsNpos = 1244;          // normalised lap position, 0..1
    private const int OffStaticSmVersion = 0;          // char[15]
    private const int OffStaticTrack = 136;            // char[33]
    private const int OffStaticTrackConfiguration = 169; // char[33]
    private const int OffStaticTrackLengthMeters = 204;  // float
    private const int StaticVersionChars = 15;
    private const int StaticNameChars = 33;
    private const int GraphicsViewBytes = 2048;
    private const int StaticViewBytes = 256;

    private const string PhysicsMap = @"Local\acevo_pmf_physics";
    private const string GraphicsMap = @"Local\acevo_pmf_graphics";
    private const string StaticMap = @"Local\acevo_pmf_static";

    private readonly ILog _log;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private MemoryMappedFile? _graphicsMmf;
    private MemoryMappedViewAccessor? _graphicsView;
    private MemoryMappedFile? _staticMmf;
    private MemoryMappedViewAccessor? _staticView;
    private TrackInfo? _track;
    private bool _wasPresent;

    public ShmReader(ILog log) => _log = log;

    public bool GameRunning { get; private set; }
    public bool PhysicsPresent { get; private set; }
    public bool GraphicsPresent { get; private set; }
    public bool StaticPresent { get; private set; }

    public void Dispose() => DisposeViews();

    private void DisposeViews()
    {
        _graphicsView?.Dispose();
        _graphicsMmf?.Dispose();
        _staticView?.Dispose();
        _staticMmf?.Dispose();
        _view?.Dispose();
        _mmf?.Dispose();
        _graphicsView = null;
        _graphicsMmf = null;
        _staticView = null;
        _staticMmf = null;
        _view = null;
        _mmf = null;
        _track = null;
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

        OpenAuxiliaryViews();
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

    // Graphics/static are optional: the physics block drives speed/gear, and
    // the HUD's address simply degrades to "no track position" when either
    // mapping is missing. WHY only on attach: EVO publishes all three blocks
    // together at session load; a per-tick open retry would waste handles on
    // the 10 Hz hot path.
    private void OpenAuxiliaryViews()
    {
        if (GraphicsPresent
            && TryOpenMapping(GraphicsMap, "graphics", GraphicsViewBytes,
                ref _graphicsMmf, ref _graphicsView))
        {
            _log.LogInfo($"EVO graphics SHM opened (npos offset {OffGraphicsNpos})");
        }

        if (StaticPresent
            && TryOpenMapping(StaticMap, "static", StaticViewBytes,
                ref _staticMmf, ref _staticView))
        {
            _log.LogInfo("EVO static SHM opened; waiting for session metadata");
            ReadStatic();
        }
    }

    private bool TryOpenMapping(
        string name, string label, int bytes,
        ref MemoryMappedFile? mmf, ref MemoryMappedViewAccessor? view)
    {
        if (view is not null)
        {
            return true;
        }

        try
        {
            mmf ??= MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            view ??= mmf.CreateViewAccessor(0, bytes, MemoryMappedFileAccess.Read);
            return true;
        }
        // WHY the same guarded set as Probe: an optional block that vanished
        // or refused read access must not take the physics read down. The HUD
        // loses its address, the overlay keeps running.
        catch (FileNotFoundException ex)
        {
            LogOptionalFailure(label, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogOptionalFailure(label, ex);
        }
        catch (IOException ex)
        {
            LogOptionalFailure(label, ex);
        }
        catch (ArgumentException ex)
        {
            LogOptionalFailure(label, ex);
        }
        catch (InvalidOperationException ex)
        {
            LogOptionalFailure(label, ex);
        }

        view?.Dispose();
        mmf?.Dispose();
        view = null;
        mmf = null;
        return false;
    }

    private void LogOptionalFailure(string label, Exception ex) =>
        _log.LogWarning($"EVO {label} SHM unavailable ({ex.GetType().Name}); HUD address degraded");

    // Static is written once per session, but often a moment AFTER its mapping
    // appears (the game is still loading when the attach probe wins). The
    // cheap sentinel re-checks only until the block is populated, then caches
    // the decoded TrackInfo for good. No per-tick strings while it is empty.
    private void EnsureStatic()
    {
        var view = _staticView;
        if (view is null || _track is not null)
        {
            return;
        }

        byte first = view.ReadByte(OffStaticTrack);
        float length = view.ReadSingle(OffStaticTrackLengthMeters);
        if (first == 0 || !float.IsFinite(length) || length <= 0f)
        {
            return;
        }
        ReadStatic();
    }

    private void ReadStatic()
    {
        var view = _staticView;
        if (view is null)
        {
            return;
        }

        string version = ReadAscii(view, OffStaticSmVersion, StaticVersionChars);
        string track = ReadAscii(view, OffStaticTrack, StaticNameChars);
        string configuration = ReadAscii(view, OffStaticTrackConfiguration, StaticNameChars);
        float length = view.ReadSingle(OffStaticTrackLengthMeters);
        double meters = float.IsFinite(length) && length > 0f ? length : 0.0;
        if (track.Length == 0 || meters <= 0.0)
        {
            // Still zero-filled: leave _track null so EnsureStatic retries.
            return;
        }

        _track = new TrackInfo(track, configuration, meters);
        _log.LogInfo($"EVO static: sm_version={version}, track={track}, " +
            $"config={configuration}, length={meters:0} m");
    }

    private static string ReadAscii(MemoryMappedViewAccessor view, int offset, int count)
    {
        byte[] buffer = new byte[count];
        view.ReadArray(offset, buffer, 0, count);
        int end = Array.IndexOf(buffer, (byte)0);
        if (end < 0)
        {
            end = count;
        }
        return Encoding.ASCII.GetString(buffer, 0, end).Trim();
    }

    // Graphics carries npos at the far end of the block. The packetId guard
    // mirrors the physics read: a mid-write sample is skipped, never shown as
    // a wild house number.
    private static bool TryReadNormalizedPosition(
        MemoryMappedViewAccessor view, out float normalizedPosition)
    {
        int packet = view.ReadInt32(OffGraphicsPacketId);
        normalizedPosition = view.ReadSingle(OffGraphicsNpos);
        if (packet != view.ReadInt32(OffGraphicsPacketId))
        {
            packet = view.ReadInt32(OffGraphicsPacketId);
            normalizedPosition = view.ReadSingle(OffGraphicsNpos);
            if (packet != view.ReadInt32(OffGraphicsPacketId))
            {
                normalizedPosition = float.NaN;
                return false;
            }
        }

        if (!float.IsFinite(normalizedPosition)
            || normalizedPosition < 0f
            || normalizedPosition > 1f)
        {
            normalizedPosition = float.NaN;
            return false;
        }
        return true;
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
            EnsureStatic();
            return ReadFields(view, _graphicsView, _track);
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

    private static TelemetrySample? ReadFields(
        MemoryMappedViewAccessor view,
        MemoryMappedViewAccessor? graphicsView,
        TrackInfo? track)
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

        float? normalizedPosition = null;
        if (graphicsView is not null
            && TryReadNormalizedPosition(graphicsView, out float position))
        {
            normalizedPosition = position;
        }

        return new TelemetrySample(
            packet, gas, brake, gear, rpm, steer, speed, normalizedPosition, track);
    }

    private TelemetrySample? FailRead(Exception ex)
    {
        _log.LogWarning($"EVO SHM read failed ({ex.GetType().Name}); resetting");
        Reset();
        return null;
    }

    private void Reset() => DisposeViews();
}
