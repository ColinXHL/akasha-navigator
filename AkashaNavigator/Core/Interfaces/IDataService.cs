using System.Collections.Generic;
using AkashaNavigator.Models.Data;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 数据服务接口
    /// 负责历史记录和收藏夹的 CRUD 操作
    /// </summary>
    public interface IDataService
    {
        #region History Methods

        /// <summary>
        /// 获取所有历史记录（按访问时间降序）
        /// </summary>
        List<HistoryItem> GetHistory();

        /// <summary>
        /// 添加或更新历史记录
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="title">标题</param>
        void AddHistory(string url, string title);

        /// <summary>
        /// 删除历史记录
        /// </summary>
        /// <param name="id">历史记录 ID</param>
        void DeleteHistory(int id);

        /// <summary>
        /// 清空所有历史记录
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// 搜索历史记录
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        List<HistoryItem> SearchHistory(string keyword);

        #endregion

        #region Bookmark Methods

        /// <summary>
        /// 获取所有收藏夹（按排序顺序）
        /// </summary>
        List<BookmarkItem> GetBookmarks();

        /// <summary>
        /// 添加收藏
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="title">标题</param>
        BookmarkItem AddBookmark(string url, string title);

        /// <summary>
        /// 删除收藏
        /// </summary>
        /// <param name="id">收藏 ID</param>
        void DeleteBookmark(int id);

        /// <summary>
        /// 根据 URL 删除收藏
        /// </summary>
        /// <param name="url">URL</param>
        void DeleteBookmarkByUrl(string url);

        /// <summary>
        /// 检查 URL 是否已收藏
        /// </summary>
        /// <param name="url">URL</param>
        bool IsBookmarked(string url);

        /// <summary>
        /// 搜索收藏夹
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        List<BookmarkItem> SearchBookmarks(string keyword);

        /// <summary>
        /// 清空所有收藏
        /// </summary>
        void ClearBookmarks();

        /// <summary>
        /// 切换收藏状态
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="title">标题</param>
        bool ToggleBookmark(string url, string title);

        #endregion
    }
}
