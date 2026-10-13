namespace AceDrive.Overlay.ViewModels;

/// <summary>A screen whose buttons can be fired by speaking their labels.</summary>
public interface IVoiceScreen
{
    IReadOnlyDictionary<string, Action> VoiceActions { get; }
}
