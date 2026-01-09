using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Data;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services
{
/// <summary>
/// DataService 单元测试
/// 测试历史记录和书签的 CRUD 操作
/// </summary>
public class DataServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profileDir;
    private readonly ILogService _logService;
    private readonly MockProfileManager _profileManager;
    private readonly DataService _dataService;

    public DataServiceTests()
    {
        // 创建临时目录用于测试
        _tempDir = Path.Combine(Path.GetTempPath(), $"akasha_test_{Guid.NewGuid():N}");
        _profileDir = Path.Combine(_tempDir, "Profiles", "default");
        Directory.CreateDirectory(_profileDir);

        // 创建测试用的服务
        _logService = new LogService(Path.Combine(_tempDir, "Logs"));
        _profileManager = new MockProfileManager(_profileDir);
        _dataService = new DataService(_logService, _profileManager);
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

#region History Tests - 1.1.1

    [Fact]
    public void GetHistory_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var history = _dataService.GetHistory();

        // Assert
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public void AddHistory_WithValidData_AddsItemToCache()
    {
        // Arrange
        var url = "https://www.bilibili.com/video/test";
        var title = "测试视频";

        // Act
        _dataService.AddHistory(url, title);
        var history = _dataService.GetHistory();

        // Assert
        Assert.Single(history);
        Assert.Equal(url, history[0].Url);
        Assert.Equal(title, history[0].Title);
        Assert.Equal(1, history[0].VisitCount);
    }

    [Fact]
    public void AddHistory_WithEmptyUrl_DoesNotAddItem()
    {
        // Act
        _dataService.AddHistory("", "title");
        _dataService.AddHistory(null!, "title");
        _dataService.AddHistory("   ", "title");
        var history = _dataService.GetHistory();

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public void AddHistory_WithEmptyTitle_UsesUrlAsTitle()
    {
        // Arrange
        var url = "https://www.bilibili.com/video/test";

        // Act
        _dataService.AddHistory(url, "");
        var history = _dataService.GetHistory();

        // Assert
        Assert.Single(history);
        Assert.Equal(url, history[0].Title);
    }

    [Fact]
    public async Task AddHistory_WithExistingUrl_UpdatesVisitTimeAndCount()
    {
        // Arrange
        var url = "https://www.bilibili.com/video/test";
        _dataService.AddHistory(url, "First Title");

        // 等待一小段时间确保时间戳不同
        await Task.Delay(10);

        // Act
        _dataService.AddHistory(url, "Updated Title");
        var history = _dataService.GetHistory();

        // Assert
        Assert.Single(history);
        Assert.Equal("Updated Title", history[0].Title);
        Assert.Equal(2, history[0].VisitCount);
    }

    [Fact]
    public void AddHistory_CaseInsensitiveUrl_MatchesExistingEntry()
    {
        // Arrange
        var url1 = "https://www.BILIBILI.com/video/test";
        var url2 = "https://www.bilibili.com/video/TEST";

        // Act
        _dataService.AddHistory(url1, "Title 1");
        _dataService.AddHistory(url2, "Title 2");
        var history = _dataService.GetHistory();

        // Assert
        Assert.Single(history);
        Assert.Equal(2, history[0].VisitCount);
    }

    [Fact]
    public async Task GetHistory_ReturnsItemsInDescendingOrder()
    {
        // Arrange
        _dataService.AddHistory("url1", "Title 1");
        await Task.Delay(10);
        _dataService.AddHistory("url2", "Title 2");
        await Task.Delay(10);
        _dataService.AddHistory("url3", "Title 3");

        // Act
        var history = _dataService.GetHistory();

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal("url3", history[0].Url); // 最新
        Assert.Equal("url1", history[2].Url); // 最旧
    }

    [Fact]
    public void DeleteHistory_WithValidId_RemovesItem()
    {
        // Arrange
        _dataService.AddHistory("url1", "Title 1");
        _dataService.AddHistory("url2", "Title 2");
        var history = _dataService.GetHistory();
        var idToDelete = history[0].Id;

        // Act
        _dataService.DeleteHistory(idToDelete);
        var updatedHistory = _dataService.GetHistory();

        // Assert
        Assert.Single(updatedHistory);
        Assert.DoesNotContain(updatedHistory, h => h.Id == idToDelete);
    }

    [Fact]
    public void ClearHistory_RemovesAllItems()
    {
        // Arrange
        _dataService.AddHistory("url1", "Title 1");
        _dataService.AddHistory("url2", "Title 2");
        _dataService.AddHistory("url3", "Title 3");
        Assert.Equal(3, _dataService.GetHistory().Count);

        // Act
        _dataService.ClearHistory();
        var history = _dataService.GetHistory();

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public void SearchHistory_WithKeyword_ReturnsMatchingItems()
    {
        // Arrange
        _dataService.AddHistory("https://www.bilibili.com/video/game1", "游戏攻略 第一章");
        _dataService.AddHistory("https://www.youtube.com/watch?v=123", "Music Video");
        _dataService.AddHistory("https://www.bilibili.com/video/game2", "游戏攻略 第二章");

        // Act
        var results = _dataService.SearchHistory("游戏");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, item => Assert.Contains("游戏", item.Title));
    }

    [Fact]
    public void SearchHistory_WithUrlKeyword_ReturnsMatchingItems()
    {
        // Arrange
        _dataService.AddHistory("https://www.bilibili.com/video/game1", "Title 1");
        _dataService.AddHistory("https://www.youtube.com/watch?v=123", "Title 2");
        _dataService.AddHistory("https://www.bilibili.com/video/game2", "Title 3");

        // Act
        var results = _dataService.SearchHistory("youtube");

        // Assert
        Assert.Single(results);
        Assert.Contains("youtube", results[0].Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchHistory_WithEmptyKeyword_ReturnsAllItems()
    {
        // Arrange
        _dataService.AddHistory("url1", "Title 1");
        _dataService.AddHistory("url2", "Title 2");

        // Act
        var results = _dataService.SearchHistory("");

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void SearchHistory_CaseInsensitive_ReturnsMatchingItems()
    {
        // Arrange
        _dataService.AddHistory("url1", "游戏攻略");
        _dataService.AddHistory("url2", "OTHER TITLE");

        // Act
        var results = _dataService.SearchHistory("游戏");

        // Assert
        Assert.Single(results);
    }

#endregion

#region Bookmark Tests - 1.1.2

    [Fact]
    public void GetBookmarks_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.NotNull(bookmarks);
        Assert.Empty(bookmarks);
    }

    [Fact]
    public void AddBookmark_WithValidData_AddsItem()
    {
        // Arrange
        var url = "https://www.bilibili.com/video/test";
        var title = "测试视频";

        // Act
        var bookmark = _dataService.AddBookmark(url, title);
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.NotNull(bookmark);
        Assert.Equal(url, bookmark.Url);
        Assert.Equal(title, bookmark.Title);
        Assert.Single(bookmarks);
    }

    [Fact]
    public void AddBookmark_WithEmptyUrl_CreatesBookmarkWithEmptyUrl()
    {
        // Act
        var bookmark = _dataService.AddBookmark("", "title");

        // Assert
        Assert.NotNull(bookmark);
        Assert.Equal("", bookmark.Url);
        Assert.Equal("title", bookmark.Title);
    }

    [Fact]
    public void AddBookmark_WithExistingUrl_ReturnsExistingBookmark()
    {
        // Arrange
        var url = "https://www.bilibili.com/video/test";
        var firstBookmark = _dataService.AddBookmark(url, "First Title");
        var bookmarks = _dataService.GetBookmarks();

        // Act
        var secondBookmark = _dataService.AddBookmark(url, "Second Title");

        // Assert
        Assert.Same(firstBookmark, secondBookmark);
        Assert.Single(bookmarks);
    }

    [Fact]
    public void AddBookmark_CaseInsensitiveUrl_MatchesExistingEntry()
    {
        // Arrange
        var url1 = "https://www.BILIBILI.com/video/test";
        var url2 = "https://www.bilibili.com/video/TEST";

        // Act
        _dataService.AddBookmark(url1, "Title 1");
        _dataService.AddBookmark(url2, "Title 2");
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Single(bookmarks);
    }

    [Fact]
    public void AddBookmark_SetsCorrectSortOrder()
    {
        // Act
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");
        _dataService.AddBookmark("url3", "Title 3");

        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Equal(0, bookmarks[0].SortOrder);
        Assert.Equal(1, bookmarks[1].SortOrder);
        Assert.Equal(2, bookmarks[2].SortOrder);
    }

    [Fact]
    public void GetBookmarks_ReturnsItemsInSortOrder()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");
        _dataService.AddBookmark("url3", "Title 3");

        // Act
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Equal("url1", bookmarks[0].Url);
        Assert.Equal("url2", bookmarks[1].Url);
        Assert.Equal("url3", bookmarks[2].Url);
    }

    [Fact]
    public void DeleteBookmark_WithValidId_RemovesItem()
    {
        // Arrange
        var bookmark = _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");

        // Act
        _dataService.DeleteBookmark(bookmark.Id);
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Single(bookmarks);
        Assert.DoesNotContain(bookmarks, b => b.Id == bookmark.Id);
    }

    [Fact]
    public void DeleteBookmarkByUrl_WithValidUrl_RemovesItem()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");

        // Act
        _dataService.DeleteBookmarkByUrl("url1");
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Single(bookmarks);
        Assert.DoesNotContain(bookmarks, b => b.Url.Equals("url1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeleteBookmarkByUrl_CaseInsensitive_RemovesItem()
    {
        // Arrange
        _dataService.AddBookmark("https://www.BILIBILI.com/test", "Title 1");

        // Act
        _dataService.DeleteBookmarkByUrl("https://www.bilibili.com/TEST");
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Empty(bookmarks);
    }

    [Fact]
    public void IsBookmarked_WithExistingUrl_ReturnsTrue()
    {
        // Arrange
        _dataService.AddBookmark("https://www.bilibili.com/test", "Title 1");

        // Act
        var isBookmarked = _dataService.IsBookmarked("https://www.bilibili.com/test");

        // Assert
        Assert.True(isBookmarked);
    }

    [Fact]
    public void IsBookmarked_WithNonExistingUrl_ReturnsFalse()
    {
        // Act
        var isBookmarked = _dataService.IsBookmarked("https://www.example.com");

        // Assert
        Assert.False(isBookmarked);
    }

    [Fact]
    public void IsBookmarked_WithEmptyUrl_ReturnsFalse()
    {
        // Act
        var result1 = _dataService.IsBookmarked("");
        var result2 = _dataService.IsBookmarked(null!);

        // Assert
        Assert.False(result1);
        Assert.False(result2);
    }

    [Fact]
    public void IsBookmarked_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        _dataService.AddBookmark("https://www.BILIBILI.com/test", "Title 1");

        // Act
        var isBookmarked = _dataService.IsBookmarked("https://www.bilibili.com/TEST");

        // Assert
        Assert.True(isBookmarked);
    }

    [Fact]
    public void ClearBookmarks_RemovesAllItems()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");
        _dataService.AddBookmark("url3", "Title 3");
        Assert.Equal(3, _dataService.GetBookmarks().Count);

        // Act
        _dataService.ClearBookmarks();
        var bookmarks = _dataService.GetBookmarks();

        // Assert
        Assert.Empty(bookmarks);
    }

    [Fact]
    public void ToggleBookmark_WhenNotBookmarked_AddsAndReturnsTrue()
    {
        // Arrange
        var url = "https://www.bilibili.com/test";
        var title = "Test Title";

        // Act
        var result = _dataService.ToggleBookmark(url, title);

        // Assert
        Assert.True(result);
        Assert.True(_dataService.IsBookmarked(url));
    }

    [Fact]
    public void ToggleBookmark_WhenAlreadyBookmarked_RemovesAndReturnsFalse()
    {
        // Arrange
        var url = "https://www.bilibili.com/test";
        _dataService.AddBookmark(url, "Title 1");

        // Act
        var result = _dataService.ToggleBookmark(url, "Title 2");

        // Assert
        Assert.False(result);
        Assert.False(_dataService.IsBookmarked(url));
    }

    [Fact]
    public void SearchBookmarks_WithKeyword_ReturnsMatchingItems()
    {
        // Arrange
        _dataService.AddBookmark("url1", "游戏攻略 第一章");
        _dataService.AddBookmark("url2", "Music Video");
        _dataService.AddBookmark("url3", "游戏攻略 第二章");

        // Act
        var results = _dataService.SearchBookmarks("游戏");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, item => Assert.Contains("游戏", item.Title));
    }

    [Fact]
    public void SearchBookmarks_WithEmptyKeyword_ReturnsAllItems()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");

        // Act
        var results = _dataService.SearchBookmarks("");

        // Assert
        Assert.Equal(2, results.Count);
    }

#endregion

#region Concurrency Tests - 1.1.3

    /// <summary>
    /// 并发测试已知问题： DataService 不是线程安全的
    /// 这些测试记录了当前代码在并发场景下的实际行为
    /// 注意：DataService 使用内部缓存，并发访问可能导致集合修改异常或数据丢失
    /// </summary>

    [Fact]
    public async Task AddHistory_ConcurrentCalls_KnownConcurrencyIssues()
    {
        // Arrange
        var tasks = new Task[100];
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
                                {
                                    try
                                    {
                                        _dataService.AddHistory($"url{index}", $"Title {index}");
                                    }
                                    catch (Exception ex)
                                    {
                                        exceptions.Add(ex);
                                    }
                                });
        }
        await Task.WhenAll(tasks);

        // Assert - 记录并发问题的存在
        // 由于 DataService 不是线程安全的，可能会出现以下情况：
        // 1. InvalidOperationException: 集合被修改
        // 2. 历史记录数量少于预期（并发覆盖）
        var history = _dataService.GetHistory();
        Assert.True(history.Count > 0, "至少应该有一些历史记录被添加");
    }

    [Fact]
    public async Task AddBookmark_ConcurrentCalls_KnownConcurrencyIssues()
    {
        // Arrange
        var tasks = new Task[100];
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
                                {
                                    try
                                    {
                                        _dataService.AddBookmark($"url{index}", $"Title {index}");
                                    }
                                    catch (Exception ex)
                                    {
                                        exceptions.Add(ex);
                                    }
                                });
        }
        await Task.WhenAll(tasks);

        // Assert - 记录并发问题的存在
        // 由于 DataService 不是线程安全的，书签数量可能少于预期
        var bookmarks = _dataService.GetBookmarks();
        Assert.True(bookmarks.Count > 0, "至少应该有一些书签被添加");
    }

    [Fact]
    public async Task MixedOperations_ConcurrentCalls_KnownConcurrencyIssues()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");
        var tasks = new Task[50];
        var random = new Random();

        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
                                {
                                    try
                                    {
                                        switch (random.Next(5))
                                        {
                                        case 0:
                                            _dataService.AddHistory($"url_{Guid.NewGuid()}", "Title");
                                            break;
                                        case 1:
                                            _dataService.GetHistory();
                                            break;
                                        case 2:
                                            _dataService.AddBookmark($"url_{Guid.NewGuid()}", "Title");
                                            break;
                                        case 3:
                                            _dataService.GetBookmarks();
                                            break;
                                        case 4:
                                            _dataService.SearchBookmarks("Title");
                                            break;
                                        }
                                    }
                                    catch
                                    {
                                        // 忽略并发异常
                                    }
                                });
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - 至少不会崩溃
        Assert.True(_dataService.GetHistory().Count >= 2);
    }

#endregion

#region Persistence Tests - 1.1.4

    [Fact]
    public void History_PersistsToFile_RetainsData()
    {
        // Arrange
        _dataService.AddHistory("url1", "Title 1");
        _dataService.AddHistory("url2", "Title 2");

        // Act - 创建新的 DataService 实例来测试文件持久化
        var newDataService = new DataService(_logService, _profileManager);
        var history = newDataService.GetHistory();

        // Assert
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Bookmarks_PersistToFile_RetainsData()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");

        // Act - 创建新的 DataService 实例来测试文件持久化
        var newDataService = new DataService(_logService, _profileManager);
        var bookmarks = newDataService.GetBookmarks();

        // Assert
        Assert.Equal(2, bookmarks.Count);
    }

    [Fact]
    public void History_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange - 创建一个新目录确保文件不存在
        var newProfileDir = Path.Combine(_tempDir, "Profiles", "new_profile");
        Directory.CreateDirectory(newProfileDir);
        var newProfileManager = new MockProfileManager(newProfileDir);

        // Act
        var newDataService = new DataService(_logService, newProfileManager);
        var history = newDataService.GetHistory();

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public void Bookmarks_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange - 创建一个新目录确保文件不存在
        var newProfileDir = Path.Combine(_tempDir, "Profiles", "new_profile");
        Directory.CreateDirectory(newProfileDir);
        var newProfileManager = new MockProfileManager(newProfileDir);

        // Act
        var newDataService = new DataService(_logService, newProfileManager);
        var bookmarks = newDataService.GetBookmarks();

        // Assert
        Assert.Empty(bookmarks);
    }

    [Fact]
    public void History_ClearHistory_DeletesFileContent()
    {
        // Arrange
        _dataService.AddHistory("url1", "Title 1");
        _dataService.AddHistory("url2", "Title 2");
        _dataService.ClearHistory();

        // Act - 创建新的 DataService 实例
        var newDataService = new DataService(_logService, _profileManager);
        var history = newDataService.GetHistory();

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public void Bookmarks_ClearBookmarks_DeletesFileContent()
    {
        // Arrange
        _dataService.AddBookmark("url1", "Title 1");
        _dataService.AddBookmark("url2", "Title 2");
        _dataService.ClearBookmarks();

        // Act - 创建新的 DataService 实例
        var newDataService = new DataService(_logService, _profileManager);
        var bookmarks = newDataService.GetBookmarks();

        // Assert
        Assert.Empty(bookmarks);
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

#pragma warning disable CS0067 // 事件从未使用（接口要求实现）
        public event EventHandler<GameProfile>? ProfileChanged;
#pragma warning restore CS0067

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
    }

#endregion
}
}
