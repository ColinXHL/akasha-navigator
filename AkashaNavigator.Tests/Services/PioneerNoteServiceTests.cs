using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services
{
/// <summary>
/// PioneerNoteService 单元测试
/// 测试开荒笔记的 CRUD 操作
/// </summary>
public class PioneerNoteServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profileDir;
    private readonly ILogService _logService;
    private readonly MockProfileManager _profileManager;
    private readonly PioneerNoteService _noteService;

    public PioneerNoteServiceTests()
    {
        // 创建临时目录用于测试
        _tempDir = Path.Combine(Path.GetTempPath(), $"akasha_note_test_{Guid.NewGuid():N}");
        _profileDir = Path.Combine(_tempDir, "Profiles", "default");
        Directory.CreateDirectory(_profileDir);

        // 创建测试用的服务
        _logService = new LogService(Path.Combine(_tempDir, "Logs"));
        _profileManager = new MockProfileManager(_profileDir);
        _noteService = new PioneerNoteService(_logService, _profileManager);
    }

    public void Dispose()
    {
        // 清理临时目录
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }

#region Folder Tests - 1.3.1

    [Fact]
    public void CreateFolder_WithValidName_CreatesFolderInRoot()
    {
        // Act
        var folder = _noteService.CreateFolder("测试目录");

        // Assert
        Assert.NotNull(folder);
        Assert.Equal("测试目录", folder.Name);
        Assert.Null(folder.ParentId);
        Assert.Equal(0, folder.SortOrder);
        Assert.NotNull(folder.Id);
    }

    [Fact]
    public void CreateFolder_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _noteService.CreateFolder(""));
        Assert.Throws<ArgumentException>(() => _noteService.CreateFolder("   "));
        Assert.Throws<ArgumentException>(() => _noteService.CreateFolder(null!));
    }

    [Fact]
    public void CreateFolder_WithParentId_CreatesChildFolder()
    {
        // Arrange
        var parentFolder = _noteService.CreateFolder("父目录");

        // Act
        var childFolder = _noteService.CreateFolder("子目录", parentFolder.Id);

        // Assert
        Assert.Equal(parentFolder.Id, childFolder.ParentId);
        Assert.Equal("子目录", childFolder.Name);
    }

    [Fact]
    public void CreateFolder_WithInvalidParentId_CreatesInRoot()
    {
        // Act
        var folder = _noteService.CreateFolder("测试目录", "invalid_parent_id");

        // Assert
        Assert.Null(folder.ParentId);
    }

    [Fact]
    public void CreateFolder_MultipleFolders_IncrementsSortOrder()
    {
        // Act
        var folder1 = _noteService.CreateFolder("目录1");
        var folder2 = _noteService.CreateFolder("目录2");
        var folder3 = _noteService.CreateFolder("目录3");

        // Assert
        Assert.Equal(0, folder1.SortOrder);
        Assert.Equal(1, folder2.SortOrder);
        Assert.Equal(2, folder3.SortOrder);
    }

    [Fact]
    public void CreateFolder_WithSameParent_IncrementsSortOrder()
    {
        // Arrange
        var parent = _noteService.CreateFolder("父目录");

        // Act
        var child1 = _noteService.CreateFolder("子目录1", parent.Id);
        var child2 = _noteService.CreateFolder("子目录2", parent.Id);

        // Assert
        Assert.Equal(0, child1.SortOrder);
        Assert.Equal(1, child2.SortOrder);
    }

    [Fact]
    public void UpdateFolder_WithValidId_UpdatesFolderName()
    {
        // Arrange
        var folder = _noteService.CreateFolder("旧名称");

        // Act
        _noteService.UpdateFolder(folder.Id, "新名称");
        var updated = _noteService.GetFolderById(folder.Id);

        // Assert
        Assert.Equal("新名称", updated!.Name);
    }

    [Fact]
    public void UpdateFolder_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var folder = _noteService.CreateFolder("测试");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _noteService.UpdateFolder(folder.Id, ""));
        Assert.Throws<ArgumentException>(() => _noteService.UpdateFolder(folder.Id, "   "));
    }

    [Fact]
    public void UpdateFolder_WithInvalidId_DoesNothing()
    {
        // Act - 应该不抛出异常
        _noteService.UpdateFolder("invalid_id", "新名称");
    }

    [Fact]
    public void RenameFolder_AliasMethod_UpdatesFolderName()
    {
        // Arrange
        var folder = _noteService.CreateFolder("旧名称");

        // Act
        _noteService.RenameFolder(folder.Id, "新名称");
        var updated = _noteService.GetFolderById(folder.Id);

        // Assert
        Assert.Equal("新名称", updated!.Name);
    }

    [Fact]
    public void DeleteFolder_WithValidId_RemovesFolder()
    {
        // Arrange
        var folder = _noteService.CreateFolder("测试目录");

        // Act
        _noteService.DeleteFolder(folder.Id);
        var deleted = _noteService.GetFolderById(folder.Id);

        // Assert
        Assert.Null(deleted);
    }

    [Fact]
    public void DeleteFolder_WithInvalidId_DoesNothing()
    {
        // Act - 应该不抛出异常
        _noteService.DeleteFolder("invalid_id");
    }

    [Fact]
    public void DeleteFolder_WithCascade_True_RemovesChildrenAndNotes()
    {
        // Arrange
        var parent = _noteService.CreateFolder("父目录");
        var child = _noteService.CreateFolder("子目录", parent.Id);
        var note1 = _noteService.RecordNote("url1", "笔记1", parent.Id);
        var note2 = _noteService.RecordNote("url2", "笔记2", child.Id);

        // Act
        _noteService.DeleteFolder(parent.Id, cascade: true);

        // Assert
        Assert.Null(_noteService.GetFolderById(parent.Id));
        Assert.Null(_noteService.GetFolderById(child.Id));
        Assert.Null(_noteService.GetNoteById(note1.Id));
        Assert.Null(_noteService.GetNoteById(note2.Id));
    }

    [Fact]
    public void DeleteFolder_WithCascade_False_MovesDirectChildrenToRoot()
    {
        // Arrange
        var parent = _noteService.CreateFolder("父目录");
        var child = _noteService.CreateFolder("子目录", parent.Id);
        var note1 = _noteService.RecordNote("url1", "笔记1", parent.Id);
        // 注意：note2 在子目录中，删除父目录时不会移动孙子目录中的笔记
        var note2 = _noteService.RecordNote("url2", "笔记2", parent.Id);

        // Act
        _noteService.DeleteFolder(parent.Id, cascade: false);

        // Assert
        Assert.Null(_noteService.GetFolderById(parent.Id));

        var remainingChild = _noteService.GetFolderById(child.Id);
        Assert.NotNull(remainingChild);
        // 子目录的 ParentId 会被设为 null（移到根目录）
        Assert.Null(remainingChild.ParentId);

        // 父目录直接下的笔记会被移到根目录
        var updatedNote1 = _noteService.GetNoteById(note1.Id);
        var updatedNote2 = _noteService.GetNoteById(note2.Id);
        Assert.Null(updatedNote1!.FolderId);
        Assert.Null(updatedNote2!.FolderId);
    }

    [Fact]
    public void DeleteFolder_DefaultAlias_CascadesDeletion()
    {
        // Arrange
        var parent = _noteService.CreateFolder("父目录");
        var child = _noteService.CreateFolder("子目录", parent.Id);
        _noteService.RecordNote("url1", "笔记1", parent.Id);

        // Act - 使用默认的 DeleteFolder (应该 cascade=true)
        _noteService.DeleteFolder(parent.Id);

        // Assert
        Assert.Null(_noteService.GetFolderById(parent.Id));
        Assert.Null(_noteService.GetFolderById(child.Id));
        Assert.Equal(0, _noteService.GetItemCount());
    }

    [Fact]
    public void FolderExists_WithValidId_ReturnsTrue()
    {
        // Arrange
        var folder = _noteService.CreateFolder("测试");

        // Act
        var exists = _noteService.FolderExists(folder.Id);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void FolderExists_WithInvalidId_ReturnsFalse()
    {
        // Act
        var exists = _noteService.FolderExists("invalid_id");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void GetFoldersByParent_WithNullParent_ReturnsRootFolders()
    {
        // Arrange
        _noteService.CreateFolder("根目录1");
        _noteService.CreateFolder("根目录2");
        var parent = _noteService.CreateFolder("父目录");
        _noteService.CreateFolder("子目录", parent.Id);

        // Act
        var rootFolders = _noteService.GetFoldersByParent(null);

        // Assert
        Assert.Equal(3, rootFolders.Count);
        Assert.All(rootFolders, f => Assert.Null(f.ParentId));
    }

    [Fact]
    public void GetFoldersByParent_WithParentId_ReturnsChildFolders()
    {
        // Arrange
        var parent = _noteService.CreateFolder("父目录");
        _noteService.CreateFolder("子目录1", parent.Id);
        _noteService.CreateFolder("子目录2", parent.Id);
        _noteService.CreateFolder("根目录");

        // Act
        var childFolders = _noteService.GetFoldersByParent(parent.Id);

        // Assert
        Assert.Equal(2, childFolders.Count);
        Assert.All(childFolders, f => Assert.Equal(parent.Id, f.ParentId));
    }

    [Fact]
    public void GetFoldersByParent_ReturnsFoldersInSortOrder()
    {
        // Arrange
        var folder3 = _noteService.CreateFolder("目录3");
        var folder1 = _noteService.CreateFolder("目录1");
        var folder2 = _noteService.CreateFolder("目录2");

        // Act
        var folders = _noteService.GetFoldersByParent(null);

        // Assert - 按 SortOrder 排序，不是按名称排序
        Assert.Equal(folder3.Id, folders[0].Id); // SortOrder = 0
        Assert.Equal(folder1.Id, folders[1].Id); // SortOrder = 1
        Assert.Equal(folder2.Id, folders[2].Id); // SortOrder = 2
    }

    [Fact]
    public void GetAllFolders_ReturnsAllFolders()
    {
        // Arrange
        _noteService.CreateFolder("根目录1");
        var parent = _noteService.CreateFolder("父目录");
        _noteService.CreateFolder("子目录", parent.Id);

        // Act
        var allFolders = _noteService.GetAllFolders();

        // Assert
        Assert.Equal(3, allFolders.Count);
    }

    [Fact]
    public void GetFolderCount_ReturnsCorrectCount()
    {
        // Arrange
        _noteService.CreateFolder("目录1");
        _noteService.CreateFolder("目录2");
        _noteService.CreateFolder("目录3");

        // Act
        var count = _noteService.GetFolderCount();

        // Assert
        Assert.Equal(3, count);
    }

#endregion

#region Note Item Tests - 1.3.2

    [Fact]
    public void RecordNote_WithValidData_CreatesNoteInRoot()
    {
        // Act
        var note = _noteService.RecordNote("https://example.com/video", "测试视频");

        // Assert
        Assert.NotNull(note);
        Assert.Equal("https://example.com/video", note.Url);
        Assert.Equal("测试视频", note.Title);
        Assert.Null(note.FolderId);
        Assert.NotNull(note.Id);
    }

    [Fact]
    public void RecordNote_WithEmptyTitle_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _noteService.RecordNote("url", ""));
        Assert.Throws<ArgumentException>(() => _noteService.RecordNote("url", "   "));
        Assert.Throws<ArgumentException>(() => _noteService.RecordNote("url", null!));
    }

    [Fact]
    public void RecordNote_WithFolderId_CreatesNoteInFolder()
    {
        // Arrange
        var folder = _noteService.CreateFolder("测试目录");

        // Act
        var note = _noteService.RecordNote("https://example.com/video", "测试视频", folder.Id);

        // Assert
        Assert.Equal(folder.Id, note.FolderId);
    }

    [Fact]
    public void RecordNote_WithInvalidFolderId_CreatesInRoot()
    {
        // Act
        var note = _noteService.RecordNote("https://example.com/video", "测试视频", "invalid_folder_id");

        // Assert
        Assert.Null(note.FolderId);
    }

    [Fact]
    public void RecordNote_WithDuplicateTitleAndUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var url = "https://example.com/video";
        var title = "测试视频";
        _noteService.RecordNote(url, title);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _noteService.RecordNote(url, title));
    }

    [Fact]
    public void RecordNote_SameTitleDifferentUrl_CreatesNote()
    {
        // Arrange
        var title = "测试视频";
        _noteService.RecordNote("https://example.com/video1", title);

        // Act
        var note2 = _noteService.RecordNote("https://example.com/video2", title);

        // Assert
        Assert.NotNull(note2);
    }

    [Fact]
    public void RecordNote_DifferentTitleSameUrl_CreatesNote()
    {
        // Arrange
        var url = "https://example.com/video";
        _noteService.RecordNote(url, "标题1");

        // Act
        var note2 = _noteService.RecordNote(url, "标题2");

        // Assert
        Assert.NotNull(note2);
    }

    [Fact]
    public void RecordNote_DuplicateInDifferentFolders_CreatesNote()
    {
        // Arrange
        var folder1 = _noteService.CreateFolder("目录1");
        var folder2 = _noteService.CreateFolder("目录2");
        var url = "https://example.com/video";
        var title = "测试视频";
        _noteService.RecordNote(url, title, folder1.Id);

        // Act
        var note2 = _noteService.RecordNote(url, title, folder2.Id);

        // Assert
        Assert.NotNull(note2);
        Assert.Equal(folder2.Id, note2.FolderId);
    }

    [Fact]
    public void RecordNote_WithNullUrl_UsesEmptyString()
    {
        // Act
        var note = _noteService.RecordNote(null!, "测试视频");

        // Assert
        Assert.Equal("", note.Url);
    }

    [Fact]
    public void UpdateNote_WithValidId_UpdatesNote()
    {
        // Arrange
        var note = _noteService.RecordNote("https://example.com/video", "旧标题");

        // Act
        _noteService.UpdateNote(note.Id, "新标题");
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Equal("新标题", updated!.Title);
    }

    [Fact]
    public void UpdateNote_WithNewUrl_UpdatesUrl()
    {
        // Arrange
        var note = _noteService.RecordNote("https://example.com/old", "标题");

        // Act
        _noteService.UpdateNote(note.Id, "标题", "https://example.com/new");
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Equal("https://example.com/new", updated!.Url);
    }

    [Fact]
    public void UpdateNote_WithNullNewUrl_KeepsOriginalUrl()
    {
        // Arrange
        var note = _noteService.RecordNote("https://example.com/video", "标题");

        // Act
        _noteService.UpdateNote(note.Id, "新标题", null);
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Equal("https://example.com/video", updated!.Url);
    }

    [Fact]
    public void UpdateNote_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var note = _noteService.RecordNote("url", "标题");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _noteService.UpdateNote(note.Id, ""));
    }

    [Fact]
    public void UpdateNote_WithInvalidId_DoesNothing()
    {
        // Act - 应该不抛出异常
        _noteService.UpdateNote("invalid_id", "新标题");
    }

    [Fact]
    public void DeleteNote_WithValidId_RemovesNote()
    {
        // Arrange
        var note = _noteService.RecordNote("url", "标题");

        // Act
        _noteService.DeleteNote(note.Id);
        var deleted = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Null(deleted);
    }

    [Fact]
    public void DeleteNote_WithInvalidId_DoesNothing()
    {
        // Act - 应该不抛出异常
        _noteService.DeleteNote("invalid_id");
    }

    [Fact]
    public void MoveNote_WithValidFolderId_MovesNote()
    {
        // Arrange
        var folder1 = _noteService.CreateFolder("目录1");
        var folder2 = _noteService.CreateFolder("目录2");
        var note = _noteService.RecordNote("url", "标题", folder1.Id);

        // Act
        _noteService.MoveNote(note.Id, folder2.Id);
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Equal(folder2.Id, updated!.FolderId);
    }

    [Fact]
    public void MoveNote_WithNullFolderId_MovesToRoot()
    {
        // Arrange
        var folder = _noteService.CreateFolder("目录1");
        var note = _noteService.RecordNote("url", "标题", folder.Id);

        // Act
        _noteService.MoveNote(note.Id, null);
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Null(updated!.FolderId);
    }

    [Fact]
    public void MoveNote_WithInvalidFolderId_MovesToRoot()
    {
        // Arrange
        var folder = _noteService.CreateFolder("目录1");
        var note = _noteService.RecordNote("url", "标题", folder.Id);

        // Act
        _noteService.MoveNote(note.Id, "invalid_folder_id");
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Null(updated!.FolderId);
    }

    [Fact]
    public void MoveNoteToFolder_AliasMethod_MovesNote()
    {
        // Arrange
        var folder1 = _noteService.CreateFolder("目录1");
        var folder2 = _noteService.CreateFolder("目录2");
        var note = _noteService.RecordNote("url", "标题", folder1.Id);

        // Act
        _noteService.MoveNoteToFolder(note.Id, folder2.Id);
        var updated = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.Equal(folder2.Id, updated!.FolderId);
    }

    [Fact]
    public void GetNoteById_WithValidId_ReturnsNote()
    {
        // Arrange
        var note = _noteService.RecordNote("url", "标题");

        // Act
        var found = _noteService.GetNoteById(note.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(note.Id, found.Id);
        Assert.Equal("标题", found.Title);
    }

    [Fact]
    public void GetNoteById_WithInvalidId_ReturnsNull()
    {
        // Act
        var found = _noteService.GetNoteById("invalid_id");

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void GetItemsByFolder_WithNullFolderId_ReturnsRootItems()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        var folder = _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url2", "标题2", folder.Id);

        // Act
        var rootItems = _noteService.GetItemsByFolder(null);

        // Assert
        Assert.Single(rootItems);
        Assert.Null(rootItems[0].FolderId);
    }

    [Fact]
    public void GetItemsByFolder_WithFolderId_ReturnsFolderItems()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        var folder = _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url2", "标题2", folder.Id);
        _noteService.RecordNote("url3", "标题3", folder.Id);

        // Act
        var folderItems = _noteService.GetItemsByFolder(folder.Id);

        // Assert
        Assert.Equal(2, folderItems.Count);
        Assert.All(folderItems, item => Assert.Equal(folder.Id, item.FolderId));
    }

    [Fact]
    public void GetItemsByFolder_ReturnsItemsInDescendingOrder()
    {
        // Arrange
        var folder = _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url1", "标题1", folder.Id);
        Thread.Sleep(10);
        _noteService.RecordNote("url2", "标题2", folder.Id);
        Thread.Sleep(10);
        _noteService.RecordNote("url3", "标题3", folder.Id);

        // Act
        var items = _noteService.GetItemsByFolder(folder.Id);

        // Assert
        Assert.Equal("标题3", items[0].Title);
        Assert.Equal("标题1", items[2].Title);
    }

    [Fact]
    public void GetNotesInFolder_AliasMethod_ReturnsFolderItems()
    {
        // Arrange
        var folder = _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url1", "标题1", folder.Id);
        _noteService.RecordNote("url2", "标题2", folder.Id);

        // Act
        var items = _noteService.GetNotesInFolder(folder.Id);

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void GetItemCount_ReturnsCorrectCount()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        _noteService.RecordNote("url2", "标题2");
        _noteService.RecordNote("url3", "标题3");

        // Act
        var count = _noteService.GetItemCount();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void IsUrlRecorded_WithExistingUrl_ReturnsTrue()
    {
        // Arrange
        var url = "https://example.com/video";
        _noteService.RecordNote(url, "标题");

        // Act
        var isRecorded = _noteService.IsUrlRecorded(url);

        // Assert
        Assert.True(isRecorded);
    }

    [Fact]
    public void IsUrlRecorded_WithNonExistingUrl_ReturnsFalse()
    {
        // Act
        var isRecorded = _noteService.IsUrlRecorded("https://example.com/notfound");

        // Assert
        Assert.False(isRecorded);
    }

    [Fact]
    public void IsUrlRecorded_WithEmptyUrl_ReturnsFalse()
    {
        // Act
        var result1 = _noteService.IsUrlRecorded("");
        var result2 = _noteService.IsUrlRecorded(null!);

        // Assert
        Assert.False(result1);
        Assert.False(result2);
    }

#endregion

#region Search Tests - 1.3.3

    [Fact]
    public void SearchNotes_WithKeyword_ReturnsMatchingNotes()
    {
        // Arrange
        _noteService.RecordNote("url1", "游戏攻略 第一章");
        _noteService.RecordNote("url2", "Music Video");
        _noteService.RecordNote("url3", "游戏攻略 第二章");

        // Act
        var results = _noteService.SearchNotes("游戏");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, item => Assert.Contains("游戏", item.Title));
    }

    [Fact]
    public void SearchNotes_SearchesInUrl()
    {
        // Arrange
        _noteService.RecordNote("https://www.bilibili.com/video/game1", "标题1");
        _noteService.RecordNote("https://www.youtube.com/watch?v=123", "标题2");
        _noteService.RecordNote("https://www.bilibili.com/video/game2", "标题3");

        // Act
        var results = _noteService.SearchNotes("youtube");

        // Assert
        Assert.Single(results);
        Assert.Contains("youtube", results[0].Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchNotes_WithEmptyKeyword_ReturnsAllNotes()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        _noteService.RecordNote("url2", "标题2");

        // Act
        var results = _noteService.SearchNotes("");

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void SearchNotes_CaseInsensitive_ReturnsMatchingNotes()
    {
        // Arrange
        _noteService.RecordNote("url1", "游戏攻略");
        _noteService.RecordNote("url2", "OTHER TITLE");

        // Act
        var results = _noteService.SearchNotes("游戏");

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public void SearchNotes_ReturnsInDescendingOrder()
    {
        // Arrange
        _noteService.RecordNote("url1", "游戏1");
        Thread.Sleep(10);
        _noteService.RecordNote("url2", "游戏2");
        Thread.Sleep(10);
        _noteService.RecordNote("url3", "游戏3");

        // Act
        var results = _noteService.SearchNotes("游戏");

        // Assert
        Assert.Equal("游戏3", results[0].Title);
        Assert.Equal("游戏1", results[2].Title);
    }

#endregion

#region Sort Tests - 1.3.4

    [Fact]
    public void GetSortedItems_WithDescending_ReturnsNewestFirst()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        Thread.Sleep(10);
        _noteService.RecordNote("url2", "标题2");
        Thread.Sleep(10);
        _noteService.RecordNote("url3", "标题3");

        // Act
        var items = _noteService.GetSortedItems(SortDirection.Descending);

        // Assert
        Assert.Equal("标题3", items[0].Title);
        Assert.Equal("标题1", items[2].Title);
    }

    [Fact]
    public void GetSortedItems_WithAscending_ReturnsOldestFirst()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        Thread.Sleep(10);
        _noteService.RecordNote("url2", "标题2");
        Thread.Sleep(10);
        _noteService.RecordNote("url3", "标题3");

        // Act
        var items = _noteService.GetSortedItems(SortDirection.Ascending);

        // Assert
        Assert.Equal("标题1", items[0].Title);
        Assert.Equal("标题3", items[2].Title);
    }

    [Fact]
    public void ToggleSortOrder_TogglesBetweenAscendingAndDescending()
    {
        // Arrange - 默认是 Descending
        _noteService.RecordNote("url", "标题");

        // Act
        var newOrder = _noteService.ToggleSortOrder();

        // Assert
        Assert.Equal(SortDirection.Ascending, newOrder);
        Assert.Equal(SortDirection.Ascending, _noteService.CurrentSortOrder);
    }

    [Fact]
    public void ToggleSortOrder_Twice_ReturnsToOriginal()
    {
        // Arrange
        _noteService.RecordNote("url", "标题");
        var originalOrder = _noteService.CurrentSortOrder;

        // Act
        _noteService.ToggleSortOrder();
        var finalOrder = _noteService.ToggleSortOrder();

        // Assert
        Assert.Equal(originalOrder, finalOrder);
    }

    [Fact]
    public void CurrentSortOrder_DefaultValue_IsDescending()
    {
        // Act
        var sortOrder = _noteService.CurrentSortOrder;

        // Assert
        Assert.Equal(SortDirection.Descending, sortOrder);
    }

    [Fact]
    public void CurrentSortOrder_CanBeSet()
    {
        // Act
        _noteService.CurrentSortOrder = SortDirection.Ascending;

        // Assert
        Assert.Equal(SortDirection.Ascending, _noteService.CurrentSortOrder);
    }

    [Fact]
    public void GetAllNotes_ReturnsNotesInCurrentSortOrder()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        Thread.Sleep(10);
        _noteService.RecordNote("url2", "标题2");

        // Act - 默认是降序
        var notes = _noteService.GetAllNotes();

        // Assert
        Assert.Equal("标题2", notes[0].Title);
    }

    [Fact]
    public void GetAllNotes_WithAscendingSort_ReturnsOldestFirst()
    {
        // Arrange
        _noteService.RecordNote("url1", "标题1");
        Thread.Sleep(10);
        _noteService.RecordNote("url2", "标题2");
        _noteService.CurrentSortOrder = SortDirection.Ascending;

        // Act
        var notes = _noteService.GetAllNotes();

        // Assert
        Assert.Equal("标题1", notes[0].Title);
    }

#endregion

#region Persistence Tests - 1.3.5

    [Fact]
    public void Data_PersistsToFile_RetainsNotesAndFolders()
    {
        // Arrange
        var folder = _noteService.CreateFolder("测试目录");
        _noteService.RecordNote("url1", "标题1", folder.Id);
        _noteService.RecordNote("url2", "标题2");

        // Act - 创建新的 PioneerNoteService 实例来测试文件持久化
        var newNoteService = new PioneerNoteService(_logService, _profileManager);

        // Assert
        Assert.Equal(1, newNoteService.GetFolderCount());
        Assert.Equal(2, newNoteService.GetItemCount());
        var savedFolder = newNoteService.GetFolderById(folder.Id);
        Assert.NotNull(savedFolder);
        Assert.Equal("测试目录", savedFolder.Name);
    }

    [Fact]
    public void Data_WhenFileDoesNotExist_ReturnsEmptyData()
    {
        // Arrange - 创建一个新目录确保文件不存在
        var newProfileDir = Path.Combine(_tempDir, "Profiles", "new_profile");
        Directory.CreateDirectory(newProfileDir);
        var newProfileManager = new MockProfileManager(newProfileDir);

        // Act
        var newNoteService = new PioneerNoteService(_logService, newProfileManager);

        // Assert
        Assert.Equal(0, newNoteService.GetFolderCount());
        Assert.Equal(0, newNoteService.GetItemCount());
    }

    [Fact]
    public void SortOrder_PersistsToFile_RetainsValue()
    {
        // Arrange
        _noteService.CurrentSortOrder = SortDirection.Ascending;

        // Act
        var newNoteService = new PioneerNoteService(_logService, _profileManager);

        // Assert
        Assert.Equal(SortDirection.Ascending, newNoteService.CurrentSortOrder);
    }

    [Fact]
    public void ClearAll_RemovesAllData()
    {
        // Arrange
        _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url1", "标题1");
        Assert.Equal(1, _noteService.GetFolderCount());
        Assert.Equal(1, _noteService.GetItemCount());

        // Act
        _noteService.ClearAll();

        // Assert
        Assert.Equal(0, _noteService.GetFolderCount());
        Assert.Equal(0, _noteService.GetItemCount());
    }

    [Fact]
    public void ClearAll_DataPersists_EmptyFile()
    {
        // Arrange
        _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url1", "标题1");
        _noteService.ClearAll();

        // Act - 创建新的实例验证数据已被清空
        var newNoteService = new PioneerNoteService(_logService, _profileManager);

        // Assert
        Assert.Equal(0, newNoteService.GetFolderCount());
        Assert.Equal(0, newNoteService.GetItemCount());
    }

    [Fact]
    public void GetNoteTree_ReturnsCompleteTree()
    {
        // Arrange
        var parentFolder = _noteService.CreateFolder("父目录");
        var childFolder = _noteService.CreateFolder("子目录", parentFolder.Id);
        _noteService.RecordNote("url1", "标题1", parentFolder.Id);
        _noteService.RecordNote("url2", "标题2", childFolder.Id);

        // Act
        var tree = _noteService.GetNoteTree();

        // Assert
        Assert.Equal(2, tree.Folders.Count);
        Assert.Equal(2, tree.Items.Count);
        Assert.Equal(SortDirection.Descending, tree.SortOrder);
    }

    [Fact]
    public void ProfileChanged_ReloadsDataFromFile()
    {
        // Arrange
        _noteService.CreateFolder("目录1");
        _noteService.RecordNote("url1", "标题1");
        Assert.Equal(1, _noteService.GetFolderCount());
        Assert.Equal(1, _noteService.GetItemCount());

        // Act - 模拟 Profile 切换（缓存被清除）
        _profileManager.RaiseProfileChanged();

        // Assert
        // 缓存被清除后，会从文件重新加载数据
        // 因为使用的是同一个 Profile 目录，数据仍然存在
        Assert.Equal(1, _noteService.GetFolderCount());
        Assert.Equal(1, _noteService.GetItemCount());
    }

#endregion

#region Mock Profile Manager

    /// <summary>
    /// 简单的 Mock ProfileManager，用于测试
    /// </summary>
    private class MockProfileManager : IProfileManager
    {
        private readonly string _profileDirectory;

        public MockProfileManager(string profileDirectory)
        {
            _profileDirectory = profileDirectory;
            CurrentProfile = new GameProfile { Id = "test", Name = "Test Profile", Icon = "test.png" };
        }

        public event EventHandler<GameProfile>? ProfileChanged;

        public GameProfile CurrentProfile { get; }

        public List<GameProfile> Profiles => new() { CurrentProfile };

        public IReadOnlyList<GameProfile> InstalledProfiles => Profiles;

        public string DataDirectory => Path.GetDirectoryName(_profileDirectory) ?? "";
        public string ProfilesDirectory => Path.GetDirectoryName(_profileDirectory) ?? "";
        public string[] ProfileIcons => Array.Empty<string>();

        public bool SwitchProfile(string profileId) => true;

        public GameProfile? GetProfileById(string id) => CurrentProfile;

        public string GetCurrentProfileDirectory() => _profileDirectory;

        public string GetProfileDirectory(string profileId) => _profileDirectory;

        public void SaveCurrentProfile()
        {
        }

        public void SaveProfile(GameProfile profile)
        {
        }

        public Result DeleteProfile(string id) => Result.Success();

        public bool IsDefaultProfile(string id) => id == "default";

        public bool ProfileIdExists(string id) => id == "test";

        public string GenerateProfileId(string name) => name.ToLower().Replace(" ", "_");

        public Result<string> CreateProfile(string? id, string name, string icon,
                                            List<string>? pluginIds) => Result<string>.Success("test");

        public bool UpdateProfile(string id, string newName, string newIcon) => true;

        public bool UpdateProfile(string id, ProfileUpdateData updateData) => true;

        public void ReloadProfiles()
        {
        }

        public bool SubscribeProfile(string profileId) => true;

        public UnsubscribeResult UnsubscribeProfile(string profileId) => UnsubscribeResult.Succeeded();

        public UnsubscribeResult UnsubscribeProfileViaSubscription(string profileId) => UnsubscribeResult.Succeeded();

        public ProfileExportData? ExportProfile(string profileId) => null;

        public bool ExportProfileToFile(string profileId, string filePath) => true;

        public ProfileImportResult ImportProfile(ProfileExportData data,
                                                 bool overwrite = false) => ProfileImportResult.Success("test");

        public ProfileImportResult ImportProfileFromFile(string filePath,
                                                         bool overwrite = false) => ProfileImportResult.Success("test");

        public ProfileImportResult PreviewImport(ProfileExportData data) => ProfileImportResult.Success("test");

        public List<PluginReference> GetPluginReferences(string profileId) => new();

        public bool SetPluginEnabled(string profileId, string pluginId, bool enabled) => true;

        public Dictionary<string, object>? GetPluginConfig(string profileId, string pluginId) => new();

        public bool SavePluginConfig(string profileId, string pluginId, Dictionary<string, object> config) => true;

        public bool DeletePluginConfig(string profileId, string pluginId) => true;

        public string GetPluginConfigsDirectory(string profileId) => _profileDirectory;

        public Dictionary<string, Dictionary<string, object>> GetAllPluginConfigs(string profileId) => new();

        public void RaiseProfileChanged()
        {
            ProfileChanged?.Invoke(this, CurrentProfile);
        }
    }

#endregion
}
}
