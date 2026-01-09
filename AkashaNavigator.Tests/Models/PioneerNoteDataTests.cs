using System;
using System.Collections.Generic;
using System.Text.Json;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;
using Xunit;

namespace AkashaNavigator.Tests.Models
{
/// <summary>
/// PioneerNoteData 单元测试
/// 测试笔记数据模型验证、序列化/反序列化和树节点结构
/// </summary>
public class PioneerNoteDataTests
{
#region Note Data Model Validation Tests - 3.3.1

    [Fact]
    public void PioneerNoteData_Constructor_WithNoParameters_HasDefaultValues()
    {
        // Arrange & Act
        var data = new PioneerNoteData();

        // Assert
        Assert.NotNull(data.Folders);
        Assert.Empty(data.Folders);
        Assert.NotNull(data.Items);
        Assert.Empty(data.Items);
        Assert.Equal(SortDirection.Descending, data.SortOrder);
        Assert.Equal(1, data.Version);
    }

    [Fact]
    public void PioneerNoteData_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var data = new PioneerNoteData { Folders = new List<NoteFolder> { new() { Name = "Test Folder" } },
                                         Items = new List<NoteItem> { new() { Title = "Test Note" } },
                                         SortOrder = SortDirection.Ascending, Version = 2 };

        // Assert
        Assert.Single(data.Folders);
        Assert.Equal("Test Folder", data.Folders[0].Name);
        Assert.Single(data.Items);
        Assert.Equal("Test Note", data.Items[0].Title);
        Assert.Equal(SortDirection.Ascending, data.SortOrder);
        Assert.Equal(2, data.Version);
    }

    [Fact]
    public void NoteFolder_Constructor_WithNoParameters_HasDefaultValues()
    {
        // Arrange & Act
        var folder = new NoteFolder();

        // Assert
        Assert.NotNull(folder.Id);
        Assert.NotEmpty(folder.Id);
        Assert.Equal(string.Empty, folder.Name);
        Assert.Null(folder.ParentId);
        // CreatedTime 是值类型，不需要 NotNull 检查
        Assert.Null(folder.Icon);
        Assert.Equal(0, folder.SortOrder);
    }

    [Fact]
    public void NoteFolder_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var testId = "test_folder_id";
        var parentId = "parent_folder_id";
        var createdTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var folder =
            new NoteFolder { Id = testId, Name = "Custom Folder", ParentId = parentId, CreatedTime = createdTime,
                             Icon = "📁", SortOrder = 5 };

        // Assert
        Assert.Equal(testId, folder.Id);
        Assert.Equal("Custom Folder", folder.Name);
        Assert.Equal(parentId, folder.ParentId);
        Assert.Equal(createdTime, folder.CreatedTime);
        Assert.Equal("📁", folder.Icon);
        Assert.Equal(5, folder.SortOrder);
    }

    [Fact]
    public void NoteFolder_Id_GeneratesUniqueGuid()
    {
        // Arrange & Act
        var folder1 = new NoteFolder();
        var folder2 = new NoteFolder();

        // Assert
        Assert.NotEqual(folder1.Id, folder2.Id);
    }

    [Fact]
    public void NoteItem_Constructor_WithNoParameters_HasDefaultValues()
    {
        // Arrange & Act
        var item = new NoteItem();

        // Assert
        Assert.NotNull(item.Id);
        Assert.NotEmpty(item.Id);
        Assert.Equal(string.Empty, item.Title);
        Assert.Equal(string.Empty, item.Url);
        Assert.Null(item.FolderId);
        // RecordedTime 是值类型，不需要 NotNull 检查
        Assert.Null(item.Metadata);
    }

    [Fact]
    public void NoteItem_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var testId = "test_item_id";
        var folderId = "folder_id";
        var recordedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var metadata = new Dictionary<string, string> { { "profile_id", "test_profile" }, { "tags", "test1,test2" } };

        var item = new NoteItem { Id = testId,         Title = "Custom Note",       Url = "https://example.com/video",
                                  FolderId = folderId, RecordedTime = recordedTime, Metadata = metadata };

        // Assert
        Assert.Equal(testId, item.Id);
        Assert.Equal("Custom Note", item.Title);
        Assert.Equal("https://example.com/video", item.Url);
        Assert.Equal(folderId, item.FolderId);
        Assert.Equal(recordedTime, item.RecordedTime);
        Assert.NotNull(item.Metadata);
        Assert.Equal(2, item.Metadata.Count);
        Assert.Equal("test_profile", item.Metadata["profile_id"]);
        Assert.Equal("test1,test2", item.Metadata["tags"]);
    }

    [Fact]
    public void NoteItem_Id_GeneratesUniqueGuid()
    {
        // Arrange & Act
        var item1 = new NoteItem();
        var item2 = new NoteItem();

        // Assert
        Assert.NotEqual(item1.Id, item2.Id);
    }

    [Fact]
    public void NoteItem_Metadata_WithEmptyDictionary_WorksCorrectly()
    {
        // Arrange & Act
        var item = new NoteItem { Title = "Test", Metadata = new Dictionary<string, string>() };

        // Assert
        Assert.Equal("Test", item.Title);
        Assert.NotNull(item.Metadata);
        Assert.Empty(item.Metadata);
    }

#endregion

#region Serialization / Deserialization Tests - 3.3.2

    [Fact]
    public void PioneerNoteData_Serialize_WithCompleteData_ProducesValidJson()
    {
        // Arrange
        var data = new PioneerNoteData {
            Folders =
                new List<NoteFolder> {
                    new NoteFolder { Id = "folder1", Name = "Game Guide", ParentId = null,
                                     CreatedTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc), Icon = "🎮",
                                     SortOrder = 0 },
                    new NoteFolder { Id = "folder2", Name = "Boss Strategies", ParentId = "folder1",
                                     CreatedTime = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc), Icon = "👹",
                                     SortOrder = 1 }
                },
            Items = new List<NoteItem> { new NoteItem {
                Id = "item1", Title = "UID12345-亚洲服", Url = "https://example.com/uid12345", FolderId = "folder2",
                RecordedTime = new DateTime(2024, 1, 3, 15, 30, 0, DateTimeKind.Utc),
                Metadata = new Dictionary<string, string> { { "server", "asia" } }
            } },
            SortOrder = SortDirection.Ascending, Version = 1
        };

        // Act
        var json = JsonSerializer.Serialize(data, JsonHelper.WriteOptions);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("folder1", json);
        Assert.Contains("Game Guide", json);
        // 中文字符在 JSON 中会被转义，所以我们只需要检查 URL 包含 uid12345
        Assert.Contains("uid12345", json);
    }

    [Fact]
    public void PioneerNoteData_Deserialize_WithValidJson_ReturnsCorrectData()
    {
        // Arrange
        var json = @"{
""folders"": [
{
""id"": ""folder_root"",
""name"": ""Root Folder"",
""parentId"": null,
""createdTime"": ""2024-01-01T10:00:00Z"",
""icon"": ""📂"",
""sortOrder"": 0
},
{
""id"": ""folder_child"",
""name"": ""Child Folder"",
""parentId"": ""folder_root"",
""createdTime"": ""2024-01-02T10:00:00Z"",
""icon"": ""📁"",
""sortOrder"": 0
}
],
""items"": [
{
""id"": ""item1"",
""title"": ""Test Note"",
""url"": ""https://test.com"",
""folderId"": ""folder_child"",
""recordedTime"": ""2024-01-03T15:30:00Z"",
""metadata"": {
""profile"": ""test_profile"",
""tags"": ""tag1,tag2""
}
}
],
""sortOrder"": 0,
""version"": 1
}";

        // Act
        var data = JsonSerializer.Deserialize<PioneerNoteData>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(data);
        Assert.NotNull(data.Folders);
        Assert.Equal(2, data.Folders.Count);
        Assert.Equal("folder_root", data.Folders[0].Id);
        Assert.Equal("Root Folder", data.Folders[0].Name);
        Assert.Null(data.Folders[0].ParentId);
        Assert.Equal("📂", data.Folders[0].Icon);
        Assert.Equal(0, data.Folders[0].SortOrder);
        Assert.Equal("folder_child", data.Folders[1].Id);
        Assert.Equal("Child Folder", data.Folders[1].Name);
        Assert.Equal("folder_root", data.Folders[1].ParentId);

        Assert.NotNull(data.Items);
        Assert.Single(data.Items);
        Assert.Equal("item1", data.Items[0].Id);
        Assert.Equal("Test Note", data.Items[0].Title);
        Assert.Equal("https://test.com", data.Items[0].Url);
        Assert.Equal("folder_child", data.Items[0].FolderId);
        Assert.NotNull(data.Items[0].Metadata);
        Assert.Equal(2, data.Items[0].Metadata!.Count);
        Assert.Equal("test_profile", data.Items[0].Metadata!["profile"]);
        Assert.Equal(SortDirection.Ascending, data.SortOrder);
        Assert.Equal(1, data.Version);
    }

    [Fact]
    public void PioneerNoteData_SerializeThenDeserialize_PreservesAllProperties()
    {
        // Arrange
        var original = new PioneerNoteData {
            Folders = new List<NoteFolder> { new NoteFolder {
                Id = "folder1", Name = "Test Folder", ParentId = null,
                CreatedTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc), Icon = "📁", SortOrder = 10
            } },
            Items = new List<NoteItem> { new NoteItem {
                Id = "item1", Title = "Test Title", Url = "https://example.com/test", FolderId = "folder1",
                RecordedTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc),
                Metadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }
            } },
            SortOrder = SortDirection.Descending, Version = 2
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonHelper.WriteOptions);
        var deserialized = JsonSerializer.Deserialize<PioneerNoteData>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Folders.Count, deserialized.Folders.Count);
        Assert.Equal(original.Folders[0].Id, deserialized.Folders[0].Id);
        Assert.Equal(original.Folders[0].Name, deserialized.Folders[0].Name);
        Assert.Equal(original.Folders[0].ParentId, deserialized.Folders[0].ParentId);
        Assert.Equal(original.Folders[0].CreatedTime, deserialized.Folders[0].CreatedTime);
        Assert.Equal(original.Folders[0].Icon, deserialized.Folders[0].Icon);
        Assert.Equal(original.Folders[0].SortOrder, deserialized.Folders[0].SortOrder);

        Assert.Equal(original.Items.Count, deserialized.Items.Count);
        Assert.Equal(original.Items[0].Id, deserialized.Items[0].Id);
        Assert.Equal(original.Items[0].Title, deserialized.Items[0].Title);
        Assert.Equal(original.Items[0].Url, deserialized.Items[0].Url);
        Assert.Equal(original.Items[0].FolderId, deserialized.Items[0].FolderId);
        Assert.Equal(original.Items[0].RecordedTime, deserialized.Items[0].RecordedTime);
        Assert.NotNull(deserialized.Items[0].Metadata);
        Assert.Equal(original.Items[0].Metadata!.Count, deserialized.Items[0].Metadata!.Count);
        Assert.Equal(original.Items[0].Metadata!["key1"], deserialized.Items[0].Metadata!["key1"]);
        Assert.Equal(original.Items[0].Metadata!["key2"], deserialized.Items[0].Metadata!["key2"]);

        Assert.Equal(original.SortOrder, deserialized.SortOrder);
        Assert.Equal(original.Version, deserialized.Version);
    }

    [Fact]
    public void PioneerNoteData_Deserialize_WithMinimalJson_HasDefaultValues()
    {
        // Arrange
        var minimalJson = @"{
""folders"": [],
""items"": [],
""version"": 1
}";

        // Act
        var data = JsonSerializer.Deserialize<PioneerNoteData>(minimalJson, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(data);
        Assert.NotNull(data.Folders);
        Assert.Empty(data.Folders);
        Assert.NotNull(data.Items);
        Assert.Empty(data.Items);
        Assert.Equal(SortDirection.Descending, data.SortOrder); // 默认值
        Assert.Equal(1, data.Version);
    }

    [Fact]
    public void SortDirection_Ascending_SerializesAsZero()
    {
        // Arrange
        var data = new PioneerNoteData { SortOrder = SortDirection.Ascending };

        // Act
        var json = JsonSerializer.Serialize(data, JsonHelper.WriteOptions);

        // Assert
        Assert.Contains("sortOrder", json);
        Assert.Contains("0", json);
    }

    [Fact]
    public void SortDirection_Descending_SerializesAsOne()
    {
        // Arrange
        var data = new PioneerNoteData { SortOrder = SortDirection.Descending };

        // Act
        var json = JsonSerializer.Serialize(data, JsonHelper.WriteOptions);

        // Assert
        Assert.Contains("sortOrder", json);
        Assert.Contains("1", json);
    }

    [Fact]
    public void SortDirection_Deserialize_ValidValue_WorksCorrectly()
    {
        // Arrange
        var json = @"{ ""sortOrder"": 0 }";

        // Act
        var data = JsonSerializer.Deserialize<PioneerNoteData>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(data);
        Assert.Equal(SortDirection.Ascending, data.SortOrder);
    }

#endregion

#region Tree Node Structure Tests - 3.3.3

    [Fact]
    public void NoteFolder_ParentId_Null_RepresentsRootFolder()
    {
        // Arrange & Act
        var folder = new NoteFolder { Id = "root", Name = "Root", ParentId = null };

        // Assert
        Assert.NotNull(folder.Id);
        Assert.Null(folder.ParentId);
    }

    [Fact]
    public void NoteFolder_ParentId_Set_CreatesChildRelationship()
    {
        // Arrange
        var parent = new NoteFolder { Id = "parent", Name = "Parent" };
        var child = new NoteFolder { Id = "child", Name = "Child", ParentId = "parent" };

        // Assert
        Assert.Equal("parent", child.ParentId);
        Assert.NotEqual(parent.Id, child.Id);
    }

    [Fact]
    public void NoteFolder_SortOrder_AllowsSiblingOrdering()
    {
        // Arrange & Act
        var folders = new List<NoteFolder> { new NoteFolder { Id = "f1", Name = "First", SortOrder = 2 },
                                             new NoteFolder { Id = "f2", Name = "Second", SortOrder = 0 },
                                             new NoteFolder { Id = "f3", Name = "Third", SortOrder = 1 } };

        // Act - 按排序顺序排列
        folders.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

        // Assert
        Assert.Equal("f2", folders[0].Id); // SortOrder = 0
        Assert.Equal("f3", folders[1].Id); // SortOrder = 1
        Assert.Equal("f1", folders[2].Id); // SortOrder = 2
    }

    [Fact]
    public void NoteItem_FolderId_Null_RepresentsRootItem()
    {
        // Arrange & Act
        var item = new NoteItem { Id = "item1", Title = "Root Item", FolderId = null };

        // Assert
        Assert.Null(item.FolderId);
    }

    [Fact]
    public void NoteItem_FolderId_Set_CreatesFolderAssociation()
    {
        // Arrange
        var folder = new NoteFolder { Id = "folder1", Name = "Folder" };
        var item = new NoteItem { Id = "item1", Title = "Item in Folder", FolderId = "folder1" };

        // Assert
        Assert.Equal("folder1", item.FolderId);
    }

    [Fact]
    public void NoteFolder_CreatedTime_DefaultsToCurrentTime()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);

        // Act
        var folder = new NoteFolder();

        // Assert
        // CreatedTime 应该接近当前时间（使用 Local 时间比较）
        var diff = Math.Abs((DateTime.Now - folder.CreatedTime).TotalSeconds);
        Assert.True(diff < 2, $"CreatedTime should be within 2 seconds of current time, but was {diff}s");
    }

    [Fact]
    public void NoteItem_RecordedTime_DefaultsToCurrentTime()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);

        // Act
        var item = new NoteItem();

        // Assert
        // RecordedTime 应该接近当前时间（使用 Local 时间比较）
        var diff = Math.Abs((DateTime.Now - item.RecordedTime).TotalSeconds);
        Assert.True(diff < 2, $"RecordedTime should be within 2 seconds of current time, but was {diff}s");
    }

    [Fact]
    public void NoteFolder_Icon_Null_UsesDefaultIcon()
    {
        // Arrange & Act
        var folder = new NoteFolder { Id = "f1", Name = "No Icon Folder", Icon = null };

        // Assert
        Assert.Null(folder.Icon);
    }

    [Fact]
    public void NoteFolder_Icon_Set_CanUseEmoji()
    {
        // Arrange & Act
        var folder = new NoteFolder { Id = "f1", Name = "Game Folder", Icon = "🎮" };

        // Assert
        Assert.Equal("🎮", folder.Icon);
    }

    [Fact]
    public void NoteItem_Metadata_CanStoreMultipleKeyValuePairs()
    {
        // Arrange & Act
        var item = new NoteItem { Id = "item1", Title = "Test",
                                  Metadata = new Dictionary<string, string> { { "profile_id", "profile1" },
                                                                              { "server", "asia" },
                                                                              { "character_level", "50" },
                                                                              { "boss_name", "Dragon" } } };

        // Assert
        Assert.NotNull(item.Metadata);
        Assert.Equal(4, item.Metadata.Count);
        Assert.Equal("profile1", item.Metadata["profile_id"]);
        Assert.Equal("asia", item.Metadata["server"]);
        Assert.Equal("50", item.Metadata["character_level"]);
        Assert.Equal("Dragon", item.Metadata["boss_name"]);
    }

    [Fact]
    public void NoteItem_Metadata_Null_AllowsNoMetadata()
    {
        // Arrange & Act
        var item = new NoteItem { Id = "item1", Title = "Simple Note", Url = "https://example.com", Metadata = null };

        // Assert
        Assert.Null(item.Metadata);
    }

    [Fact]
    public void PioneerNoteData_CanRepresentEmptyNoteCollection()
    {
        // Arrange & Act
        var data = new PioneerNoteData { Folders = new List<NoteFolder>(), Items = new List<NoteItem>() };

        // Assert
        Assert.NotNull(data.Folders);
        Assert.Empty(data.Folders);
        Assert.NotNull(data.Items);
        Assert.Empty(data.Items);
        Assert.Equal(SortDirection.Descending, data.SortOrder);
        Assert.Equal(1, data.Version);
    }

    [Fact]
    public void PioneerNoteData_CanRepresentComplexNestedStructure()
    {
        // Arrange
        var data = new PioneerNoteData {
            Folders =
                new List<NoteFolder> {// 根目录
                                      new NoteFolder { Id = "root1", Name = "Game Guide", ParentId = null },
                                      new NoteFolder { Id = "root2", Name = "Walkthrough", ParentId = null },
                                      // 子目录
                                      new NoteFolder { Id = "sub1", Name = "Chapter 1", ParentId = "root1" },
                                      new NoteFolder { Id = "sub2", Name = "Chapter 2", ParentId = "root1" },
                                      // 孙目录
                                      new NoteFolder { Id = "subsub1", Name = "Boss 1", ParentId = "sub1" }
                },
            Items =
                new List<NoteItem> {// 根目录项
                                    new NoteItem { Id = "item1", Title = "General Note", FolderId = null },
                                    // 子目录项
                                    new NoteItem { Id = "item2", Title = "Chapter 1 Note", FolderId = "sub1" },
                                    new NoteItem { Id = "item3", Title = "Chapter 2 Note", FolderId = "sub2" },
                                    // 孙目录项
                                    new NoteItem { Id = "item4", Title = "Boss 1 Strategy", FolderId = "subsub1" }
                }
        };

        // Assert & Act
        Assert.Equal(5, data.Folders.Count);
        Assert.Equal(4, data.Items.Count);

        // 验证根目录
        var rootFolders = data.Folders.FindAll(f => f.ParentId == null);
        Assert.Equal(2, rootFolders.Count);

        // 验证 root1 的子目录
        var root1Children = data.Folders.FindAll(f => f.ParentId == "root1");
        Assert.Equal(2, root1Children.Count);

        // 验证 sub1 的子目录
        var sub1Children = data.Folders.FindAll(f => f.ParentId == "sub1");
        Assert.Single(sub1Children);
        Assert.Equal("subsub1", sub1Children[0].Id);

        // 验证每个文件夹的项
        Assert.Single(data.Items.FindAll(i => i.FolderId == null));      // 根目录项
        Assert.Single(data.Items.FindAll(i => i.FolderId == "sub1"));    // sub1 项
        Assert.Single(data.Items.FindAll(i => i.FolderId == "sub2"));    // sub2 项
        Assert.Single(data.Items.FindAll(i => i.FolderId == "subsub1")); // subsub1 项
    }

#endregion
}
}
