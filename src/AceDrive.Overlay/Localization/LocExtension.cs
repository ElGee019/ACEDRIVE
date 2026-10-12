using System.Windows.Markup;

namespace AceDrive.Overlay.Localization;

/// <summary>XAML usage: <c>Text="{loc:Loc Key=AppTitle}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public string? Key { get; set; }

    public LocExtension() { }
    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Key is null ? string.Empty : Strings.Get(Key);
}
