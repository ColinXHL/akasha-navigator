using System;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 窗口状态服务接口
    /// 负责保存和加载窗口位置、大小、最后访问 URL 等
    /// </summary>
    public interface IWindowStateService
    {
        /// <summary>
        /// 加载窗口状态
        /// </summary>
        WindowState Load();

        /// <summary>
        /// 保存窗口状态
        /// </summary>
        /// <param name="state">窗口状态</param>
        void Save(WindowState state);

        /// <summary>
        /// 更新并保存窗口状态
        /// </summary>
        /// <param name="updateAction">更新操作</param>
        void Update(Action<WindowState> updateAction);
    }
}
