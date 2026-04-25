using System;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using Xunit;

namespace AkashaNavigator.Tests.Services
{
    /// <summary>
    /// MonitorLayoutService 单元测试
    /// 测试显示器布局服务的核心功能和事件发布行为
    /// </summary>
    public class MonitorLayoutServiceTests : IDisposable
    {
        private readonly FakeLogService _logService;
        private readonly RecordingEventBus _eventBus;
        private readonly MonitorLayoutService _service;

        public MonitorLayoutServiceTests()
        {
            _logService = new FakeLogService();
            _eventBus = new RecordingEventBus();
            _service = new MonitorLayoutService(_logService, _eventBus);
        }

        public void Dispose()
        {
            _service.Dispose();
        }

        #region GetMonitorFromWindow Tests

        [Fact]
        public void GetMonitorFromWindow_WithZeroHandle_ReturnsPrimaryMonitor()
        {
            // Arrange - IntPtr.Zero uses MONITOR_DEFAULTTONEAREST which returns primary monitor
            IntPtr zeroHandle = IntPtr.Zero;

            // Act
            var result = _service.GetMonitorFromWindow(zeroHandle);

            // Assert - MONITOR_DEFAULTTONEAREST falls back to primary monitor
            Assert.NotNull(result);
            Assert.True(result.IsPrimary, "Zero handle should return primary monitor via MONITOR_DEFAULTTONEAREST");
        }

        [Fact]
        public void GetMonitorFromWindowOrDefault_WithZeroHandle_ReturnsPrimaryMonitor()
        {
            // Arrange
            IntPtr zeroHandle = IntPtr.Zero;

            // Act
            var result = _service.GetMonitorFromWindowOrDefault(zeroHandle);

            // Assert
            Assert.NotNull(result);
            // Should fall back to primary monitor
            Assert.True(result.IsPrimary || result.MonitorRect.Right > 0);
        }

        #endregion

        #region GetPrimaryMonitor Tests

        [Fact]
        public void GetPrimaryMonitor_ReturnsNonNullMonitor()
        {
            // Act
            var result = _service.GetPrimaryMonitor();

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void GetPrimaryMonitor_ReturnsMonitorWithValidDimensions()
        {
            // Act
            var result = _service.GetPrimaryMonitor();

            // Assert - primary monitor should have positive dimensions
            Assert.True(result.MonitorRect.Right > 0, "Monitor width should be positive");
            Assert.True(result.MonitorRect.Bottom > 0, "Monitor height should be positive");
        }

        [Fact]
        public void GetPrimaryMonitor_WorkAreaIsSmallerThanOrEqualToMonitorRect()
        {
            // Act
            var result = _service.GetPrimaryMonitor();

            // Assert - work area should fit within monitor rect
            Assert.True(result.WorkAreaRect.Left >= result.MonitorRect.Left,
                "Work area left should be >= monitor left");
            Assert.True(result.WorkAreaRect.Top >= result.MonitorRect.Top,
                "Work area top should be >= monitor top");
            Assert.True(result.WorkAreaRect.Right <= result.MonitorRect.Right,
                "Work area right should be <= monitor right");
            Assert.True(result.WorkAreaRect.Bottom <= result.MonitorRect.Bottom,
                "Work area bottom should be <= monitor bottom");
        }

        [Fact]
        public void GetPrimaryMonitor_HasDeviceName()
        {
            // Act
            var result = _service.GetPrimaryMonitor();

            // Assert - device name should not be empty
            Assert.False(string.IsNullOrEmpty(result.DeviceName),
                "Primary monitor should have a device name");
        }

        #endregion

        #region FindMonitorByDeviceName Tests

        [Fact]
        public void FindMonitorByDeviceName_WithNull_ReturnsNull()
        {
            // Act
            var result = _service.FindMonitorByDeviceName(null!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindMonitorByDeviceName_WithEmptyString_ReturnsNull()
        {
            // Act
            var result = _service.FindMonitorByDeviceName(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindMonitorByDeviceName_WithNonExistentName_ReturnsNull()
        {
            // Act
            var result = _service.FindMonitorByDeviceName("\\\\DISPLAY_NONEXISTENT");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindMonitorByDeviceName_WithValidPrimaryName_ReturnsMonitor()
        {
            // Arrange - Get the primary monitor's device name first
            var primary = _service.GetPrimaryMonitor();

            // Act
            var result = _service.FindMonitorByDeviceName(primary.DeviceName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(primary.DeviceName, result.DeviceName);
        }

        #endregion

        #region Refresh Tests

        [Fact]
        public void Refresh_DoesNotThrow()
        {
            // Act & Assert - should not throw
            _service.Refresh();
        }

        #endregion

        #region MonitorInfo Conversion Tests

        [Fact]
        public void MonitorInfo_GetWorkAreaAsWpfRect_WithDpiScale1_ReturnsCorrectDimensions()
        {
            // Arrange
            var monitorInfo = new MonitorInfo
            {
                MonitorRect = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
                WorkAreaRect = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 },
                IsPrimary = true,
                DeviceName = "\\\\.\\DISPLAY1"
            };

            // Act
            var wpfRect = monitorInfo.GetWorkAreaAsWpfRect(1.0);

            // Assert
            Assert.Equal(0, wpfRect.Left);
            Assert.Equal(0, wpfRect.Top);
            Assert.Equal(1920, wpfRect.Width);
            Assert.Equal(1040, wpfRect.Height);
        }

        [Fact]
        public void MonitorInfo_GetWorkAreaAsWpfRect_WithDpiScale125_ScalesCorrectly()
        {
            // Arrange - 125% DPI scaling means dpiScale = 1.25
            var monitorInfo = new MonitorInfo
            {
                MonitorRect = new Win32Helper.RECT { Left = 0, Top = 0, Right = 2400, Bottom = 1350 },
                WorkAreaRect = new Win32Helper.RECT { Left = 0, Top = 0, Right = 2400, Bottom = 1300 },
                IsPrimary = true,
                DeviceName = "\\\\.\\DISPLAY1"
            };

            // Act
            var wpfRect = monitorInfo.GetWorkAreaAsWpfRect(1.25);

            // Assert
            Assert.Equal(0, wpfRect.Left);
            Assert.Equal(0, wpfRect.Top);
            Assert.Equal(1920, wpfRect.Width, 1);  // 2400 / 1.25 = 1920
            Assert.Equal(1040, wpfRect.Height, 1);  // 1300 / 1.25 = 1040
        }

        [Fact]
        public void MonitorInfo_GetMonitorRectAsWpfRect_WithNegativeCoordinates_HandlesSecondaryMonitor()
        {
            // Arrange - Secondary monitor to the left of primary
            var monitorInfo = new MonitorInfo
            {
                MonitorRect = new Win32Helper.RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1080 },
                WorkAreaRect = new Win32Helper.RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1040 },
                IsPrimary = false,
                DeviceName = "\\\\.\\DISPLAY2"
            };

            // Act
            var wpfRect = monitorInfo.GetWorkAreaAsWpfRect(1.0);

            // Assert
            Assert.Equal(-1920, wpfRect.Left);
            Assert.Equal(0, wpfRect.Top);
            Assert.Equal(1920, wpfRect.Width);
            Assert.Equal(1040, wpfRect.Height);
        }

        [Fact]
        public void MonitorInfo_GetMonitorRectAsWpfRect_WithDpiScaling()
        {
            // Arrange
            var monitorInfo = new MonitorInfo
            {
                MonitorRect = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
                WorkAreaRect = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 },
                IsPrimary = true,
                DeviceName = "\\\\.\\DISPLAY1"
            };

            // Act
            var wpfRect = monitorInfo.GetMonitorRectAsWpfRect(1.0);

            // Assert
            Assert.Equal(0, wpfRect.Left);
            Assert.Equal(0, wpfRect.Top);
            Assert.Equal(1920, wpfRect.Width);
            Assert.Equal(1080, wpfRect.Height);
        }

        #endregion

        #region WindowState MonitorDeviceName Tests

        [Fact]
        public void WindowState_MonitorDeviceName_DefaultIsNull()
        {
            // Arrange
            var state = new AkashaNavigator.Models.Config.WindowState();

            // Assert
            Assert.Null(state.MonitorDeviceName);
        }

        [Fact]
        public void WindowState_MonitorDeviceName_CanBeSetAndRead()
        {
            // Arrange
            var state = new AkashaNavigator.Models.Config.WindowState
            {
                MonitorDeviceName = "\\\\.\\DISPLAY1"
            };

            // Assert
            Assert.Equal("\\\\.\\DISPLAY1", state.MonitorDeviceName);
        }

        #endregion
    }
}