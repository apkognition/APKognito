using System.Windows.Media;

namespace APKognito.Controls.Overrides;

public static class ListViewSettings
{
    public static readonly DependencyProperty AlternateBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "AlternateBackground",
            typeof(Brush),
            typeof(ListViewSettings),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static void SetAlternateBackground(DependencyObject element, Brush value)
    {
        element.SetValue(AlternateBackgroundProperty, value);
    }

    public static Brush GetAlternateBackground(DependencyObject element)
    {
        return (Brush)element.GetValue(AlternateBackgroundProperty);
    }
}
