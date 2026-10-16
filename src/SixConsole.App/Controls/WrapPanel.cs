using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace SixConsole.App.Controls;

/// <summary>
/// Minimal flow/wrap layout panel for the operator roster tiles (native, no toolkit dependency).
/// </summary>
public sealed class WrapPanel : Panel
{
    public static readonly DependencyProperty DesiredItemWidthProperty =
        DependencyProperty.Register(nameof(DesiredItemWidth), typeof(double), typeof(WrapPanel), new PropertyMetadata(0d, OnMetricsChanged));

    public double DesiredItemWidth
    {
        get => (double)GetValue(DesiredItemWidthProperty);
        set => SetValue(DesiredItemWidthProperty, value);
    }

    private static void OnMetricsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WrapPanel)d).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxChildW = 0, maxChildH = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, availableSize.Height));
            maxChildW = Math.Max(maxChildW, child.DesiredSize.Width);
            maxChildH = Math.Max(maxChildH, child.DesiredSize.Height);
        }
        double rowWidth = double.IsInfinity(availableSize.Width) ? maxChildW * Children.Count : availableSize.Width;
        double w = DesiredItemWidth > 0 ? Math.Max(maxChildW, DesiredItemWidth) : maxChildW;
        double columns = Math.Max(1, (int)Math.Floor((rowWidth + 4) / (w + 4)));
        int rows = (int)Math.Ceiling(Children.Count / columns);
        if (columns <= 0) columns = 1;
        // Tiles carry a 4px bottom margin; rows are separated by that gap in both
        // measure and arrange so vertical rhythm matches the horizontal rhythm.
        double height = Children.Count == 0 ? 0 : rows * maxChildH + (rows - 1) * 4;
        return new Size(Math.Min(rowWidth, columns * w + Math.Max(0, columns - 1) * 4), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double w = DesiredItemWidth > 0 ? DesiredItemWidth : 0;
        double[] childWidths = new double[Children.Count];
        double maxChildW = 0, maxChildH = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            childWidths[i] = Children[i].DesiredSize.Width;
            maxChildW = Math.Max(maxChildW, childWidths[i]);
            maxChildH = Math.Max(maxChildH, Children[i].DesiredSize.Height);
        }
        double tileW = w > 0 ? Math.Max(w, maxChildW) : maxChildW;
        double columns = Math.Max(1, (int)Math.Floor((finalSize.Width + 4) / (tileW + 4)));
        if (columns <= 0) columns = 1;
        double x = 0, y = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Arrange(new Rect(x, y, childWidths[i], maxChildH));
            x += tileW + 4;
            if (x + tileW > finalSize.Width + 0.1)
            {
                x = 0;
                y += maxChildH + 4;
            }
        }
        return finalSize;
    }
}
