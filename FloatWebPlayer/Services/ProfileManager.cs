using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// Profile 管理服务
    /// 负责加载、切换、保存 Profile 配置
    /// </summary>
    public class ProfileManager
    {
        #region Singleton

        private static ProfileManager? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ProfileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ProfileManager();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Profile 切换事件
        /// </summary>
        public event EventHandler<GameProfile>? ProfileChanged;

        #endregion

        #region Properties

        /// <summary>
        /// 当前激活的 Profile
        /// </summary>
        public GameProfile CurrentProfile { get; private set; }

        /// <summary>
        /// 所有已加载的 Profile 列表
        /// </summary>
        public List<GameProfile> Profiles { get; } = new();

        /// <summary>
        /// 数据根目录
        /// </summary>
        public string DataDirectory { get; }

        /// <summary>
        /// Profiles 目录
        /// </summary>
        public string ProfilesDirectory { get; }

        #endregion

        #region Constructor

        private ProfileManager()
        {
            // 数据目录：AppData/Local/FloatWebPlayer/Data
            DataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FloatWebPlayer",
                "Data"
            );
            ProfilesDirectory = Path.Combine(DataDirectory, "Profiles");

            // 确保目录存在
            EnsureDirectoriesExist();

            // 加载所有 Profile
            LoadAllProfiles();

            // 设置默认 Profile
            CurrentProfile = GetProfileById("default") ?? CreateDefaultProfile();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 切换到指定 Profile
        /// </summary>
        public bool SwitchProfile(string profileId)
        {
            var profile = GetProfileById(profileId);
            if (profile == null)
                return false;

            CurrentProfile = profile;
            ProfileChanged?.Invoke(this, profile);
            return true;
        }

        /// <summary>
        /// 根据 ID 获取 Profile
        /// </summary>
        public GameProfile? GetProfileById(string id)
        {
            return Profiles.Find(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取当前 Profile 的数据目录
        /// </summary>
        public string GetCurrentProfileDirectory()
        {
            return GetProfileDirectory(CurrentProfile.Id);
        }

        /// <summary>
        /// 获取指定 Profile 的数据目录
        /// </summary>
        public string GetProfileDirectory(string profileId)
        {
            return Path.Combine(ProfilesDirectory, profileId);
        }

        /// <summary>
        /// 保存当前 Profile 配置
        /// </summary>
        public void SaveCurrentProfile()
        {
            SaveProfile(CurrentProfile);
        }

        /// <summary>
        /// 保存指定 Profile 配置
        /// </summary>
        public void SaveProfile(GameProfile profile)
        {
            var profileDir = GetProfileDirectory(profile.Id);
            Directory.CreateDirectory(profileDir);

            var profilePath = Path.Combine(profileDir, "profile.json");
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(profile, options);
            File.WriteAllText(profilePath, json);
        }

        /// <summary>
        /// 重新加载所有 Profile
        /// </summary>
        public void ReloadProfiles()
        {
            Profiles.Clear();
            LoadAllProfiles();
            
            // 如果当前 Profile 不存在，切换到 Default
            if (GetProfileById(CurrentProfile.Id) == null)
            {
                CurrentProfile = GetProfileById("default") ?? CreateDefaultProfile();
                ProfileChanged?.Invoke(this, CurrentProfile);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 确保必要目录存在
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(ProfilesDirectory);
        }

        /// <summary>
        /// 加载所有 Profile
        /// </summary>
        private void LoadAllProfiles()
        {
            if (!Directory.Exists(ProfilesDirectory))
                return;

            var profileDirs = Directory.GetDirectories(ProfilesDirectory);
            foreach (var dir in profileDirs)
            {
                var profilePath = Path.Combine(dir, "profile.json");
                if (File.Exists(profilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(profilePath);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true
                        };
                        var profile = JsonSerializer.Deserialize<GameProfile>(json, options);
                        if (profile != null)
                        {
                            Profiles.Add(profile);
                        }
                    }
                    catch
                    {
                        // 跳过无效的 Profile
                    }
                }
            }

            // 如果没有 Default Profile，创建一个
            if (GetProfileById("default") == null)
            {
                var defaultProfile = CreateDefaultProfile();
                Profiles.Add(defaultProfile);
            }
        }

        /// <summary>
        /// 创建默认 Profile
        /// </summary>
        private GameProfile CreateDefaultProfile()
        {
            var profile = new GameProfile
            {
                Id = "default",
                Name = "Default",
                Icon = "🌐",
                Version = 1,
                Defaults = new ProfileDefaults
                {
                    Url = AppConstants.DefaultHomeUrl,
                    Opacity = 1.0,
                    SeekSeconds = AppConstants.DefaultSeekSeconds
                }
            };

            // 保存到文件
            SaveProfile(profile);
            return profile;
        }

        #endregion
    }
}
