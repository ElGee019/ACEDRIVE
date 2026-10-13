using System.Text.Json;
using AceDrive.Core;
using NAudio.Wave;
using Vosk;

namespace AceDrive.Overlay.Services;

/// <summary>Mic -&gt; Vosk offline STT -&gt; text fragments. Owns the audio thread.</summary>
public sealed class VoskListener : IVoiceListener
{
    private readonly string _modelPath;
    private readonly ILog _log;
    private readonly int _sampleRate;
    private Model? _model;
    private VoskRecognizer? _rec;
    private WaveIn? _mic;
    private bool _running;
    private bool _disposed;

    public VoskListener(string modelPath, ILog log, int sampleRate = 16000)
    {
        _modelPath = modelPath;
        _log = log;
        _sampleRate = sampleRate;
    }

    public bool IsAvailable => true;

    public event EventHandler<VoiceTextEventArgs>? Heard;

    public void StartListening()
    {
        if (_running || _disposed) return;
        try
        {
            Vosk.Vosk.SetLogLevel(-1);
            _model = new Model(_modelPath);
            _rec = new VoskRecognizer(_model, _sampleRate);
            _rec.SetWords(false);
            _mic = new WaveIn { WaveFormat = new WaveFormat(_sampleRate, 1) };
            _mic.DataAvailable += OnAudio;
            _mic.StartRecording();
            _running = true;
            _log.LogInfo($"voice listening (model {_modelPath})");
        }
        catch (Exception ex)
        {
            _log.LogWarning($"voice start failed ({ex.GetType().Name}); voice off");
            Cleanup();
        }
    }

    public void StopListening()
    {
        _running = false;
        Cleanup();
    }

    private void OnAudio(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (!_running || _rec is null || e.BytesRecorded == 0) return;
            if (_rec.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                Emit(_rec.Result(), isFinal: true);
            }
            else
            {
                Emit(_rec.PartialResult(), isFinal: false);
            }
        }
        catch { /* never break the audio thread */ }
    }

    private void Emit(string json, bool isFinal)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            string key = isFinal ? "text" : "partial";
            if (doc.RootElement.TryGetProperty(key, out JsonElement el) &&
                el.GetString() is { Length: > 0 } text)
            {
                Heard?.Invoke(this, new VoiceTextEventArgs(text, isFinal));
            }
        }
        catch { /* torn fragment; the next one follows shortly */ }
    }

    private void Cleanup()
    {
        try { _mic?.StopRecording(); } catch { }
        _mic?.Dispose();
        _mic = null;
        _rec?.Dispose();
        _rec = null;
        _model?.Dispose();
        _model = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopListening();
    }
}
