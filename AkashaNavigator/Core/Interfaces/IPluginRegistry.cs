using System.Collections.Generic;
using AkashaNavigator.Services;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 插件注册表接口
    /// 仅用于兼容旧 Profile 推荐插件的存在性索引，不提供版本或更新信息。
    /// </summary>
    public interface IPluginRegistry
    {
        /// <summary>
        /// 内置插件目录
        /// </summary>
        string BuiltInPluginsDirectory { get; }

        /// <summary>
        /// 获取所有内置插件
        /// </summary>
        /// <returns>插件信息列表</returns>
        List<BuiltInPluginInfo> GetAllPlugins();

        /// <summary>
        /// 获取指定插件的信息
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>插件信息，不存在则返回null</returns>
        BuiltInPluginInfo? GetPlugin(string pluginId);

        /// <summary>
        /// 获取插件源代码目录
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>插件源代码目录路径</returns>
        string GetPluginSourceDirectory(string pluginId);

        /// <summary>
        /// 检查插件是否存在
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>是否存在</returns>
        bool PluginExists(string pluginId);

        /// <summary>
        /// 重新加载插件注册表
        /// </summary>
        void Reload();
    }
}
