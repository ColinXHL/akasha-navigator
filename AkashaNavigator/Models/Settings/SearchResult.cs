using System;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Models.Settings;

/// <summary>
/// 设置搜索结果
/// </summary>
public class SearchResult
{
    /// <summary>
    /// 结果所属的页面类型
    /// </summary>
    public SettingsPageType PageType { get; set; }

    /// <summary>
    /// 页面显示名称（例如："⚙️ 通用"）
    /// </summary>
    public string PageDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 设置项的显示名称（例如："默认透明度"）
    /// </summary>
    public string SettingDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 设置项的分组名称（例如："基础设置"）
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// 导航命令参数（用于跳转）
    /// </summary>
    public SettingsPageType NavigationTarget => PageType;

    /// <summary>
    /// 匹配的高亮文本（用于显示匹配的搜索关键词）
    /// </summary>
    public string? HighlightedText { get; set; }

    /// <summary>
    /// 搜索结果类型（页面级、设置项级）
    /// </summary>
    public SearchResultType ResultType { get; set; }

    /// <summary>
    /// 显示文本（用于 UI 显示）
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (ResultType == SearchResultType.Page)
            {
                return PageDisplayName;
            }
            // 设置项级别：显示页面名称 + 设置项名称
            return $"{PageDisplayName.Replace("⚙️ ", "").Replace("🔲 ", "").Replace("⌨️ ", "").Replace("🔧 ", "")} → {SettingDisplayName}";
        }
    }

    public SearchResult()
    {
    }

    public SearchResult(SettingsPageType pageType, string pageDisplayName, string settingDisplayName,
                        SearchResultType resultType = SearchResultType.Setting, string? groupName = null)
    {
        PageType = pageType;
        PageDisplayName = pageDisplayName;
        SettingDisplayName = settingDisplayName;
        ResultType = resultType;
        GroupName = groupName;
    }

    /// <summary>
    /// 创建页面级别的搜索结果
    /// </summary>
    public static SearchResult CreatePageResult(SettingsPageType pageType, string pageDisplayName)
    {
        return new SearchResult(pageType, pageDisplayName, pageDisplayName.Replace("⚙️ ", "").Replace("🔲 ", "")
                               .Replace("⌨️ ", "").Replace("🔧 ", ""), SearchResultType.Page);
    }

    /// <summary>
    /// 创建设置项级别的搜索结果
    /// </summary>
    public static SearchResult CreateSettingResult(SettingsPageType pageType, string pageDisplayName,
                                                    string settingDisplayName, string? groupName = null)
    {
        return new SearchResult(pageType, pageDisplayName, settingDisplayName, SearchResultType.Setting, groupName);
    }
}

/// <summary>
/// 搜索结果类型
/// </summary>
public enum SearchResultType
{
    /// <summary>
    /// 页面级别（直接匹配页面名称）
    /// </summary>
    Page,

    /// <summary>
    /// 设置项级别（匹配页面内的具体设置）
    /// </summary>
    Setting
}
