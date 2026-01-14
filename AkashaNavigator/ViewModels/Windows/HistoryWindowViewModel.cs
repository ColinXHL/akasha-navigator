using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Common;

namespace AkashaNavigator.ViewModels.Windows
{
/// <summary>
/// 历史记录窗口 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class HistoryWindowViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    /// <summary>
    /// 历史记录列表
    /// </summary>
    public ObservableCollection<HistoryItem> HistoryItems { get; } = new();

    /// <summary>
    /// 搜索文本（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 是否为空（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearAllCommand))]
    private bool _isEmpty;

    /// <summary>
    /// 选择历史项事件（由 Code-behind 订阅以关闭窗口）
    /// </summary>
    public event EventHandler<HistoryItem?>? ItemSelected;

    /// <summary>
    /// 请求显示确认对话框事件（由 Code-behind 订阅以显示对话框）
    /// </summary>
    public event EventHandler<ConfirmDialogRequest>? ConfirmDialogRequested;

    public HistoryWindowViewModel(IDataService dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        LoadHistory();
    }

    /// <summary>
    /// 搜索文本变化时重新加载（自动生成的方法）
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        LoadHistory();
    }

    /// <summary>
    /// 加载历史记录
    /// </summary>
    public void LoadHistory()
    {
        var history =
            string.IsNullOrWhiteSpace(SearchText) ? _dataService.GetHistory() : _dataService.SearchHistory(SearchText);

        HistoryItems.Clear();
        foreach (var item in history)
        {
            HistoryItems.Add(item);
        }

        IsEmpty = HistoryItems.Count == 0;
    }

    /// <summary>
    /// 删除指定历史项（自动生成 DeleteCommand）
    /// </summary>
    [RelayCommand]
    private void Delete(int id)
    {
        _dataService.DeleteHistory(id);
        LoadHistory();
    }

    /// <summary>
    /// 清空全部历史（自动生成 ClearAllCommand）
    /// 触发确认对话框请求事件，由 View 显示对话框
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearAll))]
    private void ClearAll()
    {
        ConfirmDialogRequested?.Invoke(
            this,
            new ConfirmDialogRequest { Message = "确定要清空所有历史记录吗？此操作不可撤销。", Title = "确认清空",
                                       ConfirmText = "清空", CancelText = "取消", OnConfirmed = ExecuteClearAll });
    }

    /// <summary>
    /// 执行清空历史记录操作
    /// </summary>
    private void ExecuteClearAll()
    {
        _dataService.ClearHistory();
        LoadHistory();
    }

    /// <summary>
    /// 是否可以清空（当列表不为空时）
    /// </summary>
    private bool CanClearAll() => !IsEmpty;

    /// <summary>
    /// 选择历史项（自动生成 SelectItemCommand）
    /// </summary>
    [RelayCommand]
    private void SelectItem(HistoryItem? item)
    {
        ItemSelected?.Invoke(this, item);
    }
}
}
