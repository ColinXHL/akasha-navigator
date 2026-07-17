using AkashaNavigator.Helpers;
using Xunit;

namespace AkashaNavigator.Tests.Windows;

public class ControlBarBoundsCalculatorTests
{
    [Fact]
    public void Calculate_CentersControlBarInWorkArea()
    {
        var monitor = CreateMonitor(left: 0, top: 0, width: 1920, height: 1040);

        var result = ControlBarBoundsCalculator.Calculate(
            monitor,
            dpiScale: 1.0,
            centerAnchorRatio: 0.5,
            heightDip: 55);

        Assert.Equal(640, result.Left);
        Assert.Equal(2, result.Top);
        Assert.Equal(1280, result.Right);
        Assert.Equal(57, result.Bottom);
    }

    [Fact]
    public void Calculate_UsesPhysicalCoordinatesForNegativeMonitorOrigin()
    {
        var monitor = CreateMonitor(left: -2560, top: -200, width: 2560, height: 1440);

        var result = ControlBarBoundsCalculator.Calculate(
            monitor,
            dpiScale: 1.25,
            centerAnchorRatio: 0.5,
            heightDip: 16);

        Assert.Equal(-1707, result.Left);
        Assert.Equal(-198, result.Top);
        Assert.Equal(-854, result.Right);
        Assert.Equal(-178, result.Bottom);
    }

    [Fact]
    public void Calculate_ClampsMinimumWidthAndAnchorToWorkArea()
    {
        var monitor = CreateMonitor(left: 100, top: 50, width: 900, height: 700);

        var result = ControlBarBoundsCalculator.Calculate(
            monitor,
            dpiScale: 1.5,
            centerAnchorRatio: 0.0,
            heightDip: 55);

        Assert.Equal(100, result.Left);
        Assert.Equal(53, result.Top);
        Assert.Equal(700, result.Right);
        Assert.Equal(136, result.Bottom);
    }

    private static MonitorInfo CreateMonitor(int left, int top, int width, int height)
    {
        return new MonitorInfo
        {
            WorkAreaRect = new Win32Helper.RECT
            {
                Left = left,
                Top = top,
                Right = left + width,
                Bottom = top + height
            }
        };
    }
}
