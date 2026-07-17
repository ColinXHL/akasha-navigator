using AkashaNavigator.Helpers;
using Xunit;

namespace AkashaNavigator.Tests.Helpers;

public class EdgeSnapCalculatorTests
{
    private static readonly Win32Helper.RECT Monitor =
        new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

    private static readonly Win32Helper.RECT WorkAreaWithTopTaskbar =
        new() { Left = 0, Top = 40, Right = 1920, Bottom = 1080 };

    [Fact]
    public void Calculate_TopEdge_UsesPhysicalMonitorTop()
    {
        var intended = Rect(300, 8, 640, 360);

        var result = Calculate(intended, WorkAreaWithTopTaskbar);

        Assert.Equal(0, result.Rect.Top);
    }

    [Fact]
    public void Calculate_TopEdgeNearMonitorCenter_SnapsHorizontallyToCenter()
    {
        var intended = Rect(650, 5, 640, 360);

        var result = Calculate(intended, WorkAreaWithTopTaskbar);

        Assert.Equal(640, result.Rect.Left);
        Assert.Equal(0, result.Rect.Top);
        Assert.True(result.State.IsHorizontalCenterSnapped);
    }

    [Fact]
    public void Calculate_BottomEdgeNearMonitorCenter_SnapsHorizontallyToCenter()
    {
        var intended = Rect(630, 715, 640, 360);

        var result = Calculate(intended, WorkAreaWithTopTaskbar);

        Assert.Equal(640, result.Rect.Left);
        Assert.Equal(720, result.Rect.Top);
        Assert.True(result.State.IsHorizontalCenterSnapped);
    }

    [Theory]
    [InlineData(5, 350, 0, 360)]
    [InlineData(1275, 350, 1280, 360)]
    public void Calculate_SideEdgeNearMonitorCenter_SnapsVerticallyToCenter(
        int left,
        int top,
        int expectedLeft,
        int expectedTop)
    {
        var intended = Rect(left, top, 640, 360);

        var result = Calculate(intended, WorkAreaWithTopTaskbar);

        Assert.Equal(expectedLeft, result.Rect.Left);
        Assert.Equal(expectedTop, result.Rect.Top);
        Assert.True(result.State.IsVerticalCenterSnapped);
    }

    [Fact]
    public void Calculate_Corner_RemainsFreelyPositionedAlongBothEdges()
    {
        var intended = Rect(4, 6, 640, 360);

        var result = Calculate(intended, WorkAreaWithTopTaskbar);

        Assert.Equal(0, result.Rect.Left);
        Assert.Equal(0, result.Rect.Top);
        Assert.False(result.State.IsHorizontalCenterSnapped);
        Assert.False(result.State.IsVerticalCenterSnapped);
    }

    [Fact]
    public void Calculate_CenterSnap_UsesReleaseHysteresis()
    {
        var intended = Rect(666, 5, 640, 360);
        var previous = new EdgeSnapState(IsHorizontalCenterSnapped: true, IsVerticalCenterSnapped: false);

        var result = EdgeSnapCalculator.Calculate(
            intended, WorkAreaWithTopTaskbar, Monitor, edgeThreshold: 15, centerThreshold: 20,
            centerHysteresis: 8, previous);

        Assert.Equal(640, result.Rect.Left);
        Assert.True(result.State.IsHorizontalCenterSnapped);
    }

    [Fact]
    public void Calculate_CenterSnap_ReleasesOutsideHysteresis()
    {
        var intended = Rect(669, 5, 640, 360);
        var previous = new EdgeSnapState(IsHorizontalCenterSnapped: true, IsVerticalCenterSnapped: false);

        var result = EdgeSnapCalculator.Calculate(
            intended, WorkAreaWithTopTaskbar, Monitor, edgeThreshold: 15, centerThreshold: 20,
            centerHysteresis: 8, previous);

        Assert.Equal(669, result.Rect.Left);
        Assert.False(result.State.IsHorizontalCenterSnapped);
    }

    [Fact]
    public void Calculate_MultiMonitorWithNegativeCoordinates_UsesThatMonitorCenter()
    {
        var monitor = new Win32Helper.RECT { Left = -2560, Top = -200, Right = 0, Bottom = 1240 };
        var workArea = new Win32Helper.RECT { Left = -2560, Top = -160, Right = 0, Bottom = 1240 };
        var intended = Rect(-1610, -195, 640, 360);

        var result = EdgeSnapCalculator.Calculate(
            intended, workArea, monitor, edgeThreshold: 15, centerThreshold: 20, centerHysteresis: 8, default);

        Assert.Equal(-1600, result.Rect.Left);
        Assert.Equal(-200, result.Rect.Top);
    }

    [Fact]
    public void Calculate_WhenEdgeSnappingIsDisabled_DoesNotSnapAtExactEdge()
    {
        var intended = Rect(650, 0, 640, 360);

        var result = EdgeSnapCalculator.Calculate(
            intended, WorkAreaWithTopTaskbar, Monitor, edgeThreshold: -1, centerThreshold: 30,
            centerHysteresis: 8, default);

        Assert.Equal(intended, result.Rect);
        Assert.Equal(default, result.State);
    }

    private static EdgeSnapResult Calculate(Win32Helper.RECT intended, Win32Helper.RECT workArea)
    {
        return EdgeSnapCalculator.Calculate(
            intended, workArea, Monitor, edgeThreshold: 15, centerThreshold: 30, centerHysteresis: 8, default);
    }

    private static Win32Helper.RECT Rect(int left, int top, int width, int height)
    {
        return new Win32Helper.RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height
        };
    }
}
