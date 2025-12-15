using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 覆盖层窗口 - 用于在游戏上显示方向标记等内容
    /// 透明背景、无边框、置顶、鼠标穿透
    /// 所有坐标使用逻辑像素（WPF 自动处理 DPI 缩放）
    /// </summary>
    public partial class OverlayWindow : Window
    {
        #region Fields

        /// <summary>
        /// 所属插件 ID
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// 当前显示的方向
        /// </summary>
        private Direction? _currentDirection;

        /// <summary>
        /// 自动隐藏定时器
        /// </summary>
        private DispatcherTimer? _hideTimer;

        /// <summary>
        /// 方向标记元素映射
        /// </summary>
        private readonly Dictionary<Direction, System.Windows.Shapes.Path> _markers;

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        private bool _isEditMode;

        /// <summary>
        /// 拖拽起始点
        /// </summary>
        private Point _dragStartPoint;

        /// <summary>
        /// 拖拽起始窗口位置
        /// </summary>
        private Point _dragStartWindowPos;

        /// <summary>
        /// 当前调整大小的方向
        /// </summary>
        private Win32Helper.ResizeDirection _resizeDirection;

        /// <summary>
        /// 调整大小起始窗口矩形
        /// </summary>
        private Rect _resizeStartRect;

        /// <summary>
        /// 绘图元素 ID 计数器
        /// </summary>
        private int _elementIdCounter;

        /// <summary>
        /// 绘图元素映射（ID -> UIElement）
        /// </summary>
        private readonly Dictionary<string, UIElement> _drawingElements = new();

        /// <summary>
        /// 元素自动隐藏定时器映射
        /// </summary>
        private readonly Dictionary<string, DispatcherTimer> _elementTimers = new();

        #endregion

        #region Events

        /// <summary>
        /// 编辑模式退出事件
        /// </summary>
        public event EventHandler? EditModeExited;

        #endregion

        #region Constructor

        public OverlayWindow(string pluginId)
        {
            InitializeComponent();
            PluginId = pluginId;

            // 初始化方向标记映射
            _markers = new Dictionary<Direction, System.Windows.Shapes.Path>
            {
                { Direction.North, MarkerNorth },
                { Direction.NorthEast, MarkerNorthEast },
                { Direction.East, MarkerEast },
                { Direction.SouthEast, MarkerSouthEast },
                { Direction.South, MarkerSouth },
                { Direction.SouthWest, MarkerSouthWest },
                { Direction.West, MarkerWest },
                { Direction.NorthWest, MarkerNorthWest }
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        public bool IsEditMode => _isEditMode;

        #endregion

        #region Window Events

        /// <summary>
        /// 窗口源初始化完成 - 设置鼠标穿透和隐藏 Alt+Tab
        /// </summary>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                const int GWL_EXSTYLE = -20;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_TRANSPARENT = 0x00000020;

                // 设置 WS_EX_TOOLWINDOW 使窗口不在 Alt+Tab 中显示
                // 设置 WS_EX_TRANSPARENT 使点击可以穿透到下层窗口
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                exStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT;
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            }
        }

        /// <summary>
        /// 仅设置点击穿透，不影响 WPF 透明度渲染
        /// </summary>
        private void SetClickThroughOnly(bool enable)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            const int GWL_EXSTYLE = -20;
            const int WS_EX_TRANSPARENT = 0x00000020;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enable)
            {
                exStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// 键盘按下事件 - ESC 退出编辑模式
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _isEditMode)
            {
                ExitEditMode();
                e.Handled = true;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 显示方向标记
        /// </summary>
        /// <param name="direction">方向</param>
        /// <param name="durationMs">显示时长（毫秒），0 表示常驻</param>
        public void ShowDirectionMarker(Direction direction, int durationMs = 0)
        {
            Services.LogService.Instance.Debug("OverlayWindow", $"ShowDirectionMarker: {direction}, duration={durationMs}, IsVisible={IsVisible}, Left={Left}, Top={Top}, Width={Width}, Height={Height}");

            // 停止之前的定时器
            StopHideTimer();

            // 隐藏所有标记
            HideAllMarkers();

            // 显示指定方向的标记
            if (_markers.TryGetValue(direction, out var marker))
            {
                marker.Visibility = Visibility.Visible;
                _currentDirection = direction;
                Services.LogService.Instance.Debug("OverlayWindow", $"Marker {direction} set to Visible");
            }
            else
            {
                Services.LogService.Instance.Warn("OverlayWindow", $"Marker {direction} not found in _markers dictionary");
            }

            // 如果指定了时长，设置定时隐藏
            if (durationMs > 0)
            {
                StartHideTimer(durationMs);
            }
        }

        /// <summary>
        /// 清除所有方向标记
        /// </summary>
        public void ClearMarkers()
        {
            StopHideTimer();
            HideAllMarkers();
            _currentDirection = null;
        }

        /// <summary>
        /// 设置窗口位置（逻辑像素）
        /// </summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        public void SetPosition(double x, double y)
        {
            Left = x;
            Top = y;
        }

        /// <summary>
        /// 设置窗口大小（逻辑像素）
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public void SetSize(double width, double height)
        {
            Width = width;
            Height = height;
            UpdateMarkerPositions();
        }

        /// <summary>
        /// 获取窗口矩形信息（逻辑像素）
        /// </summary>
        /// <returns>位置和大小</returns>
        public (double X, double Y, double Width, double Height) GetRect()
        {
            return (Left, Top, Width, Height);
        }

        /// <summary>
        /// 进入编辑模式
        /// </summary>
        public void EnterEditMode()
        {
            if (_isEditMode)
                return;

            _isEditMode = true;

            // 禁用鼠标穿透
            SetClickThroughOnly(false);

            // 显示编辑模式 UI
            EditBorder.Visibility = Visibility.Visible;
            ResizeHandlesCanvas.Visibility = Visibility.Visible;
            DoneButton.Visibility = Visibility.Visible;

            // 显示所有控制点
            ShowResizeHandles();
            UpdateResizeHandlePositions();

            // 设置窗口可拖拽
            MouseLeftButtonDown += Window_MouseLeftButtonDown;
            MouseMove += Window_MouseMove;
            MouseLeftButtonUp += Window_MouseLeftButtonUp;

            // 聚焦窗口以接收键盘事件
            Focus();
        }

        /// <summary>
        /// 退出编辑模式
        /// </summary>
        public void ExitEditMode()
        {
            if (!_isEditMode)
                return;

            _isEditMode = false;

            // 启用鼠标穿透
            SetClickThroughOnly(true);

            // 隐藏编辑模式 UI
            EditBorder.Visibility = Visibility.Collapsed;
            ResizeHandlesCanvas.Visibility = Visibility.Collapsed;
            DoneButton.Visibility = Visibility.Collapsed;

            // 隐藏所有控制点
            HideResizeHandles();

            // 移除拖拽事件
            MouseLeftButtonDown -= Window_MouseLeftButtonDown;
            MouseMove -= Window_MouseMove;
            MouseLeftButtonUp -= Window_MouseLeftButtonUp;

            // 触发退出事件
            EditModeExited?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Drawing Methods

        /// <summary>
        /// 绘制文本
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        /// <param name="options">样式选项</param>
        /// <returns>元素 ID</returns>
        public string DrawText(string text, double x, double y, DrawTextOptions? options = null)
        {
            options ??= new DrawTextOptions();
            var elementId = GenerateElementId();

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = Math.Max(1, options.FontSize),
                FontFamily = new FontFamily(options.FontFamily ?? "Microsoft YaHei"),
                Foreground = ParseBrush(options.Color, Brushes.White),
                Opacity = Math.Clamp(options.Opacity, 0, 1)
            };

            // 设置背景
            if (!string.IsNullOrEmpty(options.BackgroundColor))
            {
                var border = new Border
                {
                    Background = ParseBrush(options.BackgroundColor, Brushes.Transparent),
                    Padding = new Thickness(4, 2, 4, 2),
                    Child = textBlock
                };
                
                Canvas.SetLeft(border, Math.Max(0, x));
                Canvas.SetTop(border, Math.Max(0, y));
                DrawingCanvas.Children.Add(border);
                _drawingElements[elementId] = border;
            }
            else
            {
                Canvas.SetLeft(textBlock, Math.Max(0, x));
                Canvas.SetTop(textBlock, Math.Max(0, y));
                DrawingCanvas.Children.Add(textBlock);
                _drawingElements[elementId] = textBlock;
            }

            // 设置自动隐藏
            if (options.Duration > 0)
            {
                StartElementTimer(elementId, options.Duration);
            }

            return elementId;
        }

        /// <summary>
        /// 绘制矩形
        /// </summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="options">样式选项</param>
        /// <returns>元素 ID</returns>
        public string DrawRect(double x, double y, double width, double height, DrawRectOptions? options = null)
        {
            options ??= new DrawRectOptions();
            var elementId = GenerateElementId();

            var rect = new Rectangle
            {
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
            _drawingElements[elementId] = rect;

            // 设置自动隐藏
            if (options.Duration > 0)
            {
                StartElementTimer(elementId, options.Duration);
            }

            return elementId;
        }

        /// <summary>
        /// 绘制图片
        /// </summary>
        /// <param name="path">图片路径</param>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        /// <param name="options">样式选项</param>
        /// <returns>元素 ID，失败返回空字符串</returns>
        public string DrawImage(string path, double x, double y, DrawImageOptions? options = null)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Services.LogService.Instance.Error("OverlayWindow", $"DrawImage: Image file not found: {path}");
                return string.Empty;
            }

            options ??= new DrawImageOptions();
            var elementId = GenerateElementId();

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                var image = new Image
                {
                    Source = bitmap,
                    Opacity = Math.Clamp(options.Opacity, 0, 1),
                    Stretch = Stretch.Fill
                };

                if (options.Width.HasValue)
                    image.Width = Math.Max(0, options.Width.Value);
                if (options.Height.HasValue)
                    image.Height = Math.Max(0, options.Height.Value);

                Canvas.SetLeft(image, Math.Max(0, x));
                Canvas.SetTop(image, Math.Max(0, y));
                DrawingCanvas.Children.Add(image);
                _drawingElements[elementId] = image;

                // 设置自动隐藏
                if (options.Duration > 0)
                {
                    StartElementTimer(elementId, options.Duration);
                }

                return elementId;
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error("OverlayWindow", $"DrawImage failed: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 移除指定绘图元素
        /// </summary>
        /// <param name="elementId">元素 ID</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveElement(string elementId)
        {
            if (string.IsNullOrEmpty(elementId))
                return false;

            // 停止定时器
            StopElementTimer(elementId);

            if (_drawingElements.TryGetValue(elementId, out var element))
            {
                DrawingCanvas.Children.Remove(element);
                _drawingElements.Remove(elementId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清除所有绘图元素
        /// </summary>
        public void ClearDrawingElements()
        {
            // 停止所有定时器
            foreach (var timer in _elementTimers.Values)
            {
                timer.Stop();
            }
            _elementTimers.Clear();

            // 移除所有元素
            foreach (var element in _drawingElements.Values)
            {
                DrawingCanvas.Children.Remove(element);
            }
            _drawingElements.Clear();
        }

        /// <summary>
        /// 获取所有绘图元素 ID
        /// </summary>
        /// <returns>元素 ID 列表</returns>
        public IReadOnlyCollection<string> GetDrawingElementIds()
        {
            return _drawingElements.Keys.ToList().AsReadOnly();
        }

        #endregion

        #region Drawing Helper Methods

        /// <summary>
        /// 生成唯一元素 ID
        /// </summary>
        private string GenerateElementId()
        {
            return $"{PluginId}_elem_{++_elementIdCounter}_{DateTime.UtcNow.Ticks}";
        }

        /// <summary>
        /// 解析颜色字符串为 Brush
        /// </summary>
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

        /// <summary>
        /// 启动元素自动隐藏定时器
        /// </summary>
        private void StartElementTimer(string elementId, int durationMs)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                RemoveElement(elementId);
            };
            timer.Start();
            _elementTimers[elementId] = timer;
        }

        /// <summary>
        /// 停止元素定时器
        /// </summary>
        private void StopElementTimer(string elementId)
        {
            if (_elementTimers.TryGetValue(elementId, out var timer))
            {
                timer.Stop();
                _elementTimers.Remove(elementId);
            }
        }

        #endregion

        #region Edit Mode Event Handlers

        /// <summary>
        /// 完成按钮点击
        /// </summary>
        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        /// <summary>
        /// 窗口鼠标按下 - 开始拖拽
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode)
                return;

            // 检查是否点击在控制点或按钮上
            if (e.OriginalSource is Rectangle || e.OriginalSource is Button)
                return;

            _dragStartPoint = e.GetPosition(this);
            _dragStartWindowPos = new Point(Left, Top);
            _resizeDirection = Win32Helper.ResizeDirection.None;
            CaptureMouse();
            e.Handled = true;
        }

        /// <summary>
        /// 窗口鼠标移动 - 拖拽移动或调整大小
        /// </summary>
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEditMode || e.LeftButton != MouseButtonState.Pressed)
                return;

            if (!IsMouseCaptured)
                return;

            var currentPos = e.GetPosition(this);
            var delta = currentPos - _dragStartPoint;

            if (_resizeDirection == Win32Helper.ResizeDirection.None)
            {
                // 拖拽移动
                Left = _dragStartWindowPos.X + delta.X;
                Top = _dragStartWindowPos.Y + delta.Y;
            }
            else
            {
                // 调整大小
                ResizeWindow(delta);
            }
        }

        /// <summary>
        /// 窗口鼠标释放 - 结束拖拽
        /// </summary>
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                UpdateResizeHandlePositions();
            }
        }

        /// <summary>
        /// 控制点鼠标按下 - 开始调整大小
        /// </summary>
        private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode || sender is not Rectangle handle)
                return;

            _resizeDirection = GetResizeDirectionFromHandle(handle);
            _dragStartPoint = e.GetPosition(this);
            _resizeStartRect = new Rect(Left, Top, Width, Height);
            CaptureMouse();
            e.Handled = true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 隐藏所有方向标记
        /// </summary>
        private void HideAllMarkers()
        {
            foreach (var marker in _markers.Values)
            {
                marker.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 更新标记位置（窗口大小变化时调用）
        /// </summary>
        private void UpdateMarkerPositions()
        {
            double centerX = Width / 2 - 12;  // 箭头宽度约 24
            double centerY = Height / 2 - 12; // 箭头高度约 24

            // 更新各方向标记的位置
            System.Windows.Controls.Canvas.SetLeft(MarkerNorth, centerX);
            System.Windows.Controls.Canvas.SetTop(MarkerNorth, 5);

            System.Windows.Controls.Canvas.SetLeft(MarkerSouth, centerX);
            System.Windows.Controls.Canvas.SetBottom(MarkerSouth, 5);

            System.Windows.Controls.Canvas.SetLeft(MarkerWest, 5);
            System.Windows.Controls.Canvas.SetTop(MarkerWest, centerY);

            System.Windows.Controls.Canvas.SetLeft(MarkerEast, Width - 20);
            System.Windows.Controls.Canvas.SetTop(MarkerEast, centerY);
        }

        /// <summary>
        /// 启动自动隐藏定时器
        /// </summary>
        /// <param name="durationMs">延迟时间（毫秒）</param>
        private void StartHideTimer(int durationMs)
        {
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            _hideTimer.Tick += HideTimer_Tick;
            _hideTimer.Start();
        }

        /// <summary>
        /// 停止自动隐藏定时器
        /// </summary>
        private void StopHideTimer()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer.Tick -= HideTimer_Tick;
                _hideTimer = null;
            }
        }

        /// <summary>
        /// 定时器回调：隐藏标记
        /// </summary>
        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            StopHideTimer();
            HideAllMarkers();
            _currentDirection = null;
        }

        /// <summary>
        /// 显示所有调整大小控制点
        /// </summary>
        private void ShowResizeHandles()
        {
            HandleTopLeft.Visibility = Visibility.Visible;
            HandleTopRight.Visibility = Visibility.Visible;
            HandleBottomLeft.Visibility = Visibility.Visible;
            HandleBottomRight.Visibility = Visibility.Visible;
            HandleTop.Visibility = Visibility.Visible;
            HandleBottom.Visibility = Visibility.Visible;
            HandleLeft.Visibility = Visibility.Visible;
            HandleRight.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 隐藏所有调整大小控制点
        /// </summary>
        private void HideResizeHandles()
        {
            HandleTopLeft.Visibility = Visibility.Collapsed;
            HandleTopRight.Visibility = Visibility.Collapsed;
            HandleBottomLeft.Visibility = Visibility.Collapsed;
            HandleBottomRight.Visibility = Visibility.Collapsed;
            HandleTop.Visibility = Visibility.Collapsed;
            HandleBottom.Visibility = Visibility.Collapsed;
            HandleLeft.Visibility = Visibility.Collapsed;
            HandleRight.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 更新控制点位置
        /// </summary>
        private void UpdateResizeHandlePositions()
        {
            double w = ActualWidth;
            double h = ActualHeight;
            double handleSize = 10;
            double halfHandle = handleSize / 2;

            // 四角
            Canvas.SetLeft(HandleTopLeft, -halfHandle);
            Canvas.SetTop(HandleTopLeft, -halfHandle);

            Canvas.SetLeft(HandleTopRight, w - halfHandle);
            Canvas.SetTop(HandleTopRight, -halfHandle);

            Canvas.SetLeft(HandleBottomLeft, -halfHandle);
            Canvas.SetTop(HandleBottomLeft, h - halfHandle);

            Canvas.SetLeft(HandleBottomRight, w - halfHandle);
            Canvas.SetTop(HandleBottomRight, h - halfHandle);

            // 四边中点
            Canvas.SetLeft(HandleTop, w / 2 - halfHandle);
            Canvas.SetTop(HandleTop, -halfHandle);

            Canvas.SetLeft(HandleBottom, w / 2 - halfHandle);
            Canvas.SetTop(HandleBottom, h - halfHandle);

            Canvas.SetLeft(HandleLeft, -halfHandle);
            Canvas.SetTop(HandleLeft, h / 2 - halfHandle);

            Canvas.SetLeft(HandleRight, w - halfHandle);
            Canvas.SetTop(HandleRight, h / 2 - halfHandle);
        }

        /// <summary>
        /// 根据控制点获取调整方向
        /// </summary>
        private Win32Helper.ResizeDirection GetResizeDirectionFromHandle(Rectangle handle)
        {
            if (handle == HandleTopLeft) return Win32Helper.ResizeDirection.TopLeft;
            if (handle == HandleTopRight) return Win32Helper.ResizeDirection.TopRight;
            if (handle == HandleBottomLeft) return Win32Helper.ResizeDirection.BottomLeft;
            if (handle == HandleBottomRight) return Win32Helper.ResizeDirection.BottomRight;
            if (handle == HandleTop) return Win32Helper.ResizeDirection.Top;
            if (handle == HandleBottom) return Win32Helper.ResizeDirection.Bottom;
            if (handle == HandleLeft) return Win32Helper.ResizeDirection.Left;
            if (handle == HandleRight) return Win32Helper.ResizeDirection.Right;
            return Win32Helper.ResizeDirection.None;
        }

        /// <summary>
        /// 调整窗口大小
        /// </summary>
        private void ResizeWindow(Vector delta)
        {
            const double minSize = 50;
            double newLeft = _resizeStartRect.Left;
            double newTop = _resizeStartRect.Top;
            double newWidth = _resizeStartRect.Width;
            double newHeight = _resizeStartRect.Height;

            // 根据方向调整
            switch (_resizeDirection)
            {
                case Win32Helper.ResizeDirection.TopLeft:
                    newLeft = _resizeStartRect.Left + delta.X;
                    newTop = _resizeStartRect.Top + delta.Y;
                    newWidth = _resizeStartRect.Width - delta.X;
                    newHeight = _resizeStartRect.Height - delta.Y;
                    break;
                case Win32Helper.ResizeDirection.TopRight:
                    newTop = _resizeStartRect.Top + delta.Y;
                    newWidth = _resizeStartRect.Width + delta.X;
                    newHeight = _resizeStartRect.Height - delta.Y;
                    break;
                case Win32Helper.ResizeDirection.BottomLeft:
                    newLeft = _resizeStartRect.Left + delta.X;
                    newWidth = _resizeStartRect.Width - delta.X;
                    newHeight = _resizeStartRect.Height + delta.Y;
                    break;
                case Win32Helper.ResizeDirection.BottomRight:
                    newWidth = _resizeStartRect.Width + delta.X;
                    newHeight = _resizeStartRect.Height + delta.Y;
                    break;
                case Win32Helper.ResizeDirection.Top:
                    newTop = _resizeStartRect.Top + delta.Y;
                    newHeight = _resizeStartRect.Height - delta.Y;
                    break;
                case Win32Helper.ResizeDirection.Bottom:
                    newHeight = _resizeStartRect.Height + delta.Y;
                    break;
                case Win32Helper.ResizeDirection.Left:
                    newLeft = _resizeStartRect.Left + delta.X;
                    newWidth = _resizeStartRect.Width - delta.X;
                    break;
                case Win32Helper.ResizeDirection.Right:
                    newWidth = _resizeStartRect.Width + delta.X;
                    break;
            }

            // 应用最小尺寸限制
            if (newWidth >= minSize && newHeight >= minSize)
            {
                Left = newLeft;
                Top = newTop;
                Width = newWidth;
                Height = newHeight;
                UpdateMarkerPositions();
                UpdateResizeHandlePositions();
            }
        }

        #endregion
    }

    /// <summary>
    /// 方向枚举
    /// </summary>
    public enum Direction
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest
    }

    /// <summary>
    /// 绘制文本选项
    /// </summary>
    public class DrawTextOptions
    {
        /// <summary>
        /// 字体大小（默认 16）
        /// </summary>
        public double FontSize { get; set; } = 16;

        /// <summary>
        /// 字体名称（默认 Microsoft YaHei）
        /// </summary>
        public string? FontFamily { get; set; } = "Microsoft YaHei";

        /// <summary>
        /// 文本颜色（默认白色）
        /// </summary>
        public string? Color { get; set; } = "#FFFFFF";

        /// <summary>
        /// 背景颜色（可选）
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// 透明度（0-1，默认 1）
        /// </summary>
        public double Opacity { get; set; } = 1.0;

        /// <summary>
        /// 显示时长（毫秒，0 = 永久）
        /// </summary>
        public int Duration { get; set; } = 0;
    }

    /// <summary>
    /// 绘制矩形选项
    /// </summary>
    public class DrawRectOptions
    {
        /// <summary>
        /// 填充颜色
        /// </summary>
        public string? Fill { get; set; }

        /// <summary>
        /// 边框颜色
        /// </summary>
        public string? Stroke { get; set; }

        /// <summary>
        /// 边框宽度（默认 1）
        /// </summary>
        public double StrokeWidth { get; set; } = 1;

        /// <summary>
        /// 透明度（0-1，默认 1）
        /// </summary>
        public double Opacity { get; set; } = 1.0;

        /// <summary>
        /// 圆角半径（默认 0）
        /// </summary>
        public double CornerRadius { get; set; } = 0;

        /// <summary>
        /// 显示时长（毫秒，0 = 永久）
        /// </summary>
        public int Duration { get; set; } = 0;
    }

    /// <summary>
    /// 绘制图片选项
    /// </summary>
    public class DrawImageOptions
    {
        /// <summary>
        /// 宽度（可选，不指定则使用原始宽度）
        /// </summary>
        public double? Width { get; set; }

        /// <summary>
        /// 高度（可选，不指定则使用原始高度）
        /// </summary>
        public double? Height { get; set; }

        /// <summary>
        /// 透明度（0-1，默认 1）
        /// </summary>
        public double Opacity { get; set; } = 1.0;

        /// <summary>
        /// 显示时长（毫秒，0 = 永久）
        /// </summary>
        public int Duration { get; set; } = 0;
    }
}
