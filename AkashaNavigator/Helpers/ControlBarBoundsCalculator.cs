using System;

namespace AkashaNavigator.Helpers;

internal static class ControlBarBoundsCalculator
{
    private const double MinimumWidthDip = 400.0;
    private const double TopMarginDip = 2.0;

    public static Win32Helper.RECT Calculate(MonitorInfo monitor,
                                             double dpiScale,
                                             double centerAnchorRatio,
                                             double heightDip)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        dpiScale = Math.Max(dpiScale, 1.0);
        centerAnchorRatio = Math.Clamp(centerAnchorRatio, 0.0, 1.0);

        double workAreaWidthPx = monitor.WorkAreaRect.Right - monitor.WorkAreaRect.Left;
        double workAreaHeightPx = monitor.WorkAreaRect.Bottom - monitor.WorkAreaRect.Top;
        double widthPx = Math.Min(workAreaWidthPx,
                                  Math.Max(workAreaWidthPx / 3.0, MinimumWidthDip * dpiScale));
        double heightPx = Math.Min(workAreaHeightPx, Math.Max(1.0, heightDip * dpiScale));

        double desiredCenterPx =
            monitor.WorkAreaRect.Left + (workAreaWidthPx * centerAnchorRatio);
        double minimumCenterPx = monitor.WorkAreaRect.Left + (widthPx / 2.0);
        double maximumCenterPx = monitor.WorkAreaRect.Right - (widthPx / 2.0);
        double actualCenterPx = Math.Clamp(desiredCenterPx, minimumCenterPx, maximumCenterPx);
        double leftPx = actualCenterPx - (widthPx / 2.0);
        double topPx = monitor.WorkAreaRect.Top + (TopMarginDip * dpiScale);

        int left = (int)Math.Round(leftPx, MidpointRounding.AwayFromZero);
        int top = (int)Math.Round(topPx, MidpointRounding.AwayFromZero);
        int width = (int)Math.Round(widthPx, MidpointRounding.AwayFromZero);
        int height = (int)Math.Round(heightPx, MidpointRounding.AwayFromZero);

        return new Win32Helper.RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height
        };
    }
}
