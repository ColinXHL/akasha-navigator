using System;

namespace AkashaNavigator.Helpers;

/// <summary>
/// 边缘吸附期间需要跨 WM_MOVING 消息保留的居中状态。
/// </summary>
public readonly record struct EdgeSnapState(bool IsHorizontalCenterSnapped, bool IsVerticalCenterSnapped);

/// <summary>
/// 边缘吸附计算结果。
/// </summary>
public readonly record struct EdgeSnapResult(Win32Helper.RECT Rect, EdgeSnapState State);

/// <summary>
/// 使用物理像素计算窗口边缘和边缘中点吸附。
/// </summary>
public static class EdgeSnapCalculator
{
    public static EdgeSnapResult Calculate(
        Win32Helper.RECT intendedRect,
        Win32Helper.RECT workArea,
        Win32Helper.RECT monitorArea,
        int edgeThreshold,
        int centerThreshold,
        int centerHysteresis,
        EdgeSnapState previousState)
    {
        if (edgeThreshold < 0)
        {
            return new EdgeSnapResult(intendedRect, default);
        }

        var width = intendedRect.Right - intendedRect.Left;
        var height = intendedRect.Bottom - intendedRect.Top;
        var finalLeft = intendedRect.Left;
        var finalTop = intendedRect.Top;
        var snappedToHorizontalEdge = false;
        var snappedToVerticalEdge = false;

        if (Math.Abs(intendedRect.Left - workArea.Left) <= edgeThreshold)
        {
            finalLeft = workArea.Left;
            snappedToHorizontalEdge = true;
        }
        else if (Math.Abs(intendedRect.Right - workArea.Right) <= edgeThreshold)
        {
            finalLeft = workArea.Right - width;
            snappedToHorizontalEdge = true;
        }

        // 顶边始终使用显示器物理边界，避免顶部任务栏把播放器挡在 workArea.Top。
        if (Math.Abs(intendedRect.Top - monitorArea.Top) <= edgeThreshold)
        {
            finalTop = monitorArea.Top;
            snappedToVerticalEdge = true;
        }
        else if (Math.Abs(intendedRect.Bottom - workArea.Bottom) <= edgeThreshold)
        {
            finalTop = workArea.Bottom - height;
            snappedToVerticalEdge = true;
        }
        else if (Math.Abs(intendedRect.Bottom - monitorArea.Bottom) <= edgeThreshold)
        {
            finalTop = monitorArea.Bottom - height;
            snappedToVerticalEdge = true;
        }

        var monitorCenterX = monitorArea.Left + ((monitorArea.Right - monitorArea.Left) / 2.0);
        var monitorCenterY = monitorArea.Top + ((monitorArea.Bottom - monitorArea.Top) / 2.0);
        var intendedCenterX = intendedRect.Left + (width / 2.0);
        var intendedCenterY = intendedRect.Top + (height / 2.0);

        var horizontalLimit = centerThreshold +
                              (previousState.IsHorizontalCenterSnapped ? centerHysteresis : 0);
        var verticalLimit = centerThreshold +
                            (previousState.IsVerticalCenterSnapped ? centerHysteresis : 0);

        var horizontalCenterSnapped = snappedToVerticalEdge &&
                                      Math.Abs(intendedCenterX - monitorCenterX) <= horizontalLimit;
        var verticalCenterSnapped = snappedToHorizontalEdge &&
                                    Math.Abs(intendedCenterY - monitorCenterY) <= verticalLimit;

        if (horizontalCenterSnapped)
        {
            finalLeft = (int)Math.Round(monitorCenterX - (width / 2.0), MidpointRounding.AwayFromZero);
        }

        if (verticalCenterSnapped)
        {
            finalTop = (int)Math.Round(monitorCenterY - (height / 2.0), MidpointRounding.AwayFromZero);
        }

        return new EdgeSnapResult(
            new Win32Helper.RECT
            {
                Left = finalLeft,
                Top = finalTop,
                Right = finalLeft + width,
                Bottom = finalTop + height
            },
            new EdgeSnapState(horizontalCenterSnapped, verticalCenterSnapped));
    }
}
