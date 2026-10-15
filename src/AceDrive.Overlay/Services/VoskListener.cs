using System.IO;
using System.Runtime.InteropServices;
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
        // WHY: every realistic way voice can fail to start is a model-file
        // error (IO/access), a native Vosk DLL error (load/format) or a mic
        // device error (com/invalid/not-supported). Any of them degrades to
        // "voice off" per App.CreateVoice's contract; anything else is a bug
        // and must surface rather than hide behind voice-off (CA1031).
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or FileLoadException
            or InvalidOperationException or NotSupportedException or COMException)
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
        // WHY: the audio thread has no dispatcher handler, so a dead device
        // or the dispose race mid-callback would take the overlay (and the
        // game session beside it) down. Recognizer/driver state errors and
        // the disposal race are the whole recoverable set (IVoiceListener:
        // degrade to silence); a bug elsewhere in the chain is NOT muted here.
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
        {
        }
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
        // WHY: a torn/mis-shaped fragment (JsonException) or a non-string
        // text element (InvalidOperationException) is expected noise from a
        // streaming recognizer; the next fragment follows shortly.
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
        }
    }

    private void Cleanup()
    {
        // WHY: StopRecording on an already-dead or disposed device is the
        // expected shutdown race; the overlay must close quietly (CA1031:
        // specific types, not a blanket mute).
        try { _mic?.StopRecording(); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
        {
        }
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
