using System;
using System.Collections.Generic;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Common;
using AkashaNavigator.ViewModels.Windows;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels
{
/// <summary>
/// HistoryWindowViewModel 单元测试
/// 测试历史记录窗口的 ViewModel 逻辑
/// </summary>
public class HistoryWindowViewModelTests
{
    /// <summary>
    /// 创建默认的 Mock DataService
    /// </summary>
    private static Mock<IDataService> CreateDefaultMockDataService()
    {
        var mock = new Mock<IDataService>(MockBehavior.Strict);
        mock.Setup(s => s.GetHistory()).Returns(new List<HistoryItem>());
        mock.Setup(s => s.SearchHistory(It.IsAny<string>())).Returns(new List<HistoryItem>());
        mock.Setup(s => s.DeleteHistory(It.IsAny<int>()));
        mock.Setup(s => s.ClearHistory());
        return mock;
    }

#region 2.2.1 历史记录加载测试

    [Fact]
    public void Constructor_WithValidDataService_LoadsHistoryOnCreation()
    {
        // Arrange
        var historyItems = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        // Act
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Assert
        mockDataService.Verify(s => s.GetHistory(), Times.Once);
        Assert.Equal(2, viewModel.HistoryItems.Count);
    }

    [Fact]
    public void Constructor_WithEmptyHistory_SetsIsEmptyToTrue()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();

        // Act
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Assert
        Assert.Empty(viewModel.HistoryItems);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void Constructor_WithHistoryItems_SetsIsEmptyToFalse()
    {
        // Arrange
        var historyItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        // Act
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Assert
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public void LoadHistory_WithExistingHistory_ClearsAndReloadsItems()
    {
        // Arrange
        var initialItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "初始", VisitTime = DateTime.Now } };
        var updatedItems = new List<HistoryItem> { new HistoryItem { Id = 2, Url = "https://example.com/2",
                                                                     Title = "更新", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(initialItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.Single(viewModel.HistoryItems);

        // 设置返回更新后的数据
        mockDataService.Setup(s => s.GetHistory()).Returns(updatedItems);

        // Act
        viewModel.LoadHistory();

        // Assert
        Assert.Single(viewModel.HistoryItems);
        Assert.Equal("更新", viewModel.HistoryItems[0].Title);
        mockDataService.Verify(s => s.GetHistory(), Times.Exactly(2));
    }

    [Fact]
    public void LoadHistory_CalledTwice_ClearsPreviousItems()
    {
        // Arrange
        var historyItems = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.HistoryItems.Count);

        // 设置返回空列表
        mockDataService.Setup(s => s.GetHistory()).Returns(new List<HistoryItem>());

        // Act
        viewModel.LoadHistory();

        // Assert
        Assert.Empty(viewModel.HistoryItems);
        Assert.True(viewModel.IsEmpty);
    }

#endregion

#region 2.2.2 搜索过滤测试

    [Fact]
    public void SearchText_WhenSet_ReloadsHistoryWithSearch()
    {
        // Arrange
        var searchResults = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/search",
                                                                      Title = "搜索结果", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchHistory("游戏")).Returns(searchResults);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "游戏";

        // Assert
        mockDataService.Verify(s => s.SearchHistory("游戏"), Times.Once);
        Assert.Single(viewModel.HistoryItems);
        Assert.Equal("搜索结果", viewModel.HistoryItems[0].Title);
    }

    [Fact]
    public void SearchText_WithEmptySearchText_LoadsFullHistory()
    {
        // Arrange
        var fullHistory = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(fullHistory);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act - 清空搜索文本
        viewModel.SearchText = "";

        // Assert
        mockDataService.Verify(s => s.GetHistory(), Times.AtLeastOnce);
        mockDataService.Verify(s => s.SearchHistory(It.IsAny<string>()), Times.Never);
        Assert.Equal(2, viewModel.HistoryItems.Count);
    }

    [Fact]
    public void SearchText_WithWhitespace_LoadsFullHistory()
    {
        // Arrange
        var fullHistory = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                    Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(fullHistory);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "   ";

        // Assert
        mockDataService.Verify(s => s.GetHistory(), Times.AtLeastOnce);
        Assert.Single(viewModel.HistoryItems);
    }

    [Fact]
    public void SearchText_WithNoMatchingResults_SetsIsEmptyToTrue()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchHistory("不存在的关键词")).Returns(new List<HistoryItem>());

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "不存在的关键词";

        // Assert
        Assert.Empty(viewModel.HistoryItems);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void SearchText_WithMatchingResults_SetsIsEmptyToFalse()
    {
        // Arrange
        var searchResults = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/game",
                                                                      Title = "游戏攻略", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchHistory("游戏")).Returns(searchResults);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act
        viewModel.SearchText = "游戏";

        // Assert
        Assert.False(viewModel.IsEmpty);
        Assert.Single(viewModel.HistoryItems);
    }

    [Fact]
    public void SearchText_ChangedFromSearchToEmpty_ReloadsFullHistory()
    {
        // Arrange
        var searchResults = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/search",
                                                                      Title = "搜索结果", VisitTime = DateTime.Now } };
        var fullHistory = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.SearchHistory(It.IsAny<string>())).Returns(searchResults);
        mockDataService.Setup(s => s.GetHistory()).Returns(fullHistory);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act - 先搜索
        viewModel.SearchText = "搜索";
        Assert.Single(viewModel.HistoryItems);

        // 清空搜索
        viewModel.SearchText = "";

        // Assert
        Assert.Equal(2, viewModel.HistoryItems.Count);
        mockDataService.Verify(s => s.GetHistory(), Times.AtLeastOnce);
    }

#endregion

#region 2.2.3 删除操作测试

    [Fact]
    public void DeleteCommand_WithValidId_CallsDeleteHistoryAndReloads()
    {
        // Arrange
        var historyItems = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };
        var remainingItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                       Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.HistoryItems.Count);

        // 删除后只剩一条记录
        mockDataService.Setup(s => s.GetHistory()).Returns(remainingItems);

        // Act
        viewModel.DeleteCommand.Execute(2);

        // Assert
        mockDataService.Verify(s => s.DeleteHistory(2), Times.Once);
        mockDataService.Verify(s => s.GetHistory(), Times.AtLeast(2)); // 初始加载 + 删除后重载
        Assert.Single(viewModel.HistoryItems);
    }

    [Fact]
    public void DeleteCommand_AfterDeletion_UpdatesIsEmptyState()
    {
        // Arrange
        var historyItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.False(viewModel.IsEmpty);

        // 删除后无记录
        mockDataService.Setup(s => s.GetHistory()).Returns(new List<HistoryItem>());

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

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act & Assert - 不应抛出异常
        viewModel.DeleteCommand.Execute(999);

        mockDataService.Verify(s => s.DeleteHistory(999), Times.Once);
    }

    [Fact]
    public void ClearAllCommand_WhenNotEmpty_RaisesConfirmDialogRequestedEvent()
    {
        // Arrange
        var historyItems = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.Equal(2, viewModel.HistoryItems.Count);

        ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        // Act
        viewModel.ClearAllCommand.Execute(null);

        // Assert - 应触发确认对话框请求事件，而不是直接清空
        Assert.NotNull(receivedRequest);
        Assert.Equal("确定要清空所有历史记录吗？此操作不可撤销。", receivedRequest.Message);
        Assert.Equal("确认清空", receivedRequest.Title);
        Assert.Equal("清空", receivedRequest.ConfirmText);
        Assert.Equal("取消", receivedRequest.CancelText);
        Assert.NotNull(receivedRequest.OnConfirmed);
        // 数据服务的 ClearHistory 不应被直接调用
        mockDataService.Verify(s => s.ClearHistory(), Times.Never);
    }

    [Fact]
    public void ClearAllCommand_WhenConfirmed_CallsClearHistoryAndReloads()
    {
        // Arrange
        var historyItems = new List<HistoryItem> {
            new HistoryItem { Id = 1, Url = "https://example.com/1", Title = "页面1", VisitTime = DateTime.Now },
            new HistoryItem { Id = 2, Url = "https://example.com/2", Title = "页面2", VisitTime = DateTime.Now }
        };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        // 清空后无记录
        mockDataService.Setup(s => s.GetHistory()).Returns(new List<HistoryItem>());

        // Act - 执行命令
        viewModel.ClearAllCommand.Execute(null);

        // 模拟用户确认
        Assert.NotNull(receivedRequest?.OnConfirmed);
        receivedRequest.OnConfirmed();

        // Assert
        mockDataService.Verify(s => s.ClearHistory(), Times.Once);
        mockDataService.Verify(s => s.GetHistory(), Times.AtLeast(2));
        Assert.Empty(viewModel.HistoryItems);
    }

    [Fact]
    public void ClearAllCommand_WhenCancelled_DoesNotCallClearHistory()
    {
        // Arrange
        var historyItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        // Act - 执行命令但不调用 OnConfirmed（模拟用户取消）
        viewModel.ClearAllCommand.Execute(null);

        // Assert - 不调用 OnConfirmed，数据不应被清空
        Assert.NotNull(receivedRequest);
        mockDataService.Verify(s => s.ClearHistory(), Times.Never);
        Assert.Single(viewModel.HistoryItems);
    }

    [Fact]
    public void ClearAllCommand_AfterConfirmedClearing_SetsIsEmptyToTrue()
    {
        // Arrange
        var historyItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.False(viewModel.IsEmpty);

        ConfirmDialogRequest? receivedRequest = null;
        viewModel.ConfirmDialogRequested += (s, request) => receivedRequest = request;

        mockDataService.Setup(s => s.GetHistory()).Returns(new List<HistoryItem>());

        // Act
        viewModel.ClearAllCommand.Execute(null);
        receivedRequest?.OnConfirmed?.Invoke();

        // Assert
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void ClearAllCommand_WhenEmpty_CannotExecute()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.True(viewModel.IsEmpty);

        // Act
        bool canExecute = viewModel.ClearAllCommand.CanExecute(null);

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public void ClearAllCommand_WithItems_CanExecute()
    {
        // Arrange
        var historyItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        // Act
        bool canExecute = viewModel.ClearAllCommand.CanExecute(null);

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void ClearAllCommand_CanExecuteChangesWhenIsEmptyChanges()
    {
        // Arrange
        var historyItems = new List<HistoryItem> { new HistoryItem { Id = 1, Url = "https://example.com/1",
                                                                     Title = "页面1", VisitTime = DateTime.Now } };

        var mockDataService = CreateDefaultMockDataService();
        mockDataService.Setup(s => s.GetHistory()).Returns(historyItems);

        var viewModel = new HistoryWindowViewModel(mockDataService.Object);
        Assert.True(viewModel.ClearAllCommand.CanExecute(null));

        // 删除所有项
        mockDataService.Setup(s => s.GetHistory()).Returns(new List<HistoryItem>());

        // Act
        viewModel.DeleteCommand.Execute(1);

        // Assert
        Assert.False(viewModel.ClearAllCommand.CanExecute(null));
    }

#endregion

#region 2.2.4 导航命令测试

    [Fact]
    public void SelectItemCommand_WithValidItem_RaisesItemSelectedEvent()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        HistoryItem? selectedItem = null;
        viewModel.ItemSelected += (s, item) => selectedItem = item;

        var item =
            new HistoryItem { Id = 1, Url = "https://example.com/video", Title = "视频教程", VisitTime = DateTime.Now };

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
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        HistoryItem? selectedItem = null;
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
    public void SelectItemCommand_MultipleSubscribers_AllReceiveEvent()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        HistoryItem? selectedItem1 = null;
        HistoryItem? selectedItem2 = null;
        int callCount = 0;

        var item =
            new HistoryItem { Id = 1, Url = "https://example.com/video", Title = "视频教程", VisitTime = DateTime.Now };

        viewModel.ItemSelected += (s, i) =>
        {
            callCount++;
            selectedItem1 = i;
        };
        viewModel.ItemSelected += (s, i) =>
        {
            callCount++;
            selectedItem2 = i;
        };

        // Act
        viewModel.SelectItemCommand.Execute(item);

        // Assert
        Assert.Equal(2, callCount);
        Assert.NotNull(selectedItem1);
        Assert.NotNull(selectedItem2);
        Assert.Equal(item.Id, selectedItem1?.Id);
        Assert.Equal(item.Id, selectedItem2?.Id);
    }

    [Fact]
    public void SelectItemCommand_WithItemContainingFullData_PreservesAllProperties()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        HistoryItem? selectedItem = null;
        viewModel.ItemSelected += (s, item) => selectedItem = item;

        var testTime = new DateTime(2026, 1, 9, 12, 30, 0);
        var item = new HistoryItem { Id = 42, Url = "https://example.com/test", Title = "测试页面",
                                     VisitTime = testTime, VisitCount = 5 };

        // Act
        viewModel.SelectItemCommand.Execute(item);

        // Assert
        Assert.NotNull(selectedItem);
        Assert.Equal(42, selectedItem.Id);
        Assert.Equal("https://example.com/test", selectedItem.Url);
        Assert.Equal("测试页面", selectedItem.Title);
        Assert.Equal(testTime, selectedItem.VisitTime);
        Assert.Equal(5, selectedItem.VisitCount);
    }

    [Fact]
    public void SelectItemCommand_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var mockDataService = CreateDefaultMockDataService();
        var viewModel = new HistoryWindowViewModel(mockDataService.Object);

        var item =
            new HistoryItem { Id = 1, Url = "https://example.com/video", Title = "视频教程", VisitTime = DateTime.Now };

        // Act & Assert - 不应抛出异常
        viewModel.SelectItemCommand.Execute(item);
    }

#endregion
}
}
