using System;
using System.Collections.Generic;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Services;

namespace AkashaNavigator.Core.Interfaces
{
/// <summary>
/// 插件库管理服务接口
/// </summary>
public interface IPluginLibrary
{
    /// <summary>
    /// 插件变更事件
    /// </summary>
    event EventHandler<PluginLibraryChangedEventArgs> PluginChanged;

    /// <summary>
    /// 全局插件库目录
    /// </summary>
    string LibraryDirectory { get; }

    /// <summary>
    /// 插件库索引文件路径
    /// </summary>
    string LibraryIndexPath { get; }

    /// <summary>
    /// 重新加载索引
    /// </summary>
    void ReloadIndex();

    /// <summary>
    /// 获取所有已安装的插件
    /// </summary>
    List<InstalledPluginInfo> GetInstalledPlugins();

    /// <summary>
    /// 检查插件是否已安装
    /// </summary>
    bool IsInstalled(string pluginId);

    /// <summary>
    /// 获取插件目录
    /// </summary>
    string GetPluginDirectory(string pluginId);

    /// <summary>
    /// 获取插件清单
    /// </summary>
    PluginManifest? GetPluginManifest(string pluginId);

    /// <summary>
    /// 获取已安装插件信息
    /// </summary>
    InstalledPluginInfo? GetInstalledPluginInfo(string pluginId);

    /// <summary>
    /// 安装插件到全局库
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="sourceDirectory">源目录（为null时从内置插件目录查找）</param>
    /// <returns>安装结果，成功时返回 InstalledPluginInfo</returns>
    Result<InstalledPluginInfo> InstallPlugin(string pluginId, string? sourceDirectory = null);

    /// <summary>
    /// 从本地 ZIP 插件包安装或更新插件
    /// </summary>
    /// <param name="archivePath">插件 ZIP 文件路径</param>
    /// <returns>安装结果，成功时返回 InstalledPluginInfo</returns>
    Result<InstalledPluginInfo> InstallPluginPackage(string archivePath);

    /// <summary>
    /// 从已验证的目录执行统一安装或更新事务。
    /// </summary>
    Result<InstalledPluginInfo> InstallOrUpdateFromDirectory(
        string sourceDirectory,
        IReadOnlyList<string> savedFiles,
        string source);

    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="force">是否强制卸载</param>
    /// <param name="getReferencingProfiles">获取引用此插件的Profile列表的函数</param>
    /// <returns>卸载结果，失败时通过 Error.Code 区分状态，有引用时通过 Error.Metadata["ReferencingProfiles"]
    /// 传递引用列表</returns>
    Result UninstallPlugin(string pluginId, bool force = false,
                           Func<string, List<string>>? getReferencingProfiles = null);

}
}
