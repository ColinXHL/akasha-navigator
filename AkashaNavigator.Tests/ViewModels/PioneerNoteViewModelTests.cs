using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.ViewModels.Windows;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels
{
    /// <summary>
    /// PioneerNoteViewModel 单元测试
    /// 测试开荒笔记窗口的 ViewModel 逻辑
    /// </summary>
    public class PioneerNoteViewModelTests
    {
        private readonly Mock<IPioneerNoteService> _mockService;
        private readonly PioneerNoteViewModel _viewModel;

        public PioneerNoteViewModelTests()
        {
            _mockService = new Mock<IPioneerNoteService>(MockBehavior.Strict);
            SetupDefaultMockBehavior();
            _viewModel = new PioneerNoteViewModel(_mockService.Object);
        }

        /// <summary>
        /// 设置默认的 Mock 行为
        /// </summary>
        private void SetupDefaultMockBehavior()
        {
            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder>(),
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            _mockService.Setup(s => s.SearchNotes(It.IsAny<string>()))
                .Returns(new List<NoteItem>());

            _mockService.Setup(s => s.CurrentSortOrder)
                .Returns(SortDirection.Descending);

            _mockService.SetupSet(s => s.CurrentSortOrder = It.IsAny<SortDirection>())
                .Verifiable();

            _mockService.Setup(s => s.ToggleSortOrder())
                .Returns(SortDirection.Ascending);
        }

        #region 2.1.1 文件夹展开/折叠测试

        [Fact]
        public void LoadNoteTree_WithRootFolders_LoadsFoldersCorrectly()
        {
            // Arrange
            var folder1 = new NoteFolder { Id = "f1", Name = "目录1", Icon = "📁", CreatedTime = DateTime.Now, ParentId = null };
            var folder2 = new NoteFolder { Id = "f2", Name = "目录2", Icon = "📂", CreatedTime = DateTime.Now, ParentId = null };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder> { folder1, folder2 },
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            // Act
            _viewModel.LoadNoteTree();

            // Assert
            Assert.Equal(2, _viewModel.TreeNodes.Count);
            Assert.All(_viewModel.TreeNodes, node => Assert.True(node.IsFolder));
            Assert.Contains(_viewModel.TreeNodes, n => n.Title == "目录1");
            Assert.Contains(_viewModel.TreeNodes, n => n.Title == "目录2");
        }

        [Fact]
        public void LoadNoteTree_WithNestedFolders_BuildsTreeStructure()
        {
            // Arrange
            var parentFolder = new NoteFolder { Id = "f1", Name = "父目录", Icon = "📁", CreatedTime = DateTime.Now, ParentId = null };
            var childFolder = new NoteFolder { Id = "f2", Name = "子目录", Icon = "📁", CreatedTime = DateTime.Now, ParentId = "f1" };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder> { parentFolder, childFolder },
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            // Act
            _viewModel.LoadNoteTree();

            // Assert
            var parentNode = _viewModel.TreeNodes.FirstOrDefault(n => n.Id == "f1");
            Assert.NotNull(parentNode);
            Assert.NotNull(parentNode.Children);
            Assert.Single(parentNode.Children);
            Assert.Equal("子目录", parentNode.Children[0].Title);
        }

        [Fact]
        public void LoadNoteTree_WithRootItems_LoadsItemsCorrectly()
        {
            // Arrange
            var item1 = new NoteItem { Id = "i1", Title = "笔记1", Url = "https://example.com/1", RecordedTime = DateTime.Now, FolderId = null };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder>(),
                Items = new List<NoteItem> { item1 },
                SortOrder = SortDirection.Descending
            });

            // Act
            _viewModel.LoadNoteTree();

            // Assert
            Assert.Single(_viewModel.TreeNodes);
            var itemNode = _viewModel.TreeNodes[0];
            Assert.False(itemNode.IsFolder);
            Assert.Equal("笔记1", itemNode.Title);
            Assert.Equal("https://example.com/1", itemNode.Url);
            Assert.Equal("🔗", itemNode.Icon);
        }

        [Fact]
        public void LoadNoteTree_WithItemsInFolder_LoadsItemsAsChildren()
        {
            // Arrange
            var folder = new NoteFolder { Id = "f1", Name = "目录", Icon = "📁", CreatedTime = DateTime.Now, ParentId = null };
            var item = new NoteItem { Id = "i1", Title = "笔记", Url = "https://example.com/1", RecordedTime = DateTime.Now, FolderId = "f1" };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder> { folder },
                Items = new List<NoteItem> { item },
                SortOrder = SortDirection.Descending
            });

            // Act
            _viewModel.LoadNoteTree();

            // Assert
            var folderNode = _viewModel.TreeNodes.FirstOrDefault(n => n.Id == "f1");
            Assert.NotNull(folderNode);
            Assert.NotNull(folderNode.Children);
            Assert.Single(folderNode.Children);
            Assert.False(folderNode.Children[0].IsFolder);
            Assert.Equal("笔记", folderNode.Children[0].Title);
        }

        [Fact]
        public void LoadNoteTree_WithEmptyData_SetsIsEmptyToTrue()
        {
            // Arrange
            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder>(),
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            // Act
            _viewModel.LoadNoteTree();

            // Assert
            Assert.Empty(_viewModel.TreeNodes);
            Assert.True(_viewModel.IsEmpty);
        }

        [Fact]
        public void LoadNoteTree_WithData_SetsIsEmptyToFalse()
        {
            // Arrange
            var folder = new NoteFolder { Id = "f1", Name = "目录", Icon = "📁", CreatedTime = DateTime.Now, ParentId = null };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder> { folder },
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            // Act
            _viewModel.LoadNoteTree();

            // Assert
            Assert.False(_viewModel.IsEmpty);
        }

        #endregion

        #region 2.1.2 笔记选择测试

        [Fact]
        public void SelectNodeCommand_WithValidNode_RaisesNodeSelectedEvent()
        {
            // Arrange
            NoteTreeNode? selectedNode = null;
            _viewModel.NodeSelected += (s, node) => selectedNode = node;

            var node = new NoteTreeNode { Id = "i1", Title = "笔记", IsFolder = false, RecordedTime = DateTime.Now };

            // Act
            _viewModel.SelectNodeCommand.Execute(node);

            // Assert
            Assert.NotNull(selectedNode);
            Assert.Equal("i1", selectedNode.Id);
            Assert.Equal("笔记", selectedNode.Title);
        }

        [Fact]
        public void SelectNodeCommand_WithNullNode_RaisesEventWithNullNode()
        {
            // Arrange
            NoteTreeNode? selectedNode = null;
            bool eventRaised = false;
            _viewModel.NodeSelected += (s, node) =>
            {
                eventRaised = true;
                selectedNode = node;
            };

            // Act
            _viewModel.SelectNodeCommand.Execute(null);

            // Assert - 事件总是被触发，即使 node 是 null
            Assert.True(eventRaised);
            Assert.Null(selectedNode);
        }

        #endregion

        #region 2.1.3 搜索过滤测试

        [Fact]
        public void SearchKeyword_WhenSet_ReloadsTree()
        {
            // Arrange - 设置一个包含数据的初始状态
            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder>(),
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            // 清空并重新创建 ViewModel 以设置初始状态
            _viewModel.LoadNoteTree();
            int initialCount = _viewModel.TreeNodes.Count;

            // 设置搜索时返回空结果
            _mockService.Setup(s => s.SearchNotes("测试"))
                .Returns(new List<NoteItem>());

            // Act
            _viewModel.SearchKeyword = "测试";

            // Assert
            _mockService.Verify(s => s.SearchNotes("测试"), Times.Once);
        }

        [Fact]
        public void SearchKeyword_WithMatchingResults_DisplaysMatchingItems()
        {
            // Arrange
            var matchingItem = new NoteItem
            {
                Id = "i1",
                Title = "游戏攻略",
                Url = "https://example.com/game",
                RecordedTime = DateTime.Now,
                FolderId = null
            };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder>(),
                Items = new List<NoteItem> { matchingItem },
                SortOrder = SortDirection.Descending
            });

            _mockService.Setup(s => s.SearchNotes("游戏"))
                .Returns(new List<NoteItem> { matchingItem });

            // Act
            _viewModel.SearchKeyword = "游戏";

            // Assert
            Assert.Single(_viewModel.TreeNodes);
            Assert.Equal("游戏攻略", _viewModel.TreeNodes[0].Title);
        }

        [Fact]
        public void SearchKeyword_WithNoResults_SetsEmptyHintText()
        {
            // Arrange
            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder>(),
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            _mockService.Setup(s => s.SearchNotes("不存在的关键词"))
                .Returns(new List<NoteItem>());

            // Act
            _viewModel.SearchKeyword = "不存在的关键词";

            // Assert
            Assert.True(_viewModel.IsEmpty);
            Assert.Equal("未找到匹配的笔记", _viewModel.EmptyHintText);
        }

        [Fact]
        public void SearchKeyword_WhenCleared_RestoresFullTree()
        {
            // Arrange
            var folder = new NoteFolder
            {
                Id = "f1",
                Name = "目录1",
                Icon = "📁",
                CreatedTime = DateTime.Now,
                ParentId = null
            };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder> { folder },
                Items = new List<NoteItem>(),
                SortOrder = SortDirection.Descending
            });

            // 先搜索（空结果）
            _mockService.Setup(s => s.SearchNotes(It.IsAny<string>()))
                .Returns(new List<NoteItem>());

            // Act
            _viewModel.SearchKeyword = "测试";
            Assert.True(_viewModel.IsEmpty);

            // 清空搜索
            _viewModel.SearchKeyword = "";

            // Assert - 重新加载了完整树
            Assert.Single(_viewModel.TreeNodes);
            Assert.Equal("目录1", _viewModel.TreeNodes[0].Title);
            Assert.False(_viewModel.IsEmpty);
        }

        [Fact]
        public void SearchKeyword_WithFolderHierarchy_IncludesParentFolders()
        {
            // Arrange
            var parentFolder = new NoteFolder
            {
                Id = "f1",
                Name = "父目录",
                Icon = "📁",
                CreatedTime = DateTime.Now,
                ParentId = null
            };
            var childFolder = new NoteFolder
            {
                Id = "f2",
                Name = "子目录",
                Icon = "📁",
                CreatedTime = DateTime.Now,
                ParentId = "f1"
            };
            var matchingItem = new NoteItem
            {
                Id = "i1",
                Title = "匹配的笔记",
                Url = "https://example.com/match",
                RecordedTime = DateTime.Now,
                FolderId = "f2"
            };

            _mockService.Setup(s => s.GetNoteTree()).Returns(new PioneerNoteData
            {
                Folders = new List<NoteFolder> { parentFolder, childFolder },
                Items = new List<NoteItem> { matchingItem },
                SortOrder = SortDirection.Descending
            });

            _mockService.Setup(s => s.SearchNotes("匹配"))
                .Returns(new List<NoteItem> { matchingItem });

            // Act
            _viewModel.SearchKeyword = "匹配";

            // Assert - 应该显示父目录结构
            var rootNode = _viewModel.TreeNodes.FirstOrDefault(n => n.Id == "f1");
            Assert.NotNull(rootNode);
            Assert.NotNull(rootNode.Children);
        }

        #endregion

        #region 2.1.4 命令执行测试

        [Fact]
        public void ToggleSortCommand_WhenExecuted_TogglesSortOrder()
        {
            // Act
            _viewModel.ToggleSortCommand.Execute(null);

            // Assert
            _mockService.Verify(s => s.ToggleSortOrder(), Times.Once);
        }

        [Fact]
        public void ToggleSortCommand_AfterToggling_UpdatesSortButtonText()
        {
            // Arrange
            _mockService.Setup(s => s.ToggleSortOrder())
                .Returns(SortDirection.Ascending);

            _mockService.Setup(s => s.CurrentSortOrder)
                .Returns(SortDirection.Ascending);

            // Act
            _viewModel.ToggleSortCommand.Execute(null);

            // Assert
            Assert.Equal("↑ 最早", _viewModel.SortButtonText);
        }

        [Fact]
        public void ToggleSortCommand_WhenDescending_DisplaysDescendingText()
        {
            // Arrange
            _mockService.Setup(s => s.CurrentSortOrder)
                .Returns(SortDirection.Descending);

            // 创建新的 ViewModel 以反映新的排序状态
            var viewModel = new PioneerNoteViewModel(_mockService.Object);

            // Assert
            Assert.Equal("↓ 最新", viewModel.SortButtonText);
        }

        [Fact]
        public void NewFolderCommand_WhenExecuted_RaisesShowNewFolderDialogRequestedEvent()
        {
            // Arrange
            string? requestedParentId = null;
            _viewModel.ShowNewFolderDialogRequested += (s, parentId) => requestedParentId = parentId;

            // Act
            _viewModel.NewFolderCommand.Execute("parent123");

            // Assert
            Assert.Equal("parent123", requestedParentId);
        }

        [Fact]
        public void RecordNoteCommand_WhenExecuted_RaisesShowRecordNoteDialogRequestedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _viewModel.ShowRecordNoteDialogRequested += (s, e) => eventRaised = true;

            // Act
            _viewModel.RecordNoteCommand.Execute(null);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void EditNodeCommand_WithValidNode_RaisesShowEditDialogRequestedEvent()
        {
            // Arrange
            NoteTreeNode? requestedNode = null;
            _viewModel.ShowEditDialogRequested += (s, node) => requestedNode = node;

            var node = new NoteTreeNode { Id = "i1", Title = "笔记", IsFolder = false, RecordedTime = DateTime.Now };

            // Act
            _viewModel.EditNodeCommand.Execute(node);

            // Assert
            Assert.NotNull(requestedNode);
            Assert.Equal("i1", requestedNode.Id);
        }

        [Fact]
        public void EditNodeCommand_WithNullNode_DoesNotRaiseEvent()
        {
            // Arrange
            bool eventRaised = false;
            _viewModel.ShowEditDialogRequested += (s, node) => eventRaised = true;

            // Act
            _viewModel.EditNodeCommand.Execute(null);

            // Assert
            Assert.False(eventRaised);
        }

        [Fact]
        public void DeleteNodeCommand_WithValidNode_RaisesShowDeleteConfirmRequestedEvent()
        {
            // Arrange
            NoteTreeNode? requestedNode = null;
            _viewModel.ShowDeleteConfirmRequested += (s, node) => requestedNode = node;

            var node = new NoteTreeNode { Id = "f1", Title = "目录", IsFolder = true, RecordedTime = DateTime.Now };

            // Act
            _viewModel.DeleteNodeCommand.Execute(node);

            // Assert
            Assert.NotNull(requestedNode);
            Assert.Equal("f1", requestedNode.Id);
        }

        [Fact]
        public void DeleteNodeCommand_WithNullNode_DoesNotRaiseEvent()
        {
            // Arrange
            bool eventRaised = false;
            _viewModel.ShowDeleteConfirmRequested += (s, node) => eventRaised = true;

            // Act
            _viewModel.DeleteNodeCommand.Execute(null);

            // Assert
            Assert.False(eventRaised);
        }

        [Fact]
        public void MoveNodeCommand_WithValidNode_RaisesShowMoveDialogRequestedEvent()
        {
            // Arrange
            NoteTreeNode? requestedNode = null;
            _viewModel.ShowMoveDialogRequested += (s, node) => requestedNode = node;

            var node = new NoteTreeNode { Id = "i1", Title = "笔记", IsFolder = false, RecordedTime = DateTime.Now };

            // Act
            _viewModel.MoveNodeCommand.Execute(node);

            // Assert
            Assert.NotNull(requestedNode);
            Assert.Equal("i1", requestedNode.Id);
        }

        [Fact]
        public void MoveNodeCommand_WithNullNode_DoesNotRaiseEvent()
        {
            // Arrange
            bool eventRaised = false;
            _viewModel.ShowMoveDialogRequested += (s, node) => eventRaised = true;

            // Act
            _viewModel.MoveNodeCommand.Execute(null);

            // Assert
            Assert.False(eventRaised);
        }

        #endregion
    }
}
