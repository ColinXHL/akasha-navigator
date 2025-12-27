using System.Collections.Generic;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Services;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 开荒笔记服务接口
    /// 负责笔记数据的 CRUD 操作
    /// </summary>
    public interface IPioneerNoteService
    {
        /// <summary>
        /// 当前排序方向
        /// </summary>
        SortDirection CurrentSortOrder { get; set; }

        /// <summary>
        /// 记录笔记
        /// </summary>
        /// <param name="url">页面 URL</param>
        /// <param name="title">笔记标题</param>
        /// <param name="folderId">目标目录 ID（null 表示根目录）</param>
        /// <returns>创建的笔记项</returns>
        NoteItem RecordNote(string url, string title, string? folderId = null);

        /// <summary>
        /// 更新笔记标题
        /// </summary>
        /// <param name="id">笔记项 ID</param>
        /// <param name="newTitle">新标题</param>
        /// <param name="newUrl">新 URL（可选，为 null 时不更新）</param>
        void UpdateNote(string id, string newTitle, string? newUrl = null);

        /// <summary>
        /// 删除笔记
        /// </summary>
        /// <param name="id">笔记项 ID</param>
        void DeleteNote(string id);

        /// <summary>
        /// 移动笔记到指定目录
        /// </summary>
        /// <param name="id">笔记项 ID</param>
        /// <param name="targetFolderId">目标目录 ID（null 表示根目录）</param>
        void MoveNote(string id, string? targetFolderId);

        /// <summary>
        /// 根据 ID 获取笔记项
        /// </summary>
        /// <param name="id">笔记项 ID</param>
        /// <returns>笔记项，不存在返回 null</returns>
        NoteItem? GetNoteById(string id);

        /// <summary>
        /// 创建目录
        /// </summary>
        /// <param name="name">目录名称</param>
        /// <param name="parentId">父目录 ID（null 表示根目录）</param>
        /// <returns>创建的目录</returns>
        NoteFolder CreateFolder(string name, string? parentId = null);

        /// <summary>
        /// 更新目录名称
        /// </summary>
        /// <param name="id">目录 ID</param>
        /// <param name="newName">新名称</param>
        void UpdateFolder(string id, string newName);

        /// <summary>
        /// 删除目录
        /// </summary>
        /// <param name="id">目录 ID</param>
        /// <param name="cascade">是否级联删除子目录和笔记项</param>
        void DeleteFolder(string id, bool cascade = true);

        /// <summary>
        /// 根据 ID 获取目录
        /// </summary>
        /// <param name="id">目录 ID</param>
        /// <returns>目录，不存在返回 null</returns>
        NoteFolder? GetFolderById(string id);

        /// <summary>
        /// 检查目录是否存在
        /// </summary>
        /// <param name="id">目录 ID</param>
        /// <returns>是否存在</returns>
        bool FolderExists(string id);

        /// <summary>
        /// 获取完整的笔记数据（包含目录和项目）
        /// </summary>
        /// <returns>笔记数据</returns>
        PioneerNoteData GetNoteTree();

        /// <summary>
        /// 获取指定目录下的笔记项
        /// </summary>
        /// <param name="folderId">目录 ID（null 表示根目录）</param>
        /// <returns>笔记项列表</returns>
        List<NoteItem> GetItemsByFolder(string? folderId);

        /// <summary>
        /// 获取指定目录下的子目录
        /// </summary>
        /// <param name="parentId">父目录 ID（null 表示根目录）</param>
        /// <returns>子目录列表</returns>
        List<NoteFolder> GetFoldersByParent(string? parentId);

        /// <summary>
        /// 获取排序后的所有笔记项
        /// </summary>
        /// <param name="direction">排序方向</param>
        /// <returns>排序后的笔记项列表</returns>
        List<NoteItem> GetSortedItems(SortDirection direction);

        /// <summary>
        /// 搜索笔记
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <returns>匹配的笔记项列表</returns>
        List<NoteItem> SearchNotes(string keyword);

        /// <summary>
        /// 切换排序方向
        /// </summary>
        /// <returns>切换后的排序方向</returns>
        SortDirection ToggleSortOrder();

        /// <summary>
        /// 获取所有笔记项数量
        /// </summary>
        /// <returns>笔记项数量</returns>
        int GetItemCount();

        /// <summary>
        /// 获取所有目录数量
        /// </summary>
        /// <returns>目录数量</returns>
        int GetFolderCount();

        /// <summary>
        /// 检查 URL 是否已记录
        /// </summary>
        /// <param name="url">要检查的 URL</param>
        /// <returns>如果 URL 已记录返回 true，否则返回 false</returns>
        bool IsUrlRecorded(string url);

        // 以下方法用于接口兼容（与现有UI代码兼容）

        /// <summary>
        /// 获取所有笔记项（按排序规则）
        /// </summary>
        List<NoteItem> GetAllNotes();

        /// <summary>
        /// 获取指定目录下的笔记项
        /// </summary>
        /// <param name="folderId">目录 ID（null 表示根目录）</param>
        List<NoteItem> GetNotesInFolder(string? folderId);

        /// <summary>
        /// 重命名文件夹
        /// </summary>
        /// <param name="folderId">文件夹 ID</param>
        /// <param name="newName">新名称</param>
        void RenameFolder(string folderId, string newName);

        /// <summary>
        /// 删除文件夹（及其内容）
        /// </summary>
        /// <param name="folderId">文件夹 ID</param>
        void DeleteFolder(string folderId);

        /// <summary>
        /// 获取所有文件夹（树形结构）
        /// </summary>
        List<NoteFolder> GetAllFolders();

        /// <summary>
        /// 移动笔记项到指定文件夹
        /// </summary>
        /// <param name="noteId">笔记项 ID</param>
        /// <param name="targetFolderId">目标文件夹 ID（null 表示根目录）</param>
        void MoveNoteToFolder(string noteId, string? targetFolderId);

        /// <summary>
        /// 清空所有数据
        /// </summary>
        void ClearAll();
    }
}
