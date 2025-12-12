using System;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 窗口状态服务
    /// 负责保存和加载窗口位置、大小、最后访问 URL 等
    /// </summary>
    public class WindowStateService
    {
        #region Singleton

        private static WindowStateService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static WindowStateService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new WindowStateService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private readonly JsonSerializerOptions _jsonOptions;
        private WindowState? _cachedState;

        #endregion

        #region Constructor

        private WindowStateService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 加载窗口状态
        /// </summary>
        public WindowState Load()
        {
            if (_cachedState != null)
                return _cachedState;

            var filePath = GetFilePath();
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    _cachedState = JsonSerializer.Deserialize<WindowState>(json, _jsonOptions);
                }
                catch
                {
                    _cachedState = null;
                }
            }

            // 返回默认状态
            if (_cachedState == null)
            {
                _cachedState = CreateDefaultState();
            }

            return _cachedState;
        }

        /// <summary>
        /// 保存窗口状态
        /// </summary>
        public void Save(WindowState state)
        {
            _cachedState = state;

            try
            {
                var filePath = GetFilePath();
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(state, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }

        /// <summary>
        /// 更新并保存窗口状态
        /// </summary>
        public void Update(Action<WindowState> updateAction)
        {
            var state = Load();
            updateAction(state);
            Save(state);
        }

        #endregion

        #region Private Methods

        private string GetFilePath()
        {
            return Path.Combine(ProfileManager.Instance.GetCurrentProfileDirectory(), "window_state.json");
        }

        private WindowState CreateDefaultState()
        {
            // 获取主屏幕工作区域
            var workArea = System.Windows.SystemParameters.WorkArea;

            // 计算默认大小：宽度为屏幕的 1/4，高度按 16:9 比例计算
            double defaultWidth = Math.Max(workArea.Width / 4, AppConstants.MinWindowWidth);
            double defaultHeight = defaultWidth * 9 / 16;

            if (defaultHeight < AppConstants.MinWindowHeight)
            {
                defaultHeight = AppConstants.MinWindowHeight;
                defaultWidth = defaultHeight * 16 / 9;
            }

            return new WindowState
            {
                Left = workArea.Left,
                Top = workArea.Bottom - defaultHeight,
                Width = defaultWidth,
                Height = defaultHeight,
                Opacity = AppConstants.MaxOpacity,
                IsMaximized = false,
                LastUrl = AppConstants.DefaultHomeUrl,
                IsMuted = false
            };
        }

        #endregion
    }
}
