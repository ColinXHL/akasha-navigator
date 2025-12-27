using System;
using System.Collections.Generic;
using AkashaNavigator.Models.Data;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 字幕服务接口
    /// 负责拦截、解析和管理 B站视频字幕数据
    /// </summary>
    public interface ISubtitleService
    {
        /// <summary>
        /// 字幕数据加载完成事件
        /// </summary>
        event EventHandler<SubtitleData>? SubtitleLoaded;

        /// <summary>
        /// 当前字幕变化事件
        /// </summary>
        event EventHandler<SubtitleEntry?>? SubtitleChanged;

        /// <summary>
        /// 字幕数据清除事件
        /// </summary>
        event EventHandler? SubtitleCleared;

        /// <summary>
        /// 解析 B站字幕 JSON 数据
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <param name="sourceUrl">来源 URL</param>
        /// <returns>解析后的字幕数据，解析失败返回 null</returns>
        SubtitleData? ParseSubtitleJson(string json, string sourceUrl);

        /// <summary>
        /// 获取当前字幕数据
        /// </summary>
        SubtitleData? GetSubtitleData();

        /// <summary>
        /// 根据时间戳获取字幕
        /// </summary>
        /// <param name="timeInSeconds">时间戳（秒）</param>
        /// <returns>匹配的字幕条目，无匹配返回 null</returns>
        SubtitleEntry? GetSubtitleAt(double timeInSeconds);

        /// <summary>
        /// 获取所有字幕（返回缓存的不可变数组）
        /// </summary>
        IReadOnlyList<SubtitleEntry> GetAllSubtitles();

        /// <summary>
        /// 清除字幕数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 更新当前播放时间，检测字幕变化
        /// </summary>
        /// <param name="timeInSeconds">当前播放时间（秒）</param>
        void UpdateCurrentTime(double timeInSeconds);
    }
}
