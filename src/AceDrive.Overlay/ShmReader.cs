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
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Null when the game/mapping is unavailable.</summary>
    public TelemetrySample? TryRead()
    {
        GameRunning = System.Diagnostics.Process
            .GetProcessesByName("AssettoCorsaEVO").Length > 0;
        PhysicsPresent = Probe(PhysicsMap);
        GraphicsPresent = Probe(GraphicsMap);
        StaticPresent = Probe(StaticMap);

        if (PhysicsPresent != _wasPresent)
        {
            _wasPresent = PhysicsPresent;
            _log.LogInfo(PhysicsPresent
                ? $"EVO SHM attached (graphics={GraphicsPresent} static={StaticPresent})"
                : "EVO SHM lost");
        }

        if (!PhysicsPresent) { Reset(); return null; }

        try
        {
            _mmf ??= MemoryMappedFile.OpenExisting(PhysicsMap, MemoryMappedFileRights.Read);
            _view ??= _mmf.CreateViewAccessor(0, 64, MemoryMappedFileAccess.Read);

            return new TelemetrySample(
                _view.ReadInt32(OffPacketId),
                _view.ReadSingle(OffGas),
                _view.ReadSingle(OffBrake),
                _view.ReadInt32(OffGear),
                _view.ReadInt32(OffRpms),
                _view.ReadSingle(OffSteer),
                _view.ReadSingle(OffSpeedKmh));
        }
        catch (Exception ex)
        {
            _log.LogWarning($"EVO SHM read failed ({ex.GetType().Name}); resetting");
            Reset();
            return null;
        }
    }

    private void Reset()
    {
        _view?.Dispose();
        _mmf?.Dispose();
        _view = null;
        _mmf = null;
    }
}
