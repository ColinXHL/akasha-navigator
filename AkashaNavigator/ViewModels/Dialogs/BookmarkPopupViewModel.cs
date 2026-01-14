using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Common;

namespace AkashaNavigator.ViewModels.Dialogs
{
/// <summary>
/// 收藏夹弹窗的 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class BookmarkPopupViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    /// <summary>
    /// 收藏列表
    /// </summary>
    public ObservableCollection<BookmarkItem> Bookmarks { get; } = new();

    /// <summary>
    /// 搜索文本（自动生成 SearchText 属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 是否为空（自动生成 IsEmpty 属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>
    /// 选择收藏项事件（由 Code-behind 订阅以关闭窗口）
    /// </summary>
    public event EventHandler<BookmarkItem?>? ItemSelected;

    /// <summary>
    /// 请求显示确认对话框事件（由 Code-behind 订阅以显示对话框）
    /// </summary>
    public event EventHandler<ConfirmDialogRequest>? ConfirmDialogRequested;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BookmarkPopupViewModel(IDataService dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        LoadBookmarks();
    }

    /// <summary>
    /// 搜索文本变化时重新加载（自动生成的方法）
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        LoadBookmarks();
    }

    /// <summary>
    /// 加载收藏
    /// </summary>
    public void LoadBookmarks()
    {
        var bookmarks = string.IsNullOrWhiteSpace(SearchText) ? _dataService.GetBookmarks()
                                                              : _dataService.SearchBookmarks(SearchText);

        Bookmarks.Clear();
        foreach (var item in bookmarks)
        {
            Bookmarks.Add(item);
        }

        IsEmpty = Bookmarks.Count == 0;
    }

    /// <summary>
    /// 清空全部收藏
    /// 触发确认对话框请求事件，由 View 显示对话框
    /// </summary>
    public void ClearAll()
    {
        ConfirmDialogRequested?.Invoke(this,
                                       new ConfirmDialogRequest { Message = "确定要清空所有收藏吗？此操作不可撤销。",
                                                                  Title = "确认清空", ConfirmText = "清空",
                                                                  CancelText = "取消", OnConfirmed = ExecuteClearAll });
    }

    /// <summary>
    /// 执行清空收藏操作
    /// </summary>
    private void ExecuteClearAll()
    {
        _dataService.ClearBookmarks();
        LoadBookmarks();
    }

    /// <summary>
    /// 删除指定收藏（自动生成 DeleteCommand）
    /// </summary>
    [RelayCommand]
    private void Delete(int id)
    {
        _dataService.DeleteBookmark(id);
        LoadBookmarks();
    }

    /// <summary>
    /// 选择收藏项（自动生成 SelectItemCommand）
    /// </summary>
    [RelayCommand]
    private void SelectItem(BookmarkItem? item)
    {
        ItemSelected?.Invoke(this, item);
    }
}
}
