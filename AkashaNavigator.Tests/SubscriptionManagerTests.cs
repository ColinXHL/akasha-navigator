using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests
{
    /// <summary>
    /// SubscriptionManager 单元测试
    /// </summary>
    public class SubscriptionManagerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _dataDir;
        private readonly string _profilesDir;
        private readonly string _builtInProfilesDir;
        private readonly string _builtInPluginsDir;
        private readonly string _subscriptionsFilePath;
        private readonly ILogService _logService;
        private readonly ProfileRegistry _profileRegistry;
        private readonly PluginRegistry _pluginRegistry;

        public SubscriptionManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"subscription_manager_test_{Guid.NewGuid()}");
            _dataDir = Path.Combine(_tempDir, "Data");
            _profilesDir = Path.Combine(_dataDir, "Profiles");
            _builtInProfilesDir = Path.Combine(_tempDir, "BuiltInProfiles");
            _builtInPluginsDir = Path.Combine(_tempDir, "BuiltInPlugins");
            _subscriptionsFilePath = Path.Combine(_dataDir, "subscriptions.json");

            Directory.CreateDirectory(_dataDir);
            Directory.CreateDirectory(_profilesDir);
            Directory.CreateDirectory(_builtInProfilesDir);
            Directory.CreateDirectory(_builtInPluginsDir);

            // 初始化日志服务
            _logService = new LogService();

            // 初始化 ProfileRegistry 和 PluginRegistry
            _profileRegistry = new ProfileRegistry(_builtInProfilesDir, _logService);

            _pluginRegistry = new PluginRegistry(_builtInPluginsDir, _logService);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }


        #region Helper Methods

        /// <summary>
        /// 创建测试用的订阅配置文件
        /// </summary>
        private void CreateSubscriptionsFile(object data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(_subscriptionsFilePath, json);
        }

        /// <summary>
        /// 创建内置 Profile 注册表
        /// </summary>
        private void CreateProfileRegistry(object data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(Path.Combine(_builtInProfilesDir, "registry.json"), json);
        }

        /// <summary>
        /// 创建内置插件注册表
        /// </summary>
        private void CreatePluginRegistry(object data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(Path.Combine(_builtInPluginsDir, "registry.json"), json);
        }

        /// <summary>
        /// 创建内置 Profile 模板目录
        /// </summary>
        private void CreateProfileTemplate(string profileId, object profileData)
        {
            var profileDir = Path.Combine(_builtInProfilesDir, profileId);
            Directory.CreateDirectory(profileDir);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(profileData, options);
            File.WriteAllText(Path.Combine(profileDir, "profile.json"), json);
        }

        /// <summary>
        /// 创建标准测试数据
        /// </summary>
        private void SetupStandardTestData()
        {
            // 创建内置 Profile 注册表
            CreateProfileRegistry(new
            {
                version = 1,
                profiles = new[]
                {
                    new
                    {
                        id = "default",
                        name = "默认",
                        icon = "📺",
                        description = "通用配置",
                        recommendedPlugins = Array.Empty<string>()
                    },
                    new
                    {
                        id = "genshin",
                        name = "原神",
                        icon = "🎮",
                        description = "原神游戏配置",
                        recommendedPlugins = new[] { "genshin-direction-marker" }
                    }
                }
            });

            // 创建内置 Profile 模板
            CreateProfileTemplate("default", new
            {
                id = "default",
                name = "默认",
                icon = "📺",
                version = 1
            });

            CreateProfileTemplate("genshin", new
            {
                id = "genshin",
                name = "原神",
                icon = "🎮",
                version = 1,
                recommendedPlugins = new[] { "genshin-direction-marker" }
            });

            // 创建内置插件注册表
            CreatePluginRegistry(new
            {
                version = 1,
                plugins = new[]
                {
                    new
                    {
                        id = "genshin-direction-marker",
                        name = "原神方向标记",
                        version = "1.0.0",
                        description = "方向标记插件",
                        permissions = new[] { "subtitle", "overlay" },
                        profiles = new[] { "genshin" }
                    }
                }
            });
        }

        /// <summary>
        /// 创建 SubscriptionManager 实例（使用测试目录）
        /// </summary>
        private SubscriptionManager CreateManager()
        {
            return new SubscriptionManager(_logService, _profileRegistry, _pluginRegistry,
                                          _subscriptionsFilePath, _profilesDir);
        }

        #endregion


        #region Load/Save Tests

        /// <summary>
        /// Load 应该加载现有的订阅配置
        /// </summary>
        [Fact]
        public void Load_ShouldLoadExistingConfig()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "default", "genshin" },
                pluginSubscriptions = new Dictionary<string, string[]>
                {
                    { "genshin", new[] { "genshin-direction-marker" } }
                }
            });
            var manager = CreateManager();

            // Act
            manager.Load();
            var profiles = manager.GetSubscribedProfiles();

            // Assert
            Assert.Equal(2, profiles.Count);
            Assert.Contains("default", profiles);
            Assert.Contains("genshin", profiles);
        }

        /// <summary>
        /// Load 应该在文件不存在时创建空配置
        /// </summary>
        [Fact]
        public void Load_ShouldCreateEmptyConfig_WhenFileNotExists()
        {
            // Arrange
            var manager = CreateManager();

            // Act
            manager.Load();
            var profiles = manager.GetSubscribedProfiles();

            // Assert
            Assert.Empty(profiles);
        }

        /// <summary>
        /// Save 应该持久化配置到文件
        /// </summary>
        [Fact]
        public void Save_ShouldPersistConfigToFile()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "test-profile" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();
            manager.Load();

            // Act
            manager.Save();

            // Assert
            Assert.True(File.Exists(_subscriptionsFilePath));
            var content = File.ReadAllText(_subscriptionsFilePath);
            Assert.Contains("test-profile", content);
        }

        #endregion

        #region Profile Subscription Tests

        /// <summary>
        /// GetSubscribedProfiles 应该返回已订阅的 Profile 列表
        /// </summary>
        [Fact]
        public void GetSubscribedProfiles_ShouldReturnSubscribedProfiles()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "profile1", "profile2" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();

            // Act
            var profiles = manager.GetSubscribedProfiles();

            // Assert
            Assert.Equal(2, profiles.Count);
            Assert.Contains("profile1", profiles);
            Assert.Contains("profile2", profiles);
        }

        /// <summary>
        /// GetSubscribedProfiles 应该返回副本而非原始列表
        /// </summary>
        [Fact]
        public void GetSubscribedProfiles_ShouldReturnCopy()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "profile1" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();

            // Act
            var profiles1 = manager.GetSubscribedProfiles();
            profiles1.Clear();
            var profiles2 = manager.GetSubscribedProfiles();

            // Assert
            Assert.Single(profiles2);
        }

        /// <summary>
        /// IsProfileSubscribed 应该返回正确的订阅状态
        /// </summary>
        [Fact]
        public void IsProfileSubscribed_ShouldReturnCorrectStatus()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "subscribed-profile" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();

            // Act & Assert
            Assert.True(manager.IsProfileSubscribed("subscribed-profile"));
            Assert.False(manager.IsProfileSubscribed("not-subscribed"));
        }

        /// <summary>
        /// IsProfileSubscribed 应该处理空值
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsProfileSubscribed_ShouldReturnFalse_WhenIdIsNullOrEmpty(string? profileId)
        {
            // Arrange
            var manager = CreateManager();

            // Act & Assert
            Assert.False(manager.IsProfileSubscribed(profileId!));
        }

        #endregion


        #region Plugin Subscription Tests

        /// <summary>
        /// GetSubscribedPlugins 应该返回指定 Profile 订阅的插件
        /// </summary>
        [Fact]
        public void GetSubscribedPlugins_ShouldReturnPluginsForProfile()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "genshin" },
                pluginSubscriptions = new Dictionary<string, string[]>
                {
                    { "genshin", new[] { "plugin1", "plugin2" } }
                }
            });
            var manager = CreateManager();

            // Act
            var plugins = manager.GetSubscribedPlugins("genshin");

            // Assert
            Assert.Equal(2, plugins.Count);
            Assert.Contains("plugin1", plugins);
            Assert.Contains("plugin2", plugins);
        }

        /// <summary>
        /// GetSubscribedPlugins 应该返回空列表当 Profile 没有订阅插件时
        /// </summary>
        [Fact]
        public void GetSubscribedPlugins_ShouldReturnEmptyList_WhenNoPlugins()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "default" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();

            // Act
            var plugins = manager.GetSubscribedPlugins("default");

            // Assert
            Assert.Empty(plugins);
        }

        /// <summary>
        /// GetSubscribedPlugins 应该处理空的 profileId
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetSubscribedPlugins_ShouldReturnEmptyList_WhenProfileIdIsNullOrEmpty(string? profileId)
        {
            // Arrange
            var manager = CreateManager();

            // Act
            var plugins = manager.GetSubscribedPlugins(profileId!);

            // Assert
            Assert.Empty(plugins);
        }

        /// <summary>
        /// IsPluginSubscribed 应该返回正确的订阅状态
        /// </summary>
        [Fact]
        public void IsPluginSubscribed_ShouldReturnCorrectStatus()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "genshin" },
                pluginSubscriptions = new Dictionary<string, string[]>
                {
                    { "genshin", new[] { "subscribed-plugin" } }
                }
            });
            var manager = CreateManager();

            // Act & Assert
            Assert.True(manager.IsPluginSubscribed("subscribed-plugin", "genshin"));
            Assert.False(manager.IsPluginSubscribed("not-subscribed", "genshin"));
            Assert.False(manager.IsPluginSubscribed("subscribed-plugin", "other-profile"));
        }

        /// <summary>
        /// IsPluginSubscribed 应该处理空值
        /// </summary>
        [Theory]
        [InlineData(null, "profile")]
        [InlineData("plugin", null)]
        [InlineData("", "profile")]
        [InlineData("plugin", "")]
        public void IsPluginSubscribed_ShouldReturnFalse_WhenIdsAreNullOrEmpty(string? pluginId, string? profileId)
        {
            // Arrange
            var manager = CreateManager();

            // Act & Assert
            Assert.False(manager.IsPluginSubscribed(pluginId!, profileId!));
        }

        #endregion

        #region GetPluginConfigDirectory Tests

        /// <summary>
        /// GetPluginConfigDirectory 应该返回正确的路径
        /// </summary>
        [Fact]
        public void GetPluginConfigDirectory_ShouldReturnCorrectPath()
        {
            // Arrange
            var manager = CreateManager();

            // Act
            var path = manager.GetPluginConfigDirectory("genshin", "test-plugin");

            // Assert
            var expectedPath = Path.Combine(_profilesDir, "genshin", "plugins", "test-plugin");
            Assert.Equal(expectedPath, path);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// 处理无效的 JSON 文件
        /// </summary>
        [Fact]
        public void Load_ShouldHandleInvalidJson()
        {
            // Arrange
            File.WriteAllText(_subscriptionsFilePath, "invalid json");
            var manager = CreateManager();

            // Act
            manager.Load();
            var profiles = manager.GetSubscribedProfiles();

            // Assert
            Assert.Empty(profiles);
        }

        /// <summary>
        /// EnsureLoaded 应该只加载一次（通过 GetSubscribedProfiles 间接测试）
        /// </summary>
        [Fact]
        public void EnsureLoaded_ShouldOnlyLoadOnce_WhenCalledMultipleTimes()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "profile1" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();

            // Act - 首次调用会触发 EnsureLoaded
            var profiles1 = manager.GetSubscribedProfiles();
            Assert.Single(profiles1);
            
            // 修改文件
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "profile1", "profile2" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            
            // 再次调用（EnsureLoaded 不会重新加载，因为已经加载过）
            var profiles2 = manager.GetSubscribedProfiles();

            // Assert - 应该还是只有一个 profile（因为没有重新加载）
            Assert.Single(profiles2);
        }

        /// <summary>
        /// 显式调用 Load 应该重新加载配置
        /// </summary>
        [Fact]
        public void Load_ShouldReloadConfig_WhenCalledExplicitly()
        {
            // Arrange
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "profile1" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            var manager = CreateManager();
            manager.Load();
            
            // 修改文件
            CreateSubscriptionsFile(new
            {
                version = 1,
                profiles = new[] { "profile1", "profile2" },
                pluginSubscriptions = new Dictionary<string, string[]>()
            });
            
            // Act - 显式调用 Load 应该重新加载
            manager.Load();
            var profiles = manager.GetSubscribedProfiles();

            // Assert - 应该有两个 profile
            Assert.Equal(2, profiles.Count);
        }

        #endregion
    }
}
