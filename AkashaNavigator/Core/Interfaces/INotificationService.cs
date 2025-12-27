using System.Threading.Tasks;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 通知服务接口
    /// 负责管理和显示应用内通知，替代系统原生 MessageBox
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 显示通知（自动关闭）
        /// </summary>
        /// <param name="message">通知消息</param>
        /// <param name="type">通知类型</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="durationMs">显示持续时间（毫秒），默认 3000ms</param>
        void Show(string message, NotificationType type = NotificationType.Info, string? title = null,
                  int durationMs = 3000);

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <returns>用户选择：true=确定，false=取消</returns>
        Task<bool> ConfirmAsync(string message, string? title = null);

        /// <summary>
        /// 显示带自定义按钮的对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="yesText">确定按钮文本</param>
        /// <param name="noText">取消按钮文本</param>
        /// <param name="showCancel">是否显示取消按钮（暂未实现三按钮模式）</param>
        /// <returns>用户选择：true=确定，false=取消，null=关闭按钮</returns>
        Task<bool?> ShowDialogAsync(string message, string? title = null, string yesText = "确定",
                                    string noText = "取消", bool showCancel = false);

        /// <summary>
        /// 显示信息通知
        /// </summary>
        void Info(string message, string? title = null, int durationMs = 3000);

        /// <summary>
        /// 显示成功通知
        /// </summary>
        void Success(string message, string? title = null, int durationMs = 3000);

        /// <summary>
        /// 显示警告通知
        /// </summary>
        void Warning(string message, string? title = null, int durationMs = 3000);

        /// <summary>
        /// 显示错误通知
        /// </summary>
        void Error(string message, string? title = null, int durationMs = 4000);
    }
}
