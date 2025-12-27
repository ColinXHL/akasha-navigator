using System;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 配置服务接口
    /// 负责全局配置的加载、保存、访问
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// 配置变更事件
        /// </summary>
        event EventHandler<AppConfig>? ConfigChanged;

        /// <summary>
        /// 当前配置
        /// </summary>
        AppConfig Config { get; }

        /// <summary>
        /// 配置文件路径
        /// </summary>
        string ConfigFilePath { get; }

        /// <summary>
        /// 加载配置
        /// </summary>
        AppConfig Load();

        /// <summary>
        /// 保存配置
        /// </summary>
        void Save();

        /// <summary>
        /// 更新配置并保存
        /// </summary>
        /// <param name="newConfig">新配置</param>
        void UpdateConfig(AppConfig newConfig);

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        void ResetToDefault();
    }
}
