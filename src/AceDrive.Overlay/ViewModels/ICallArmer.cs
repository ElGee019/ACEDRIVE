using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Arms the call listener with the input the player assigned, so the panel
/// answers it while the game owns the screen — without taking the press away
/// from the game. Null arms the shipped chord (<see cref="CallBinding.Default"/>).
/// </summary>
public interface ICallArmer
{
    /// <summary>Makes <paramref name="binding"/> the call from now on.</summary>
    void Arm(CallBinding? binding);
}
