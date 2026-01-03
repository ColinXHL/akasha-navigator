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
        /// <returns>安装结果</returns>
        InstallResult InstallPlugin(string pluginId, string? sourceDirectory = null);

        /// <summary>
        /// 卸载插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="force">是否强制卸载</param>
        /// <param name="getReferencingProfiles">获取引用此插件的Profile列表的函数</param>
        /// <returns>卸载结果</returns>
        UninstallResult UninstallPlugin(string pluginId, bool force = false, Func<string, List<string>>? getReferencingProfiles = null);

        /// <summary>
        /// 检查插件是否有可用更新
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>更新检查结果</returns>
        UpdateCheckResult CheckForUpdate(string pluginId);

        /// <summary>
        /// 检查所有已安装插件的更新
        /// </summary>
        /// <returns>有更新的插件列表</returns>
        List<UpdateCheckResult> CheckAllUpdates();

        /// <summary>
        /// 更新插件到最新版本
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>更新结果</returns>
        UpdateResult UpdatePlugin(string pluginId);
    }
}
