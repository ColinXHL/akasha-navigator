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
        /// 获取所有笔记项（按排序规则）
        /// </summary>
        List<NoteItem> GetAllNotes();

        /// <summary>
        /// 获取指定目录下的笔记项
        /// </summary>
        /// <param name="folderId">目录 ID（null 表示根目录）</param>
        List<NoteItem> GetNotesInFolder(string? folderId);

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="name">文件夹名称</param>
        /// <param name="parentId">父目录 ID（null 表示根目录）</param>
        /// <returns>创建的文件夹</returns>
        NoteFolder CreateFolder(string name, string? parentId = null);

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
        /// 检查文件夹是否存在
        /// </summary>
        /// <param name="folderId">文件夹 ID</param>
        bool FolderExists(string folderId);

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
