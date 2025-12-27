using System.Collections.Generic;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 订阅管理服务接口
    /// 管理用户的 Profile 和插件订阅
    /// </summary>
    public interface ISubscriptionManager
    {
        /// <summary>
        /// 订阅配置文件路径
        /// </summary>
        string SubscriptionsFilePath { get; }

        /// <summary>
        /// 用户 Profiles 目录
        /// </summary>
        string UserProfilesDirectory { get; }

        /// <summary>
        /// 加载订阅配置
        /// </summary>
        void Load();

        /// <summary>
        /// 保存订阅配置
        /// </summary>
        void Save();

        /// <summary>
        /// 获取已订阅的 Profile 列表
        /// </summary>
        /// <returns>Profile ID 列表</returns>
        List<string> GetSubscribedProfiles();

        /// <summary>
        /// 检查 Profile 是否已订阅
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否已订阅</returns>
        bool IsProfileSubscribed(string profileId);

        /// <summary>
        /// 订阅 Profile（复制模板到用户目录，自动订阅推荐插件）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功</returns>
        bool SubscribeProfile(string profileId);

        /// <summary>
        /// 取消订阅 Profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>取消订阅结果</returns>
        UnsubscribeResult UnsubscribeProfile(string profileId);

        /// <summary>
        /// 获取已订阅的插件列表（指定Profile）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件ID列表</returns>
        List<string> GetSubscribedPlugins(string profileId);

        /// <summary>
        /// 订阅插件到指定Profile
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileId">Profile ID</param>
        void SubscribePlugin(string pluginId, string profileId);

        /// <summary>
        /// 取消订阅插件
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileId">Profile ID</param>
        void UnsubscribePlugin(string pluginId, string profileId);
    }
}
