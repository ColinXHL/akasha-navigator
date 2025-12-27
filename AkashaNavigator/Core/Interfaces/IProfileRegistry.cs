using System.Collections.Generic;
using AkashaNavigator.Services;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// Profile 注册表接口
    /// 管理内置 Profile 的清单和元数据
    /// </summary>
    public interface IProfileRegistry
    {
        /// <summary>
        /// 内置 Profiles 目录
        /// </summary>
        string BuiltInProfilesDirectory { get; }

        /// <summary>
        /// 获取所有内置 Profile
        /// </summary>
        /// <returns>Profile 信息列表</returns>
        List<BuiltInProfileInfo> GetAllProfiles();

        /// <summary>
        /// 获取指定 Profile 的信息
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>Profile 信息，不存在则返回 null</returns>
        BuiltInProfileInfo? GetProfile(string profileId);

        /// <summary>
        /// 获取 Profile 模板目录
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>模板目录路径</returns>
        string GetProfileTemplateDirectory(string profileId);

        /// <summary>
        /// 检查 Profile 是否存在
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否存在</returns>
        bool ProfileExists(string profileId);

        /// <summary>
        /// 重新加载 Profile 注册表
        /// </summary>
        void Reload();
    }
}
