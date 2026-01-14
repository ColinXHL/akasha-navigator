using System;
using System.Collections.Generic;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Dialogs;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels
{
/// <summary>
/// BookmarkPopupViewModel 单元测试
/// 测试书签弹窗的 ViewModel 逻辑
/// </summary>
public class BookmarkPopupViewModelTests
{
    /// <summary>
    /// 创建默认的 Mock DataService
    /// </summary>
    private static Mock<IDataService> CreateDefaultMockDataService()
    {
        var mock = new Mock<IDataService>(MockBehavior.Strict);
        mock.Setup(s => s.GetBookmarks()).Returns(new List<BookmarkItem>());
        mock.Setup(s => s.SearchBookmarks(It.IsAny<string>())).Returns(new List<BookmarkItem>());
        mock.Setup(s => s.DeleteBookmark(It.IsAny<int>()));
        mock.Setup(s => s.ClearBookmarks());
        return mock;
    }

#region 2.3.1 书签列表加载测试

    [Fact]
    public void Constructor_WithValidDataService_LoadsBookmarksOnCreation()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "书签2",
                                                        AddTime = DateTime.Now, SortOrder = 2 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        // Act
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Assert
        mockDataService.Verify(s => s.GetBookmarks(), Times.Once);
        Assert.Equal(2, viewModel.Bookmarks.Count);
    }

    [Fact]
    public void Constructor_WithEmptyBookmarks_SetsIsEmptyToTrue()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();

        // Act
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Assert
        Assert.Empty(viewModel.Bookmarks);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void Constructor_WithBookmarkItems_SetsIsEmptyToFalse()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        // Act
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Assert
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public void Constructor_WithNullDataService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BookmarkPopupViewModel(null!));
    }

    [Fact]
    public void LoadBookmarks_WithExistingBookmarks_ClearsAndReloadsItems()
    {
        // Arrange
        var initialItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "初始",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };
        var updatedItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "更新",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(initialItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.Single(viewModel.Bookmarks);

        // 设置返回更新后的数据
        mockDataService.Setup(s => s.GetBookmarks()).Returns(updatedItems);

        // Act
        viewModel.LoadBookmarks();

        // Assert
        Assert.Single(viewModel.Bookmarks);
        Assert.Equal("更新", viewModel.Bookmarks[0].Title);
        mockDataService.Verify(s => s.GetBookmarks(), Times.Exactly(2));
    }

    [Fact]
    public void LoadBookmarks_CalledTwice_ClearsPreviousItems()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "书签2",
                                                        AddTime = DateTime.Now, SortOrder = 2 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.Bookmarks.Count);

        // 设置返回空列表
        mockDataService.Setup(s => s.GetBookmarks()).Returns(new List<BookmarkItem>());

        // Act
        viewModel.LoadBookmarks();

        // Assert
        Assert.Empty(viewModel.Bookmarks);
        Assert.True(viewModel.IsEmpty);
    }

#endregion

#region 2.3.2 添加 / 删除书签测试

    [Fact]
    public void DeleteCommand_WithValidId_CallsDeleteBookmarkAndReloads()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "书签2",
                                                        AddTime = DateTime.Now, SortOrder = 2 } };
        var remainingItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.Bookmarks.Count);

        // 删除后只剩一条记录
        mockDataService.Setup(s => s.GetBookmarks()).Returns(remainingItems);

        // Act
        viewModel.DeleteCommand.Execute(2);

        // Assert
        mockDataService.Verify(s => s.DeleteBookmark(2), Times.Once);
        mockDataService.Verify(s => s.GetBookmarks(), Times.AtLeast(2)); // 初始加载 + 删除后重载
        Assert.Single(viewModel.Bookmarks);
    }

    [Fact]
    public void DeleteCommand_AfterDeletion_UpdatesIsEmptyState()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.False(viewModel.IsEmpty);

        // 删除后无记录
        mockDataService.Setup(s => s.GetBookmarks()).Returns(new List<BookmarkItem>());

        // Act
        viewModel.DeleteCommand.Execute(1);

        // Assert
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void DeleteCommand_WhenEmptyList_DoesNotThrow()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act & Assert - 不应抛出异常
        viewModel.DeleteCommand.Execute(999);

        mockDataService.Verify(s => s.DeleteBookmark(999), Times.Once);
    }

    [Fact]
    public void ClearAll_WhenNotEmpty_RaisesConfirmDialogRequestedEvent()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "书签2",
                                                        AddTime = DateTime.Now, SortOrder = 2 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.Bookmarks.Count);

        AkashaNavigator.ViewModels.Common.ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        // Act
        viewModel.ClearAll();

        // Assert - 验证事件被触发，但不执行清空
        Assert.NotNull(receivedRequest);
        Assert.Equal("确定要清空所有收藏吗？此操作不可撤销。", receivedRequest.Message);
        Assert.Equal("确认清空", receivedRequest.Title);
        Assert.Equal("清空", receivedRequest.ConfirmText);
        Assert.Equal("取消", receivedRequest.CancelText);
        Assert.NotNull(receivedRequest.OnConfirmed);
        // 清空操作尚未执行
        mockDataService.Verify(s => s.ClearBookmarks(), Times.Never);
    }

    [Fact]
    public void ClearAll_WhenConfirmed_CallsClearBookmarksAndReloads()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "书签2",
                                                        AddTime = DateTime.Now, SortOrder = 2 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.Bookmarks.Count);

        AkashaNavigator.ViewModels.Common.ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        // 清空后无记录
        mockDataService.Setup(s => s.GetBookmarks()).Returns(new List<BookmarkItem>());

        // Act - 触发 ClearAll 并执行确认回调
        viewModel.ClearAll();
        receivedRequest?.OnConfirmed?.Invoke();

        // Assert
        mockDataService.Verify(s => s.ClearBookmarks(), Times.Once);
        mockDataService.Verify(s => s.GetBookmarks(), Times.AtLeast(2));
        Assert.Empty(viewModel.Bookmarks);
    }

    [Fact]
    public void ClearAll_WhenNotConfirmed_DoesNotCallClearBookmarks()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        AkashaNavigator.ViewModels.Common.ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        // Act - 触发 ClearAll 但不执行确认回调（模拟用户取消）
        viewModel.ClearAll();
        // 不调用 receivedRequest?.OnConfirmed?.Invoke();

        // Assert - 清空操作未执行
        mockDataService.Verify(s => s.ClearBookmarks(), Times.Never);
        Assert.Single(viewModel.Bookmarks);
    }

    [Fact]
    public void ClearAll_AfterConfirmedClearing_SetsIsEmptyToTrue()
    {
        // Arrange
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);
        Assert.False(viewModel.IsEmpty);

        AkashaNavigator.ViewModels.Common.ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        mockDataService.Setup(s => s.GetBookmarks()).Returns(new List<BookmarkItem>());

        // Act
        viewModel.ClearAll();
        receivedRequest?.OnConfirmed?.Invoke();

        // Assert
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void ClearAll_WithNoEventSubscribers_DoesNotThrow()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act & Assert - 不应抛出异常
        viewModel.ClearAll();
    }

#endregion

#region 2.3.3 书签排序测试

    [Fact]
    public void LoadBookmarks_WithUnsortedBookmarks_MaintainsServiceOrder()
    {
        // Arrange - 模拟服务返回的书签按 SortOrder 排序
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "第一",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "第二",
                                                        AddTime = DateTime.Now, SortOrder = 2 },
                                     new BookmarkItem { Id = 3, Url = "https://example.com/3", Title = "第三",
                                                        AddTime = DateTime.Now, SortOrder = 3 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        // Act
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Assert - 验证顺序与服务返回一致
        Assert.Equal(3, viewModel.Bookmarks.Count);
        Assert.Equal("第一", viewModel.Bookmarks[0].Title);
        Assert.Equal("第二", viewModel.Bookmarks[1].Title);
        Assert.Equal("第三", viewModel.Bookmarks[2].Title);
    }

    [Fact]
    public void LoadBookmarks_WithCustomSortOrder_PreservesSortOrder()
    {
        // Arrange - 模拟自定义排序顺序（非连续）
        var bookmarkItems =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/z", Title = "最后",
                                                        AddTime = DateTime.Now, SortOrder = 100 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/a", Title = "最前",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 3, Url = "https://example.com/m", Title = "中间",
                                                        AddTime = DateTime.Now, SortOrder = 50 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(bookmarkItems);

        // Act
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Assert - 验证顺序与 SortOrder 一致
        Assert.Equal(3, viewModel.Bookmarks.Count);
        Assert.Equal(100, viewModel.Bookmarks[0].SortOrder);
        Assert.Equal(1, viewModel.Bookmarks[1].SortOrder);
        Assert.Equal(50, viewModel.Bookmarks[2].SortOrder);
    }

#endregion

#region 搜索功能测试

    [Fact]
    public void SearchText_WhenSet_ReloadsBookmarksWithSearch()
    {
        // Arrange
        var searchResults =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/search", Title = "搜索结果",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchBookmarks("游戏")).Returns(searchResults);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "游戏";

        // Assert
        mockDataService.Verify(s => s.SearchBookmarks("游戏"), Times.Once);
        Assert.Single(viewModel.Bookmarks);
        Assert.Equal("搜索结果", viewModel.Bookmarks[0].Title);
    }

    [Fact]
    public void SearchText_WithEmptySearchText_LoadsFullBookmarks()
    {
        // Arrange
        var fullBookmarks =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 },
                                     new BookmarkItem { Id = 2, Url = "https://example.com/2", Title = "书签2",
                                                        AddTime = DateTime.Now, SortOrder = 2 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(fullBookmarks);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act - 清空搜索文本
        viewModel.SearchText = "";

        // Assert
        mockDataService.Verify(s => s.GetBookmarks(), Times.AtLeastOnce);
        mockDataService.Verify(s => s.SearchBookmarks(It.IsAny<string>()), Times.Never);
        Assert.Equal(2, viewModel.Bookmarks.Count);
    }

    [Fact]
    public void SearchText_WithWhitespace_LoadsFullBookmarks()
    {
        // Arrange
        var fullBookmarks =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/1", Title = "书签1",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetBookmarks()).Returns(fullBookmarks);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "   ";

        // Assert
        mockDataService.Verify(s => s.GetBookmarks(), Times.AtLeastOnce);
        Assert.Single(viewModel.Bookmarks);
    }

    [Fact]
    public void SearchText_WithNoMatchingResults_SetsIsEmptyToTrue()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchBookmarks("不存在的关键词")).Returns(new List<BookmarkItem>());

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "不存在的关键词";

        // Assert
        Assert.Empty(viewModel.Bookmarks);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void SearchText_WithMatchingResults_SetsIsEmptyToFalse()
    {
        // Arrange
        var searchResults =
            new List<BookmarkItem> { new BookmarkItem { Id = 1, Url = "https://example.com/game", Title = "游戏攻略",
                                                        AddTime = DateTime.Now, SortOrder = 1 } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchBookmarks("游戏")).Returns(searchResults);

        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "游戏";

        // Assert
        Assert.False(viewModel.IsEmpty);
        Assert.Single(viewModel.Bookmarks);
    }

#endregion

#region 选择事件测试

    [Fact]
    public void SelectItemCommand_WithValidItem_RaisesItemSelectedEvent()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        BookmarkItem? selectedItem = null;
        viewModel.ItemSelected += (s, item) => selectedItem = item;

        var item = new BookmarkItem { Id = 1, Url = "https://example.com/video", Title = "视频教程",
                                      AddTime = DateTime.Now, SortOrder = 1 };

        // Act
        viewModel.SelectItemCommand.Execute(item);

        // Assert
        Assert.NotNull(selectedItem);
        Assert.Equal(1, selectedItem.Id);
        Assert.Equal("https://example.com/video", selectedItem.Url);
        Assert.Equal("视频教程", selectedItem.Title);
    }

    [Fact]
    public void SelectItemCommand_WithNullItem_RaisesEventWithNull()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        BookmarkItem? selectedItem = null;
        bool eventRaised = false;
        viewModel.ItemSelected += (s, item) =>
        {
            eventRaised = true;
            selectedItem = item;
        };

        // Act
        viewModel.SelectItemCommand.Execute(null);

        // Assert
        Assert.True(eventRaised);
        Assert.Null(selectedItem);
    }

    [Fact]
    public void SelectItemCommand_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new BookmarkPopupViewModel(mockDataService.Object);

        var item = new BookmarkItem { Id = 1, Url = "https://example.com/video", Title = "视频教程",
                                      AddTime = DateTime.Now, SortOrder = 1 };

        // Act & Assert - 不应抛出异常
        viewModel.SelectItemCommand.Execute(item);
    }

#endregion
}
}
