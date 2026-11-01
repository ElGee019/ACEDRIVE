using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Finds the control a window-level click belongs to. The routed event cannot
/// be asked directly: a click on a page button reaches the window with
/// <see cref="RoutedEventArgs.Source"/> set to the page host
/// <see cref="ContentControl"/>, and the button only under
/// <see cref="RoutedEventArgs.OriginalSource"/>. Testing <c>Source is Button</c>
/// therefore never matched, which silently disabled the overlay's focus
/// hand-back for every button on every page.
/// </summary>
internal static class ClickTarget
{
    /// <summary>
    /// The nearest interactive ancestor of <paramref name="source"/>, or null
    /// when the click belongs to the window chrome.
    /// </summary>
    internal static DependencyObject? OwnerOf(DependencyObject? source)
    {
        while (source is not null && !IsInteractive(source))
        {
            // WHY the guard: a button label is inline content (a Run), a
            // ContentElement, and VisualTreeHelper.GetParent throws on it.
            // Such an origin stops the walk and counts as "not interactive"
            // (2D-only overlay, so checking Visual, not Visual3D, is enough).
            if (source is not Visual)
            {
                return null;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return source;
    }

    // Buttons own their clicks. A TextBoxBase needs the press for its caret
    // (the opening name, the bank amount and the job radius fields), and a
    // ComboBoxItem needs it because the open ComboBox captures the mouse and a
    // row's press tunnels through the window's preview handler — dragging there
    // stole the click and the row only committed on Enter. Everything else is
    // chrome, which drags the panel.
    private static bool IsInteractive(DependencyObject source) =>
        source is ButtonBase or TextBoxBase or ComboBoxItem;
}
