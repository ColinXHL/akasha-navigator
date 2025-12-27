using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// 退出记录提示窗口
/// 当用户退出应用且当前页面未记录时显示
/// </summary>
public partial class ExitRecordPrompt : AnimatedWindow
{
#region Enums

    /// <summary>
    /// 用户操作结果
    /// </summary>
    public enum PromptResult
    {
        /// <summary>
        /// 取消操作，不做任何事情
        /// </summary>
        Cancel,

        /// <summary>
        /// 继续退出应用
        /// </summary>
        Exit,

        /// <summary>
        /// 打开开荒笔记窗口
        /// </summary>
        OpenPioneerNotes,

        /// <summary>
        /// 打开快速记录对话框
        /// </summary>
        QuickRecord
    }

#endregion

#region Properties

    /// <summary>
    /// 用户选择的操作结果
    /// </summary>
    public PromptResult Result { get; private set; } = PromptResult.Cancel;

    /// <summary>
    /// 当前页面 URL
    /// </summary>
    public string PageUrl { get; }

    /// <summary>
    /// 当前页面标题
    /// </summary>
    public string PageTitle { get; }

#endregion

#region Fields

    private readonly IPioneerNoteService _pioneerNoteService;

#endregion

#region Constructor

    /// <summary>
    /// DI容器注入的构造函数
    /// </summary>
    public ExitRecordPrompt(IPioneerNoteService pioneerNoteService, string url, string title)
    {
        _pioneerNoteService = pioneerNoteService;
        InitializeComponent();

        PageUrl = url ?? string.Empty;
        PageTitle = title ?? string.Empty;

        // 设置页面预览信息
        TxtPageTitle.Text = string.IsNullOrWhiteSpace(PageTitle) ? "(无标题)" : PageTitle;
        TxtPageUrl.Text = string.IsNullOrWhiteSpace(PageUrl) ? "(无 URL)" : PageUrl;
    }

#endregion

#region Static Methods

    /// <summary>
    /// 检查是否需要显示退出记录提示
    /// </summary>
    /// <param name="url">当前页面 URL</param>
    /// <param name="pioneerNoteService">开荒笔记服务（可选，用于测试）</param>
    /// <returns>如果 URL 未记录且非空，返回 true</returns>
    public static bool ShouldShowPrompt(string url, IPioneerNoteService? pioneerNoteService = null)
    {
        // 如果 URL 为空，不显示提示
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var service = pioneerNoteService ?? PioneerNoteService.Instance;
        // 检查 URL 是否已记录
        return !service.IsUrlRecorded(url);
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 关闭按钮点击 - 取消操作，不做任何事情
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.Cancel;
        CloseWithAnimation();
    }

    /// <summary>
    /// 打开开荒笔记按钮点击
    /// </summary>
    private void BtnOpenPioneerNotes_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.OpenPioneerNotes;
        CloseWithAnimation();
    }

    /// <summary>
    /// 快速记录按钮点击
    /// </summary>
    private void BtnQuickRecord_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.QuickRecord;
        CloseWithAnimation();
    }

    /// <summary>
    /// 直接退出按钮点击
    /// </summary>
    private void BtnDirectExit_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptResult.Exit;
        CloseWithAnimation();
    }

#endregion
}
}
