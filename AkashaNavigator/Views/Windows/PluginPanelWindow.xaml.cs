using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// 插件独立窗口（可交互普通窗口）
/// </summary>
public partial class PluginPanelWindow : Window
{
    private readonly List<UIElement> _drawingElements = new();

    public string PluginId { get; }

    public event EventHandler<Point>? CanvasClicked;
    public event EventHandler<PanelPageItem>? PageItemClicked;
    public event EventHandler<string>? ActionButtonClicked;

    public PluginPanelWindow(string pluginId)
    {
        InitializeComponent();
        PluginId = pluginId;
    }

    public void SetPosition(double x, double y)
    {
        Left = x;
        Top = y;
    }

    public void SetSize(double width, double height)
    {
        if (width > 0)
            Width = width;
        if (height > 0)
            Height = height;
    }

    public (double X, double Y, double Width, double Height) GetRect()
    {
        return (Left, Top, Width, Height);
    }

    public void SetHeader(string title, string? hint = null)
    {
        HeaderTitleText.Text = string.IsNullOrWhiteSpace(title) ? "分P列表" : title;
        HeaderHintText.Text = string.IsNullOrWhiteSpace(hint) ? "单击条目可快速跳转" : hint;
    }

    public void SetPageItems(IReadOnlyList<PanelPageItem> items)
    {
        PageListBox.ItemsSource = items;
        ScrollToActivePage(items);
    }

    private void ScrollToActivePage(IReadOnlyList<PanelPageItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        var targetItem = items.FirstOrDefault(item => item.IsActive) ?? items[0];
        PageListBox.SelectedItem = targetItem;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            PageListBox.UpdateLayout();
            PageListBox.ScrollIntoView(targetItem);
        }), DispatcherPriority.Background);
    }

    public void SetActionButtonLabels(string? danmaku, string? subtitle)
    {
        if (!string.IsNullOrWhiteSpace(danmaku))
            DanmakuButton.Content = danmaku;
        if (!string.IsNullOrWhiteSpace(subtitle))
            SubtitleButton.Content = subtitle;
    }

    public void SetActionButtonStates(bool? danmakuEnabled, bool? subtitleEnabled)
    {
        if (danmakuEnabled.HasValue)
            DanmakuButton.IsChecked = danmakuEnabled.Value;

        if (subtitleEnabled.HasValue)
            SubtitleButton.IsChecked = subtitleEnabled.Value;
    }

    public void SetDrawingMode(bool enabled)
    {
        DrawingCanvas.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        PageListBox.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
    }

    public string DrawRect(double x, double y, double width, double height, DrawRectOptions? options = null)
    {
        SetDrawingMode(true);
        options ??= new DrawRectOptions();

        var rect = new Rectangle {
            Width = Math.Max(0, width),
            Height = Math.Max(0, height),
            Fill = ParseBrush(options.Fill, Brushes.Transparent),
            Stroke = ParseBrush(options.Stroke, null),
            StrokeThickness = Math.Max(0, options.StrokeWidth),
            Opacity = Math.Clamp(options.Opacity, 0, 1),
            RadiusX = Math.Max(0, options.CornerRadius),
            RadiusY = Math.Max(0, options.CornerRadius)
        };

        Canvas.SetLeft(rect, Math.Max(0, x));
        Canvas.SetTop(rect, Math.Max(0, y));
        DrawingCanvas.Children.Add(rect);
        _drawingElements.Add(rect);

        return $"{PluginId}_rect_{Guid.NewGuid():N}";
    }

    public string DrawText(string text, double x, double y, DrawTextOptions? options = null)
    {
        SetDrawingMode(true);
        options ??= new DrawTextOptions();

        var textBlock = new TextBlock {
            Text = text,
            FontSize = Math.Max(1, options.FontSize),
            FontFamily = new FontFamily(options.FontFamily ?? "Microsoft YaHei"),
            Foreground = ParseBrush(options.Color, Brushes.Black),
            Opacity = Math.Clamp(options.Opacity, 0, 1)
        };

        if (!string.IsNullOrEmpty(options.BackgroundColor))
        {
            var border = new Border {
                Background = ParseBrush(options.BackgroundColor, Brushes.Transparent),
                Padding = new Thickness(4, 2, 4, 2),
                Child = textBlock
            };

            Canvas.SetLeft(border, Math.Max(0, x));
            Canvas.SetTop(border, Math.Max(0, y));
            DrawingCanvas.Children.Add(border);
            _drawingElements.Add(border);
        }
        else
        {
            Canvas.SetLeft(textBlock, Math.Max(0, x));
            Canvas.SetTop(textBlock, Math.Max(0, y));
            DrawingCanvas.Children.Add(textBlock);
            _drawingElements.Add(textBlock);
        }

        return $"{PluginId}_text_{Guid.NewGuid():N}";
    }

    public string DrawLine(IReadOnlyList<Point> points, string? stroke, double lineWidth)
    {
        SetDrawingMode(true);
        if (points.Count < 2)
            return string.Empty;

        var polyline = new Polyline {
            Stroke = ParseBrush(stroke, Brushes.Black),
            StrokeThickness = Math.Max(1, lineWidth)
        };

        foreach (var point in points)
        {
            polyline.Points.Add(point);
        }

        DrawingCanvas.Children.Add(polyline);
        _drawingElements.Add(polyline);
        return $"{PluginId}_line_{Guid.NewGuid():N}";
    }

    public string DrawImage(string path, double x, double y, DrawImageOptions? options = null)
    {
        SetDrawingMode(true);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return string.Empty;

        options ??= new DrawImageOptions();

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        var image = new Image { Source = bitmap, Opacity = Math.Clamp(options.Opacity, 0, 1), Stretch = Stretch.Fill };

        if (options.Width.HasValue && options.Width > 0)
            image.Width = options.Width.Value;
        if (options.Height.HasValue && options.Height > 0)
            image.Height = options.Height.Value;

        Canvas.SetLeft(image, Math.Max(0, x));
        Canvas.SetTop(image, Math.Max(0, y));
        DrawingCanvas.Children.Add(image);
        _drawingElements.Add(image);

        return $"{PluginId}_img_{Guid.NewGuid():N}";
    }

    public void ClearDrawingElements()
    {
        foreach (var element in _drawingElements)
        {
            DrawingCanvas.Children.Remove(element);
        }
        _drawingElements.Clear();
        SetDrawingMode(false);
    }

    private void PageItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (!int.TryParse(button.Tag?.ToString(), out var page))
            return;

        if (button.DataContext is PanelPageItem item && item.Page == page)
        {
            PageItemClicked?.Invoke(this, item);
        }
    }

    private void DanmakuButton_Click(object sender, RoutedEventArgs e)
    {
        ActionButtonClicked?.Invoke(this, "danmaku");
    }

    private void SubtitleButton_Click(object sender, RoutedEventArgs e)
    {
        ActionButtonClicked?.Invoke(this, "subtitle");
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);
        CanvasClicked?.Invoke(this, point);
    }

    private void PageListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindChild<ScrollViewer>(PageListBox);
        if (scrollViewer == null)
            return;

        if (e.Delta > 0)
            scrollViewer.LineUp();
        else
            scrollViewer.LineDown();

        e.Handled = true;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindChild<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private static Brush? ParseBrush(string? colorString, Brush? defaultBrush)
    {
        if (string.IsNullOrEmpty(colorString))
            return defaultBrush;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorString);
            return new SolidColorBrush(color);
        }
        catch
        {
            return defaultBrush;
        }
    }
}

public class PanelPageItem
{
    public int Page { get; set; }

    public long Cid { get; set; }

    public string Part { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }

    public bool IsActive { get; set; }

    public string PageLabel => $"P{Page}";

    public string DurationLabel
    {
        get
        {
            var minutes = DurationSeconds / 60;
            var seconds = DurationSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }
    }
}
}
