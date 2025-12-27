using System;
using System.Collections.Generic;
using AkashaNavigator.Models.Plugin;
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
        /// 卸载插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="force">是否强制卸载</param>
        /// <param name="getReferencingProfiles">获取引用此插件的Profile列表的函数</param>
        UninstallResult UninstallPlugin(string pluginId, bool force = false, Func<string, List<string>>? getReferencingProfiles = null);
    }
}
