using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// PluginApi 属性测试
    /// </summary>
    public class PluginApiTests : IDisposable
    {
        private readonly string _tempDir;

        public PluginApiTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"plugin_api_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
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

        /// <summary>
        /// 创建测试用的插件清单
        /// </summary>
        private PluginManifest CreateTestManifest(string id = "test-plugin", List<string>? permissions = null)
        {
            return new PluginManifest
            {
                Id = id,
                Name = "Test Plugin",
                Version = "1.0.0",
                Main = "main.js",
                Permissions = permissions
            };
        }

        /// <summary>
        /// 创建测试用的 JavaScript 文件
        /// </summary>
        private void CreateJsFile(string content)
        {
            File.WriteAllText(Path.Combine(_tempDir, "main.js"), content);
        }

        /// <summary>
        /// 创建测试用的 PluginApi
        /// </summary>
        private PluginApi CreatePluginApi(List<string>? permissions = null)
        {
            var manifest = CreateTestManifest(permissions: permissions);
            CreateJsFile("function onLoad() {} function onUnload() {}");
            
            var context = new PluginContext(manifest, _tempDir);
            var config = new PluginConfig("test-plugin");
            var profileInfo = new ProfileInfo("test-profile", "Test Profile", _tempDir);

            return new PluginApi(context, config, profileInfo);
        }


        #region Property 1: 沙箱安全性

        /// <summary>
        /// **Feature: game-plugin-system, Property 1: 沙箱安全性**
        /// *对于任意*插件代码，如果它尝试访问未在 permissions 中声明的 API，则该调用应抛出权限错误
        /// **Validates: Requirements 1.2, 7.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property UnauthorizedApiAccess_ShouldReturnNull(PositiveInt permIndex)
        {
            // 所有可能的权限
            var allPermissions = new[] { "audio", "overlay", "network", "storage" };
            
            // 随机选择一个权限作为"未授权"的权限
            var unauthorizedPermission = allPermissions[permIndex.Get % allPermissions.Length];
            
            // 创建一个不包含该权限的权限列表
            var grantedPermissions = new List<string>();
            foreach (var perm in allPermissions)
            {
                if (perm != unauthorizedPermission)
                {
                    grantedPermissions.Add(perm);
                }
            }

            var api = CreatePluginApi(grantedPermissions);

            // 验证未授权的权限
            var hasUnauthorizedPermission = api.HasPermission(unauthorizedPermission);
            
            // 验证已授权的权限
            var allGrantedPermissionsValid = grantedPermissions.TrueForAll(p => api.HasPermission(p));

            // 尝试访问未授权的 API 应该返回 null
            bool unauthorizedApiReturnsNull = unauthorizedPermission switch
            {
                "audio" => api.Speech == null,
                "overlay" => api.Overlay == null,
                _ => true // network 和 storage 尚未实现
            };

            return (!hasUnauthorizedPermission && allGrantedPermissionsValid && unauthorizedApiReturnsNull)
                .Label($"Unauthorized: {unauthorizedPermission}, HasPerm: {hasUnauthorizedPermission}, " +
                       $"GrantedValid: {allGrantedPermissionsValid}, ApiNull: {unauthorizedApiReturnsNull}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 1: 沙箱安全性（RequirePermission）**
        /// *对于任意*未授权的权限，调用 RequirePermission 应抛出 PermissionDeniedException
        /// **Validates: Requirements 1.2, 7.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property RequirePermission_ShouldThrowForUnauthorized(PositiveInt permIndex)
        {
            var allPermissions = new[] { "audio", "overlay", "network", "storage" };
            var unauthorizedPermission = allPermissions[permIndex.Get % allPermissions.Length];

            // 创建一个空权限列表的 API
            var api = CreatePluginApi(new List<string>());

            // 调用 RequirePermission 应该抛出异常
            var exceptionThrown = false;
            var correctExceptionType = false;
            var correctPermissionInException = false;

            try
            {
                api.RequirePermission(unauthorizedPermission);
            }
            catch (PermissionDeniedException ex)
            {
                exceptionThrown = true;
                correctExceptionType = true;
                correctPermissionInException = ex.Permission == unauthorizedPermission;
            }
            catch
            {
                exceptionThrown = true;
            }

            return (exceptionThrown && correctExceptionType && correctPermissionInException)
                .Label($"Permission: {unauthorizedPermission}, Thrown: {exceptionThrown}, " +
                       $"CorrectType: {correctExceptionType}, CorrectPerm: {correctPermissionInException}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 1: 沙箱安全性（授权访问）**
        /// *对于任意*已授权的权限，访问对应的 API 应该返回非 null 值
        /// **Validates: Requirements 1.2, 7.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property AuthorizedApiAccess_ShouldReturnNonNull(PositiveInt permIndex)
        {
            // 需要权限的 API
            var permissionApis = new[] { "audio", "overlay" };
            var authorizedPermission = permissionApis[permIndex.Get % permissionApis.Length];

            // 创建包含该权限的 API
            var api = CreatePluginApi(new List<string> { authorizedPermission });

            // 验证已授权的权限
            var hasPermission = api.HasPermission(authorizedPermission);

            // 访问已授权的 API 应该返回非 null
            bool authorizedApiReturnsNonNull = authorizedPermission switch
            {
                "audio" => api.Speech != null,
                "overlay" => api.Overlay != null,
                _ => true
            };

            // RequirePermission 不应该抛出异常
            var noExceptionThrown = true;
            try
            {
                api.RequirePermission(authorizedPermission);
            }
            catch
            {
                noExceptionThrown = false;
            }

            return (hasPermission && authorizedApiReturnsNonNull && noExceptionThrown)
                .Label($"Permission: {authorizedPermission}, HasPerm: {hasPermission}, " +
                       $"ApiNonNull: {authorizedApiReturnsNonNull}, NoException: {noExceptionThrown}");
        }

        #endregion


        #region Unit Tests

        /// <summary>
        /// 无权限的插件应该无法访问 Speech API
        /// </summary>
        [Fact]
        public void NoPermission_ShouldNotAccessSpeechApi()
        {
            var api = CreatePluginApi(new List<string>());

            Assert.Null(api.Speech);
            Assert.False(api.HasPermission("audio"));
        }

        /// <summary>
        /// 无权限的插件应该无法访问 Overlay API
        /// </summary>
        [Fact]
        public void NoPermission_ShouldNotAccessOverlayApi()
        {
            var api = CreatePluginApi(new List<string>());

            Assert.Null(api.Overlay);
            Assert.False(api.HasPermission("overlay"));
        }

        /// <summary>
        /// 有 audio 权限的插件应该能访问 Speech API
        /// </summary>
        [Fact]
        public void WithAudioPermission_ShouldAccessSpeechApi()
        {
            var api = CreatePluginApi(new List<string> { "audio" });

            Assert.NotNull(api.Speech);
            Assert.True(api.HasPermission("audio"));
        }

        /// <summary>
        /// 有 overlay 权限的插件应该能访问 Overlay API
        /// </summary>
        [Fact]
        public void WithOverlayPermission_ShouldAccessOverlayApi()
        {
            var api = CreatePluginApi(new List<string> { "overlay" });

            Assert.NotNull(api.Overlay);
            Assert.True(api.HasPermission("overlay"));
        }

        /// <summary>
        /// Core API 应该始终可用（无需权限）
        /// </summary>
        [Fact]
        public void CoreApi_ShouldAlwaysBeAvailable()
        {
            var api = CreatePluginApi(new List<string>());

            Assert.NotNull(api.Core);
            Assert.NotNull(api.Config);
            Assert.NotNull(api.Profile);
        }

        /// <summary>
        /// Log 方法应该不抛出异常
        /// </summary>
        [Fact]
        public void Log_ShouldNotThrow()
        {
            var api = CreatePluginApi(new List<string>());

            // 不应该抛出异常
            api.Log("Test message");
            api.Core.Log("Test message");
            api.Core.Warn("Warning message");
            api.Core.Error("Error message");
        }

        /// <summary>
        /// Profile 信息应该正确
        /// </summary>
        [Fact]
        public void ProfileInfo_ShouldBeCorrect()
        {
            var manifest = CreateTestManifest();
            CreateJsFile("function onLoad() {} function onUnload() {}");
            
            var context = new PluginContext(manifest, _tempDir);
            var config = new PluginConfig("test-plugin");
            var profileInfo = new ProfileInfo("my-profile", "My Profile", "/path/to/profile");

            var api = new PluginApi(context, config, profileInfo);

            Assert.Equal("my-profile", api.Profile.Id);
            Assert.Equal("My Profile", api.Profile.Name);
            Assert.Equal("/path/to/profile", api.Profile.Directory);
        }

        /// <summary>
        /// 权限检查应该不区分大小写
        /// </summary>
        [Fact]
        public void PermissionCheck_ShouldBeCaseInsensitive()
        {
            var api = CreatePluginApi(new List<string> { "AUDIO", "Overlay" });

            Assert.True(api.HasPermission("audio"));
            Assert.True(api.HasPermission("AUDIO"));
            Assert.True(api.HasPermission("Audio"));
            Assert.True(api.HasPermission("overlay"));
            Assert.True(api.HasPermission("OVERLAY"));
        }

        #endregion
    }
}
