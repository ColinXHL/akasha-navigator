namespace AkashaNavigator.ViewModels.Common;

/// <summary>
/// 确认对话框请求，用于 ViewModel 向 View 请求显示确认对话框
/// </summary>
public class ConfirmDialogRequest
{
    /// <summary>
    /// 对话框消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 对话框标题
    /// </summary>
    public string Title { get; set; } = "确认";

    /// <summary>
    /// 确认按钮文本
    /// </summary>
    public string ConfirmText { get; set; } = "确定";

    /// <summary>
    /// 取消按钮文本
    /// </summary>
    public string CancelText { get; set; } = "取消";

    /// <summary>
    /// 确认后执行的操作
    /// </summary>
    public Action? OnConfirmed { get; set; }
}
