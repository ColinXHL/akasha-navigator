using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AkashaNavigator.Services;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Panel 绘图上下文
/// </summary>
public class PanelContext
{
    private readonly string _pluginId;

    public string fillStyle { get; set; } = "#000000";
    public string strokeStyle { get; set; } = "#000000";
    public double lineWidth { get; set; } = 1.0;
    public string font { get; set; } = "14px Microsoft YaHei";
    public string textAlign { get; set; } = "left";

    private readonly List<Point> _pathPoints = new();

    public PanelContext(string pluginId)
    {
        _pluginId = pluginId;
    }

    public void clear()
    {
        var panel = PanelManager.Instance.GetPanel(_pluginId);
        System.Windows.Application.Current?.Dispatcher.Invoke(() => panel?.ClearDrawingElements());
    }

    public void fillRect(double x, double y, double width, double height)
    {
        var panel = PanelManager.Instance.GetPanel(_pluginId);
        if (panel == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  var options = new Views.Windows.DrawRectOptions {
                                                                      Fill = fillStyle,
                                                                      Opacity = 1.0
                                                                  };
                                                                  panel.DrawRect(x, y, width, height, options);
                                                              });
    }

    public void strokeRect(double x, double y, double width, double height)
    {
        var panel = PanelManager.Instance.GetPanel(_pluginId);
        if (panel == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  var options = new Views.Windows.DrawRectOptions {
                                                                      Stroke = strokeStyle,
                                                                      StrokeWidth = lineWidth,
                                                                      Opacity = 1.0
                                                                  };
                                                                  panel.DrawRect(x, y, width, height, options);
                                                              });
    }

    public void fillText(string text, double x, double y)
    {
        var panel = PanelManager.Instance.GetPanel(_pluginId);
        if (panel == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  double fontSize = 14;
                                                                  string fontFamily = "Microsoft YaHei";
                                                                  if (!string.IsNullOrEmpty(font))
                                                                  {
                                                                      var parts = font.Split(' ');
                                                                      if (parts.Length >= 2)
                                                                      {
                                                                          var sizeStr = parts[0].Replace("px", "").Replace("pt", "");
                                                                          if (double.TryParse(sizeStr, out var size))
                                                                              fontSize = size;
                                                                          fontFamily = string.Join(" ", parts, 1, parts.Length - 1);
                                                                      }
                                                                  }

                                                                  var options = new Views.Windows.DrawTextOptions {
                                                                      Color = fillStyle,
                                                                      FontSize = fontSize,
                                                                      FontFamily = fontFamily,
                                                                      Opacity = 1.0
                                                                  };

                                                                  double adjustedX = x;
                                                                  if (string.Equals(textAlign, "center", StringComparison.OrdinalIgnoreCase))
                                                                  {
                                                                      adjustedX = x - (text.Length * fontSize * 0.25);
                                                                  }
                                                                  else if (string.Equals(textAlign, "right", StringComparison.OrdinalIgnoreCase))
                                                                  {
                                                                      adjustedX = x - (text.Length * fontSize * 0.5);
                                                                  }

                                                                  panel.DrawText(text, adjustedX, y, options);
                                                              });
    }

    public void beginPath()
    {
        _pathPoints.Clear();
    }

    public void moveTo(double x, double y)
    {
        _pathPoints.Clear();
        _pathPoints.Add(new Point(x, y));
    }

    public void lineTo(double x, double y)
    {
        _pathPoints.Add(new Point(x, y));
    }

    public void stroke()
    {
        var panel = PanelManager.Instance.GetPanel(_pluginId);
        if (panel == null)
            return;

        if (_pathPoints.Count < 2)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () => { panel.DrawLine(_pathPoints, strokeStyle, lineWidth); });
    }

    public void drawImage(string src, double x, double y, double? width = null, double? height = null)
    {
        var panel = PanelManager.Instance.GetPanel(_pluginId);
        if (panel == null)
            return;

        if (!Path.IsPathRooted(src))
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  var options = new Views.Windows.DrawImageOptions {
                                                                      Width = width,
                                                                      Height = height,
                                                                      Opacity = 1.0
                                                                  };
                                                                  panel.DrawImage(src, x, y, options);
                                                              });
    }
}
}
