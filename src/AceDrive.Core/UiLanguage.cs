// The overlay's UI languages: one satellite resx each (Strings.<culture>.resx).
// The culture code that backs each value is mapped in the Overlay, keeping
// Core free of System.Globalization for the Lua port.
namespace AceDrive.Core;

/// <summary>UI language the player picked on the first-run page.</summary>
public enum UiLanguage
{
    English,
    Dutch,
}
