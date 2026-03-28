using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AkashaNavigator.Services
{
/// <summary>
/// 插件宿主服务
/// 负责插件的加载、执行和生命周期管理
/// </summary>
public class PluginHost : IPluginHost, IDisposable
{
#region Singleton

    private static PluginHost? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// 获取单例实例（插件系统使用）
    /// </summary>
    public static PluginHost Instance
    {
        get {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 使用 DI 容器中的实例，确保与注入的实例一致
                        var logService = App.Services?.GetRequiredService<ILogService>() ?? LogService.Instance;
                        var pluginAssociationManager = App.Services?.GetRequiredService<IPluginAssociationManager>() ??
                                                       PluginAssociationManager.Instance;
                        var pluginLibrary =
                            App.Services?.GetRequiredService<IPluginLibrary>() ?? PluginLibrary.Instance;

                        _instance = new PluginHost(logService, pluginAssociationManager, pluginLibrary);
                    }
                }
            }
            return _instance;
        }
    internal
        set => _instance = value;
    }

#endregion

#region Fields

    private readonly ILogService _logService;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly IPluginLibrary _pluginLibrary;

#endregion

#region Events

    /// <summary>
    /// 插件加载完成事件
    /// </summary>
    public event EventHandler<PluginContext>? PluginLoaded;

    /// <summary>
    /// 插件卸载事件
    /// </summary>
    public event EventHandler<string>? PluginUnloaded;

#endregion

#region Fields

    private readonly List<PluginContext> _loadedPlugins = new();
    private readonly Dictionary<string, PluginConfig> _pluginConfigs = new();
    private readonly Dictionary<string, PluginApi> _pluginApis = new();
    private string? _currentProfileId;
    private bool _disposed;

    // 全局 PlayerWindow 获取器
    private static Func<Views.Windows.PlayerWindow?>? _globalWindowGetter;

#endregion

#region Static Methods

    /// <summary>
    /// 设置全局 PlayerWindow 获取器
    /// 应在应用启动时调用
    /// </summary>
    /// <param name="windowGetter">获取 PlayerWindow 的委托</param>
    public static void SetGlobalWindowGetter(Func<Views.Windows.PlayerWindow?> windowGetter)
    {
        _globalWindowGetter = windowGetter;
    }

#endregion

#region Properties

    /// <summary>
    /// 已加载的插件列表
    /// </summary>
    public IReadOnlyList<PluginContext> LoadedPlugins => _loadedPlugins.AsReadOnly();

    /// <summary>
    /// 当前 Profile ID
    /// </summary>
    public string? CurrentProfileId => _currentProfileId;

#endregion

#region Constructor

    /// <summary>
    /// 私有构造函数（单例模式 + DI）
    /// </summary>
    public PluginHost(ILogService logService, IPluginAssociationManager pluginAssociationManager,
                      IPluginLibrary pluginLibrary)
    {
        _logService = logService;
        _pluginAssociationManager = pluginAssociationManager;
        _pluginLibrary = pluginLibrary;

        // 订阅插件启用状态变化事件
        _pluginAssociationManager.PluginEnabledChanged += OnPluginEnabledChanged;

        // 订阅插件关联变化事件（用于当前 Profile 动态加载/卸载）
        _pluginAssociationManager.AssociationChanged += OnAssociationChanged;

        // 订阅插件库变化事件（用于插件更新后自动重新加载）
        _pluginLibrary.PluginChanged += OnPluginLibraryChanged;
    }

    /// <summary>
    /// 用于测试的内部构造函数
    /// </summary>
    internal PluginHost(bool forTesting, ILogService logService, IPluginAssociationManager pluginAssociationManager,
                        IPluginLibrary? pluginLibrary = null)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _pluginAssociationManager =
            pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        _pluginLibrary = pluginLibrary ?? PluginLibrary.Instance;
        // 测试用构造函数，不订阅事件
    }

#endregion

#region Public Methods

    /// <summary>
    /// 加载指定 Profile 的所有插件
    /// 使用新的清单化架构：从 PluginAssociationManager 获取插件引用，从 PluginLibrary 获取插件目录
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    public void LoadPluginsForProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return;

        // 如果已有插件加载，先卸载
        if (_loadedPlugins.Count > 0)
        {
            UnloadAllPlugins();
        }

        _currentProfileId = profileId;

        // 1. 从 PluginAssociationManager 获取 Profile 的插件引用
        var pluginReferences = _pluginAssociationManager.GetPluginsInProfile(profileId);
        if (pluginReferences.Count == 0)
        {
            Log("Profile '{ProfileId}' 没有关联任何插件", profileId);
            return;
        }

        // 2. 遍历启用的插件引用
        foreach (var reference in pluginReferences)
        {
            // 跳过禁用的插件
            if (!reference.Enabled)
            {
                Log("插件 {PluginId} 已禁用，跳过加载", reference.PluginId);
                continue;
            }

            // 3. 检查插件是否已安装（从 PluginLibrary）
            if (!_pluginLibrary.IsInstalled(reference.PluginId))
            {
                Log("插件 {PluginId} 未安装，跳过加载", reference.PluginId);
                continue;
            }

            // 4. 从 PluginLibrary 获取插件目录
            var pluginDir = _pluginLibrary.GetPluginDirectory(reference.PluginId);

            // 5. 获取 Profile 特定的配置目录
            var configDir = GetPluginConfigDirectory(profileId, reference.PluginId);

            // 6. 加载插件
            LoadPlugin(pluginDir, configDir, reference.PluginId);
        }

        Log("已加载 {PluginCount} 个插件 (Profile: {ProfileId})", _loadedPlugins.Count, profileId);
    }

    /// <summary>
    /// 卸载所有插件
    /// </summary>
    public void UnloadAllPlugins()
    {
        foreach (var plugin in _loadedPlugins.ToList())
        {
            UnloadPlugin(plugin);
        }

        _loadedPlugins.Clear();
        _pluginConfigs.Clear();
        _pluginApis.Clear();
        _currentProfileId = null;

        Log("已卸载所有插件");
    }

    /// <summary>
    /// 启用或禁用插件
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="enabled">是否启用</param>
    public void SetPluginEnabled(string pluginId, bool enabled)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
        if (plugin == null)
            return;

        if (plugin.IsEnabled == enabled)
            return;

        plugin.IsEnabled = enabled;

        // 更新配置（使用配置目录）
        if (_pluginConfigs.TryGetValue(pluginId, out var config))
        {
            config.Enabled = enabled;
            SavePluginConfig(config, plugin.ConfigDirectory);
        }

        Log("插件 {PluginId} 已{Status}", pluginId, enabled ? "启用" : "禁用");
    }

    /// <summary>
    /// 根据 ID 获取插件
    /// </summary>
    public PluginContext? GetPlugin(string pluginId)
    {
        return _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
    }

    /// <summary>
    /// 获取插件配置
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>插件配置，不存在则返回 null</returns>
    public PluginConfig? GetPluginConfig(string pluginId)
    {
        return _pluginConfigs.TryGetValue(pluginId, out var config) ? config : null;
    }

    /// <summary>
    /// 保存插件配置
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public void SavePluginConfig(string pluginId)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin == null)
            return;

        if (_pluginConfigs.TryGetValue(pluginId, out var config))
        {
            SavePluginConfig(config, plugin.ConfigDirectory);
        }
    }

    /// <summary>
    /// 广播事件到所有启用的插件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="data">事件数据</param>
    public void BroadcastEvent(string eventName, object data)
    {
        foreach (var kvp in _pluginApis)
        {
            var pluginId = kvp.Key;
            var pluginApi = kvp.Value;

            // 检查插件是否启用
            var plugin = GetPlugin(pluginId);
            if (plugin == null || !plugin.IsEnabled)
                continue;

            // 检查插件是否有 events 权限
            if (!pluginApi.HasPermission(PluginPermissions.Events))
                continue;

            // 触发事件
            try
            {
                pluginApi.Event?.emit(eventName, data);
            }
            catch (Exception ex)
            {
                Log("广播事件 {EventName} 到插件 {PluginId} 失败: {ErrorMessage}", eventName, pluginId, ex.Message);
            }
        }
    }

    /// <summary>
    /// 广播播放状态变化事件
    /// </summary>
    /// <param name="playing">是否正在播放</param>
    public void BroadcastPlayStateChanged(bool playing)
    {
        BroadcastEvent(EventManager.PlayStateChanged, new { playing });
    }

    /// <summary>
    /// 广播时间更新事件
    /// </summary>
    /// <param name="currentTime">当前时间（秒）</param>
    /// <param name="duration">总时长（秒）</param>
    public void BroadcastTimeUpdate(double currentTime, double duration)
    {
        BroadcastEvent(EventManager.TimeUpdate, new { currentTime, duration });
    }

    /// <summary>
    /// 广播 URL 变化事件
    /// </summary>
    /// <param name="url">新 URL</param>
    public void BroadcastUrlChanged(string url)
    {
        BroadcastEvent(EventManager.UrlChanged, new { url });
    }

    /// <summary>
    /// 动态启用插件（如果当前 Profile 匹配）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    public void EnablePlugin(string profileId, string pluginId)
    {
        // 检查当前 Profile 是否匹配
        if (string.IsNullOrWhiteSpace(_currentProfileId) ||
            !string.Equals(_currentProfileId, profileId, StringComparison.OrdinalIgnoreCase))
        {
            Log("当前 Profile ({CurrentProfileId}) 与目标 Profile ({ProfileId}) 不匹配，跳过启用插件 {PluginId}",
                _currentProfileId, profileId, pluginId);
            return;
        }

        // 检查插件是否已加载
        if (_loadedPlugins.Any(p => string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)))
        {
            Log("插件 {PluginId} 已加载，跳过", pluginId);
            return;
        }

        // 检查插件是否已安装
        if (!_pluginLibrary.IsInstalled(pluginId))
        {
            Log("插件 {PluginId} 未安装，无法启用", pluginId);
            return;
        }

        // 从 PluginLibrary 获取插件目录
        var pluginDir = _pluginLibrary.GetPluginDirectory(pluginId);

        // 获取 Profile 特定的配置目录
        var configDir = GetPluginConfigDirectory(profileId, pluginId);

        // 加载插件
        LoadPlugin(pluginDir, configDir, pluginId);

        Log("插件 {PluginId} 已动态启用", pluginId);
    }

    /// <summary>
    /// 重新加载指定插件（用于更新后）
    /// 如果插件正在运行，先卸载再加载；如果未运行则不执行任何操作
    /// 配置目录保持不变，确保用户配置不丢失
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public void ReloadPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return;

        // 检查插件是否正在运行
        var plugin =
            _loadedPlugins.FirstOrDefault(p => string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

        if (plugin == null)
        {
            // 插件未运行，不需要重新加载
            Log("插件 {PluginId} 未运行，跳过重新加载", pluginId);
            return;
        }

        // 保存配置目录路径（用于重新加载时使用）
        var configDir = plugin.ConfigDirectory;
        var profileId = _currentProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
        {
            Log("当前没有活动的 Profile，无法重新加载插件 {PluginId}", pluginId);
            return;
        }

        Log("开始重新加载插件 {PluginId}...", pluginId);

        // 1. 卸载插件
        UnloadPlugin(plugin);
        _loadedPlugins.Remove(plugin);
        _pluginConfigs.Remove(pluginId);
        _pluginApis.Remove(pluginId);

        // 2. 检查插件是否仍然安装
        if (!_pluginLibrary.IsInstalled(pluginId))
        {
            Log("插件 {PluginId} 已不存在于插件库中，无法重新加载", pluginId);
            return;
        }

        // 3. 从 PluginLibrary 获取新的插件目录
        var pluginDir = _pluginLibrary.GetPluginDirectory(pluginId);

        // 4. 使用原有的配置目录重新加载插件（保留配置）
        if (string.IsNullOrWhiteSpace(configDir))
        {
            configDir = GetPluginConfigDirectory(profileId, pluginId);
        }

        LoadPlugin(pluginDir, configDir, pluginId);

        Log("插件 {PluginId} 重新加载完成", pluginId);
    }

    /// <summary>
    /// 动态禁用插件（卸载正在运行的插件）
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public void DisablePlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return;

        // 查找正在运行的插件
        var plugin =
            _loadedPlugins.FirstOrDefault(p => string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

        if (plugin == null)
        {
            Log("插件 {PluginId} 未加载，跳过禁用", pluginId);
            return;
        }

        // 卸载插件
        UnloadPlugin(plugin);

        // 从列表移除
        _loadedPlugins.Remove(plugin);

        // 从配置字典移除
        _pluginConfigs.Remove(pluginId);

        // 从 API 字典移除（UnloadPlugin 已经移除，这里确保清理）
        _pluginApis.Remove(pluginId);

        Log("插件 {PluginId} 已动态禁用", pluginId);
    }

    /// <summary>
    /// 取消订阅插件（停止运行并删除插件目录）
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>操作结果</returns>
    public UnsubscribeResult UnsubscribePlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return UnsubscribeResult.Failed("插件 ID 不能为空");
        }

        // 查找插件
        var plugin = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
        if (plugin == null)
        {
            // 插件不存在，静默成功（可能已被卸载）
            Log("插件 {PluginId} 不存在，跳过取消订阅", pluginId);
            return UnsubscribeResult.Succeeded();
        }

        var pluginDir = plugin.PluginDirectory;

        try
        {
            // 停止插件运行（调用 onUnload）
            UnloadPlugin(plugin);

            // 从列表移除
            _loadedPlugins.Remove(plugin);

            // 从配置字典移除
            _pluginConfigs.Remove(pluginId);

            // 删除插件目录
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, recursive: true);
                Log("已删除插件目录: {PluginDir}", pluginDir);
            }

            Log("插件 {PluginId} 已取消订阅", pluginId);
            return UnsubscribeResult.Succeeded();
        }
        catch (UnauthorizedAccessException ex)
        {
            Log("删除插件目录失败（权限不足）: {ErrorMessage}", ex.Message);
            return UnsubscribeResult.Failed($"删除插件目录失败：权限不足。请确保没有其他程序正在使用该目录。");
        }
        catch (IOException ex)
        {
            Log("删除插件目录失败（文件被占用）: {ErrorMessage}", ex.Message);
            return UnsubscribeResult.Failed($"删除插件目录失败：文件被占用。请关闭相关程序后重试。");
        }
        catch (Exception ex)
        {
            Log("取消订阅插件失败: {ErrorMessage}", ex.Message);
            return UnsubscribeResult.Failed($"取消订阅失败：{ex.Message}");
        }
    }

#endregion

#region Private Methods

    /// <summary>
    /// 加载单个插件（新目录结构：源码目录 + 配置目录分离）
    /// </summary>
    /// <param name="sourceDir">插件源码目录（内置插件库，只读）</param>
    /// <param name="configDir">插件配置目录（用户数据目录，可写）</param>
    /// <param name="pluginId">插件 ID</param>
    private void LoadPlugin(string sourceDir, string configDir, string pluginId)
    {
        // 检查源码目录是否存在
        if (!Directory.Exists(sourceDir))
        {
            Log("插件源码目录不存在 ({PluginId}): {SourceDir}", pluginId, sourceDir);
            return;
        }

        var manifestPath = Path.Combine(sourceDir, AppConstants.PluginManifestFileName);

        // 加载清单
        var loadResult = PluginManifest.LoadFromFile(manifestPath);
        if (!loadResult.IsSuccess)
        {
            Log("加载插件清单失败 ({PluginId}): {ErrorMessage}", pluginId, loadResult.ErrorMessage);
            return;
        }

        var manifest = loadResult.Manifest!;

        // 检查是否已加载同 ID 插件
        if (_loadedPlugins.Any(p => p.PluginId == manifest.Id))
        {
            Log("插件 {PluginId} 已加载，跳过", manifest.Id);
            return;
        }

        // 确保配置目录存在
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        // 从用户配置目录加载配置
        var configPath = Path.Combine(configDir, AppConstants.PluginConfigFileName);
        var config = PluginConfig.LoadFromFile(configPath, manifest.Id!);

        // 应用默认配置
        config.ApplyDefaults(manifest.DefaultConfig);
        _pluginConfigs[manifest.Id!] = config;

        // 如果插件被禁用，跳过加载
        if (!config.Enabled)
        {
            Log("插件 {PluginId} 已禁用，跳过加载", manifest.Id);
            return;
        }

        // 创建 Profile 信息
        var profileInfo = new ProfileInfo(_currentProfileId ?? string.Empty, _currentProfileId ?? string.Empty,
                                          GetPluginConfigDirectory(_currentProfileId ?? string.Empty, pluginId));

        // 创建引擎选项
        var engineOptions = new PluginEngineOptions {
            ProfileId = _currentProfileId ?? string.Empty, ProfileName = _currentProfileId ?? string.Empty,
            ProfileDirectory = GetPluginConfigDirectory(_currentProfileId ?? string.Empty, pluginId),
            GetPlayerWindow = _globalWindowGetter,
            OsdManager = App.Services?.GetService<Core.OsdManager>()
        };

        // 创建插件上下文
        var context = PluginContext.Create(manifest, sourceDir, configDir, config, engineOptions);
        context.IsEnabled = config.Enabled;

        // 加载脚本
        if (!context.LoadScript())
        {
            Log("加载插件脚本失败 ({PluginId}): {ErrorMessage}", manifest.Id, context.LastError);
            context.Dispose();
            return;
        }

        // 创建 PluginApi（用于事件广播等）
        var pluginApi = new PluginApi(context, config, profileInfo);

        // 设置 HotkeyApi 的 ActionDispatcher（如果插件有 hotkey 权限）
        SetupHotkeyApi(context, manifest);

        // 调用 onLoad
        if (!context.CallOnLoad())
        {
            Log("插件 {PluginId} onLoad 调用失败: {ErrorMessage}", manifest.Id, context.LastError);
            // 即使 onLoad 失败，也保留插件（异常隔离）
        }

        _loadedPlugins.Add(context);
        _pluginApis[manifest.Id!] = pluginApi;
        PluginLoaded?.Invoke(this, context);

        Log("插件 {PluginName} (v{Version}) 加载成功", manifest.Name, manifest.Version);
    }

    /// <summary>
    /// 卸载单个插件
    /// </summary>
    private void UnloadPlugin(PluginContext plugin)
    {
        var pluginId = plugin.PluginId;

        try
        {
            plugin.CallOnUnload();
        }
        catch (Exception ex)
        {
            Log("插件 {PluginId} onUnload 调用失败: {ErrorMessage}", pluginId, ex.Message);
        }

        // 清理 PluginApi
        if (_pluginApis.TryGetValue(pluginId, out var pluginApi))
        {
            try
            {
                pluginApi.Cleanup();
            }
            catch (Exception ex)
            {
                Log("清理插件 API 失败 ({PluginId}): {ErrorMessage}", pluginId, ex.Message);
            }
        }

        try
        {
            plugin.Dispose();
        }
        catch (Exception ex)
        {
            Log("释放插件资源失败 ({PluginId}): {ErrorMessage}", pluginId, ex.Message);
        }

        _pluginApis.Remove(pluginId);
        PluginUnloaded?.Invoke(this, pluginId);
        Log("插件 {PluginId} 已卸载", pluginId);
    }

    /// <summary>
    /// 设置插件的 HotkeyApi（如果插件有 hotkey 权限）
    /// </summary>
    /// <param name="context">插件上下文</param>
    /// <param name="manifest">插件清单</param>
    private void SetupHotkeyApi(PluginContext context, PluginManifest manifest)
    {
        // 检查插件是否有 hotkey 权限
        var permissions = manifest.Permissions ?? new List<string>();
        if (!permissions.Contains(PluginPermissions.Hotkey, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        // 从 V8 引擎获取 HotkeyApi 实例
        if (context.Engine == null)
        {
            Log("插件 {PluginId} 的引擎为空，无法设置 HotkeyApi", context.PluginId);
            return;
        }

        try
        {
            // 从引擎中获取 hotkey 全局对象
            var hotkeyObj = context.Engine.Script.hotkey;
            if (hotkeyObj == null)
            {
                Log("插件 {PluginId} 没有 hotkey API，跳过设置", context.PluginId);
                return;
            }

            // 将 ScriptObject 转换为 HotkeyApi
            if (hotkeyObj is Plugins.Apis.HotkeyApi hotkeyApi)
            {
                // 从 DI 容器获取 HotkeyService
                var hotkeyService = App.Services?.GetService<HotkeyService>();
                if (hotkeyService != null)
                {
                    var dispatcher = hotkeyService.GetDispatcher();
                    hotkeyApi.SetDispatcher(dispatcher);
                    hotkeyApi.SetHotkeyService(hotkeyService);
                    Log("插件 {PluginId} 的 HotkeyApi 已设置 ActionDispatcher 和 HotkeyService", context.PluginId);
                }
                else
                {
                    Log("无法获取 HotkeyService，插件 {PluginId} 的快捷键功能将不可用", context.PluginId);
                }
            }
        }
        catch (Exception ex)
        {
            Log("设置插件 {PluginId} 的 HotkeyApi 失败: {ErrorMessage}", context.PluginId, ex.Message);
        }
    }

    /// <summary>
    /// 获取插件用户配置目录
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>配置目录路径</returns>
    public string GetPluginConfigDirectory(string profileId, string pluginId)
    {
        return AppPaths.GetPluginConfigDirectory(profileId, pluginId);
    }

    /// <summary>
    /// 保存插件配置
    /// </summary>
    private void SavePluginConfig(PluginConfig config, string configDir)
    {
        var configPath = Path.Combine(configDir, AppConstants.PluginConfigFileName);
        config.SaveToFile(configPath);
    }

    /// <summary>
    /// 记录日志（参数化模板）
    /// </summary>
    private void Log(string messageTemplate, params object?[] args)
    {
        _logService.Info(nameof(PluginHost), messageTemplate, args);
    }

    /// <summary>
    /// 处理插件启用状态变化事件
    /// </summary>
    private void OnPluginEnabledChanged(object? sender, PluginEnabledChangedEventArgs e)
    {
        if (e.Enabled)
        {
            // 启用插件
            EnablePlugin(e.ProfileId, e.PluginId);
        }
        else
        {
            // 禁用插件（只有当前 Profile 匹配时才禁用）
            if (!string.IsNullOrWhiteSpace(_currentProfileId) &&
                string.Equals(_currentProfileId, e.ProfileId, StringComparison.OrdinalIgnoreCase))
            {
                DisablePlugin(e.PluginId);
            }
        }
    }

    /// <summary>
    /// 处理插件库变化事件
    /// </summary>
    private void OnPluginLibraryChanged(object? sender, PluginLibraryChangedEventArgs e)
    {
        switch (e.ChangeType)
        {
        case PluginLibraryChangeType.Installed:
            if (!string.IsNullOrWhiteSpace(_currentProfileId) &&
                _pluginAssociationManager.ProfileContainsPlugin(_currentProfileId, e.PluginId) &&
                _pluginAssociationManager.GetPluginEnabled(_currentProfileId, e.PluginId) != false)
            {
                Log("检测到插件 {PluginId} 已安装，尝试为当前 Profile 动态加载...", e.PluginId);
                EnablePlugin(_currentProfileId, e.PluginId);
            }
            break;
        case PluginLibraryChangeType.Uninstalled:
            Log("检测到插件 {PluginId} 已卸载，尝试从运行态移除...", e.PluginId);
            DisablePlugin(e.PluginId);
            break;
        case PluginLibraryChangeType.Updated:
            Log("检测到插件 {PluginId} 已更新，尝试重新加载...", e.PluginId);
            ReloadPlugin(e.PluginId);
            break;
        }
    }

    /// <summary>
    /// 处理插件关联变化事件
    /// </summary>
    private void OnAssociationChanged(object? sender, AssociationChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentProfileId))
            return;

        switch (e.ChangeType)
        {
        case AssociationChangeType.Added:
            HandleAssociationAdded(e.ProfileId, e.PluginId);
            break;
        case AssociationChangeType.Removed:
            HandleAssociationRemoved(e.ProfileId, e.PluginId);
            break;
        case AssociationChangeType.BatchAdded:
            if (string.Equals(_currentProfileId, e.ProfileId, StringComparison.OrdinalIgnoreCase) && e.PluginIds != null)
            {
                foreach (var pluginId in e.PluginIds)
                {
                    HandleAssociationAdded(e.ProfileId, pluginId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(e.PluginId) && e.ProfileIds != null)
            {
                foreach (var profileId in e.ProfileIds)
                {
                    HandleAssociationAdded(profileId, e.PluginId);
                }
            }
            break;
        case AssociationChangeType.BatchRemoved:
            if (!string.IsNullOrWhiteSpace(e.PluginId) && e.ProfileIds != null)
            {
                foreach (var profileId in e.ProfileIds)
                {
                    HandleAssociationRemoved(profileId, e.PluginId);
                }
            }
            break;
        }
    }

    private void HandleAssociationAdded(string profileId, string pluginId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId) ||
            string.IsNullOrWhiteSpace(_currentProfileId) ||
            !string.Equals(_currentProfileId, profileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_pluginAssociationManager.GetPluginEnabled(profileId, pluginId) == false)
        {
            Log("插件 {PluginId} 已关联到当前 Profile，但处于禁用状态，跳过动态加载", pluginId);
            return;
        }

        Log("检测到插件 {PluginId} 已关联到当前 Profile，尝试动态加载...", pluginId);
        EnablePlugin(profileId, pluginId);
    }

    private void HandleAssociationRemoved(string profileId, string pluginId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId) ||
            string.IsNullOrWhiteSpace(_currentProfileId) ||
            !string.Equals(_currentProfileId, profileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Log("检测到插件 {PluginId} 已从当前 Profile 移除，尝试动态卸载...", pluginId);
        DisablePlugin(pluginId);
    }

#endregion

#region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // 取消订阅事件
            _pluginAssociationManager.AssociationChanged -= OnAssociationChanged;
            _pluginAssociationManager.PluginEnabledChanged -= OnPluginEnabledChanged;
            _pluginLibrary.PluginChanged -= OnPluginLibraryChanged;

            UnloadAllPlugins();
        }

        _disposed = true;
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~PluginHost()
    {
        Dispose(false);
    }

#endregion
}
}
