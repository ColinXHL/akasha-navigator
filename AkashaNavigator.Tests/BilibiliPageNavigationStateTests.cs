using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests;

/// <summary>
/// B站分P导航状态同步属性测试
/// 验证跨视频切换后分P导航状态的一致性
/// 回归场景：切换不同视频（合集）后，分P信息不应仍指向旧视频
/// </summary>
public class BilibiliPageNavigationStateTests
{
    /// <summary>
    /// 导航状态模型（与 JavaScript 插件 state 对应）
    /// </summary>
    public class NavigationState
    {
        public string? CurrentVideoId { get; set; }
        public string? CurrentVideoIdType { get; set; }
        public int CurrentPage { get; set; } = 1;
        public List<PageInfo> PageList { get; set; } = new();
    }

    /// <summary>
    /// 分P信息（与 JavaScript 插件 pageList 项对应）
    /// </summary>
    public class PageInfo
    {
        public long Cid { get; set; }
        public int Page { get; set; }
        public string Part { get; set; } = string.Empty;
        public int Duration { get; set; }
    }

    /// <summary>
    /// URL 解析结果（与 JavaScript parseUrl 对应）
    /// </summary>
    public class UrlParseResult
    {
        public bool IsBilibili { get; set; }
        public string? VideoId { get; set; }
        public string? VideoIdType { get; set; }
        public int CurrentPage { get; set; } = 1;
        public bool HasPageParam { get; set; }
    }

    /// <summary>
    /// 解析B站视频URL（与 JavaScript 实现逻辑一致）
    /// </summary>
    public static UrlParseResult ParseUrl(string? url)
    {
        var result = new UrlParseResult { IsBilibili = false, VideoId = null, VideoIdType = null, CurrentPage = 1 };

        if (string.IsNullOrEmpty(url))
            return result;

        var bilibiliPattern = new Regex(@"bilibili\.com/video/(BV[a-zA-Z0-9]+|av\d+)", RegexOptions.IgnoreCase);
        var match = bilibiliPattern.Match(url);

        if (!match.Success)
            return result;

        result.IsBilibili = true;
        var videoIdStr = match.Groups[1].Value;

        if (videoIdStr.StartsWith("BV", StringComparison.OrdinalIgnoreCase))
        {
            result.VideoId = videoIdStr;
            result.VideoIdType = "bvid";
        }
        else if (videoIdStr.StartsWith("av", StringComparison.OrdinalIgnoreCase))
        {
            result.VideoId = videoIdStr.Substring(2);
            result.VideoIdType = "avid";
        }

        var pageMatch = Regex.Match(url, @"[?&]p=(\d+)");
        if (pageMatch.Success)
        {
            result.CurrentPage = int.Parse(pageMatch.Groups[1].Value);
            result.HasPageParam = true;
        }

        return result;
    }

    /// <summary>
    /// 构建导航URL（与 JavaScript 实现逻辑一致）
    /// </summary>
    public static string BuildNavigationUrl(string videoId, string idType, int page)
    {
        var baseUrl = "https://www.bilibili.com/video/";

        if (idType == "bvid")
        {
            baseUrl += videoId;
        }
        else
        {
            baseUrl += "av" + videoId;
        }

        if (page > 1)
        {
            baseUrl += "?p=" + page;
        }

        return baseUrl;
    }

    /// <summary>
    /// 模拟 onUrlChanged 的状态刷新逻辑（与 JavaScript 实现对应）
    /// 当检测到不同视频上下文时，应失效旧缓存并重建
    /// </summary>
    public static NavigationState OnUrlChanged(NavigationState state, string url)
    {
        var parseResult = ParseUrl(url);

        if (!parseResult.IsBilibili)
        {
            // 非B站视频，清空状态
            return new NavigationState
            {
                CurrentVideoId = null,
                CurrentVideoIdType = null,
                CurrentPage = 1,
                PageList = new List<PageInfo>()
            };
        }

        // 同一个视频 → 只更新页码
        if (state.CurrentVideoId == parseResult.VideoId)
        {
            state.CurrentPage = parseResult.HasPageParam ? parseResult.CurrentPage : state.CurrentPage;
            return state;
        }

        // 不同视频 → 失效旧缓存，重建
        return new NavigationState
        {
            CurrentVideoId = parseResult.VideoId,
            CurrentVideoIdType = parseResult.VideoIdType,
            CurrentPage = parseResult.HasPageParam ? parseResult.CurrentPage : 1,
            PageList = new List<PageInfo>() // 新视频的分P列表需要重新获取
        };
    }

    /// <summary>
    /// 模拟导航上下文同步（与 JavaScript syncNavigationContext 对应）
    /// 快捷键导航前必须调用此函数确保上下文与当前页面一致
    /// </summary>
    public static bool SyncNavigationContext(NavigationState state, string currentUrl)
    {
        var parseResult = ParseUrl(currentUrl);

        if (!parseResult.IsBilibili)
            return false;

        // 缓存的视频ID与当前URL不一致 → 上下文过期
        if (state.CurrentVideoId != parseResult.VideoId)
            return false;

        // 视频ID一致但分P列表为空 → 未就绪
        if (state.PageList == null || state.PageList.Count == 0)
            return false;

        return true;
    }

    #region Cross-video navigation state tests

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Regression Test**
    /// **Validates: Spec requirement "Page navigation hotkeys SHALL use current video context"**
    ///
    /// 切换到新视频后，导航状态中的 currentVideoId 必须指向新视频，
    /// 构建的导航URL也必须指向新视频，而非旧视频。
    /// </summary>
    [Fact]
    public void CrossVideoSwitch_NavigationState_UsesNewVideoId()
    {
        // 初始状态：视频 A
        var state = new NavigationState
        {
            CurrentVideoId = "BV1GJ411x7h7",
            CurrentVideoIdType = "bvid",
            CurrentPage = 3,
            PageList = new List<PageInfo>
            {
                new() { Cid = 100, Page = 1, Part = "P1", Duration = 600 },
                new() { Cid = 101, Page = 2, Part = "P2", Duration = 700 },
                new() { Cid = 102, Page = 3, Part = "P3", Duration = 800 }
            }
        };

        // 模拟用户切到视频 B
        var newUrl = "https://www.bilibili.com/video/BV1xx411c7mD?p=1";
        var updatedState = OnUrlChanged(state, newUrl);

        // 验证：状态已切换到新视频
        Assert.Equal("BV1xx411c7mD", updatedState.CurrentVideoId);
        Assert.Equal("bvid", updatedState.CurrentVideoIdType);
        Assert.Equal(1, updatedState.CurrentPage);

        // 验证：使用新状态构建导航URL不包含旧视频ID
        var navUrl = BuildNavigationUrl(
            updatedState.CurrentVideoId!,
            updatedState.CurrentVideoIdType!,
            updatedState.CurrentPage + 1);

        Assert.DoesNotContain("BV1GJ411x7h7", navUrl);
        Assert.Contains("BV1xx411c7mD", navUrl);
    }

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Regression Test**
    /// **Validates: Spec scenario "Context refresh failure blocks incorrect navigation"**
    ///
    /// 当上下文同步失败（缓存视频ID与当前URL不匹配）时，
    /// syncNavigationContext 必须返回 false，阻止导航。
    /// </summary>
    [Fact]
    public void SyncNavigationContext_StaleCache_ReturnsFalse()
    {
        // 状态仍指向视频 A
        var state = new NavigationState
        {
            CurrentVideoId = "BV1GJ411x7h7",
            CurrentVideoIdType = "bvid",
            CurrentPage = 2,
            PageList = new List<PageInfo>
            {
                new() { Cid = 100, Page = 1, Part = "P1", Duration = 600 },
                new() { Cid = 101, Page = 2, Part = "P2", Duration = 700 }
            }
        };

        // 但当前页面已经切到视频 B
        var currentUrl = "https://www.bilibili.com/video/BV1xx411c7mD?p=1";

        // 上下文同步必须失败
        Assert.False(SyncNavigationContext(state, currentUrl));
    }

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Regression Test**
    /// **Validates: Spec scenario "Switching to a new video rebuilds navigation state"**
    ///
    /// 切换到新视频后，旧分P列表必须被清空（需要重新获取），
    /// 不能保留旧视频的分P列表。
    /// </summary>
    [Fact]
    public void OnUrlChanged_DifferentVideo_ClearsOldPageList()
    {
        // 初始状态：视频 A，有 3 个分P
        var state = new NavigationState
        {
            CurrentVideoId = "BV1GJ411x7h7",
            CurrentVideoIdType = "bvid",
            CurrentPage = 3,
            PageList = new List<PageInfo>
            {
                new() { Cid = 100, Page = 1, Part = "P1", Duration = 600 },
                new() { Cid = 101, Page = 2, Part = "P2", Duration = 700 },
                new() { Cid = 102, Page = 3, Part = "P3", Duration = 800 }
            }
        };

        // 切到视频 B
        var newUrl = "https://www.bilibili.com/video/BV1xx411c7mD";
        var updatedState = OnUrlChanged(state, newUrl);

        // 旧分P列表必须被清空
        Assert.Empty(updatedState.PageList);
        // currentVideoId 必须指向新视频
        Assert.Equal("BV1xx411c7mD", updatedState.CurrentVideoId);
    }

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Regression Test**
    /// **Validates: Spec scenario "Same-video page change preserves current context"**
    ///
    /// 同一个视频的不同分P之间切换时，必须保留当前 video context，
    /// 只更新 current page。
    /// </summary>
    [Fact]
    public void OnUrlChanged_SameVideo_UpdatesPagePreservesContext()
    {
        // 初始状态：视频 A，第1P
        var pageList = new List<PageInfo>
        {
            new() { Cid = 100, Page = 1, Part = "P1", Duration = 600 },
            new() { Cid = 101, Page = 2, Part = "P2", Duration = 700 },
            new() { Cid = 102, Page = 3, Part = "P3", Duration = 800 }
        };

        var state = new NavigationState
        {
            CurrentVideoId = "BV1GJ411x7h7",
            CurrentVideoIdType = "bvid",
            CurrentPage = 1,
            PageList = pageList
        };

        // 切到同视频的第3P
        var newUrl = "https://www.bilibili.com/video/BV1GJ411x7h7?p=3";
        var updatedState = OnUrlChanged(state, newUrl);

        // video context 不变
        Assert.Equal("BV1GJ411x7h7", updatedState.CurrentVideoId);
        Assert.Equal("bvid", updatedState.CurrentVideoIdType);
        // 页码更新
        Assert.Equal(3, updatedState.CurrentPage);
        // 分P列表保留
        Assert.Equal(3, updatedState.PageList.Count);
    }

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Property Test**
    /// **Validates: Cross-video switch always builds navigation URL for the current video**
    ///
    /// *For any* initial video, target video, and page number,
    /// after switching from the initial video to the target video,
    /// the navigation URL built from the updated state SHALL always reference the target video.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CrossVideoSwitch_NavigationUrl_AlwaysTargetsCurrentVideo()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
            select "BV" + new string(chars);

        var pageGen = Gen.Choose(1, 100);
        var crossVideoGen =
            from oldBvid in bvidGen
            from newBvid in bvidGen
            from oldPage in pageGen
            from newPage in pageGen
            select (oldBvid, newBvid, oldPage, newPage);

        return Prop.ForAll(
            crossVideoGen.ToArbitrary(),
            tuple =>
            {
                var (oldBvid, newBvid, oldPage, newPage) = tuple;

                // 确保两个视频ID不同
                if (oldBvid == newBvid) return true.ToProperty();

                // 初始状态：旧视频
                var state = new NavigationState
                {
                    CurrentVideoId = oldBvid,
                    CurrentVideoIdType = "bvid",
                    CurrentPage = oldPage,
                    PageList = new List<PageInfo>
                    {
                        new() { Cid = 1, Page = 1, Part = "P1", Duration = 600 }
                    }
                };

                // 切到新视频
                var newUrl = $"https://www.bilibili.com/video/{newBvid}?p={newPage}";
                var updatedState = OnUrlChanged(state, newUrl);

                // 用新状态构建导航URL
                var navUrl = BuildNavigationUrl(
                    updatedState.CurrentVideoId!,
                    updatedState.CurrentVideoIdType!,
                    Math.Max(1, updatedState.CurrentPage));

                // 导航URL必须包含新视频ID，不包含旧视频ID
                var containsNew = navUrl.Contains(newBvid);
                var containsOld = navUrl.Contains(oldBvid);
                var idMatches = updatedState.CurrentVideoId == newBvid;

                return (containsNew && !containsOld && idMatches)
                    .Label($"NavURL: {navUrl}, ContainsNew: {containsNew}, ContainsOld: {containsOld}, IdMatches: {idMatches}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Property Test**
    /// **Validates: syncNavigationContext returns false when state videoId doesn't match URL**
    ///
    /// *For any* pair of different video IDs,
    /// syncNavigationContext SHALL return false when the cached videoId
    /// differs from the one in the current URL.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SyncNavigationContext_MismatchedVideoId_ReturnsFalse()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
            select "BV" + new string(chars);

        return Prop.ForAll(
            bvidGen.ToArbitrary(), bvidGen.ToArbitrary(),
            (cachedBvid, urlBvid) =>
            {
                // 确保两个ID不同
                if (cachedBvid == urlBvid) return true.ToProperty();

                var state = new NavigationState
                {
                    CurrentVideoId = cachedBvid,
                    CurrentVideoIdType = "bvid",
                    CurrentPage = 1,
                    PageList = new List<PageInfo>
                    {
                        new() { Cid = 1, Page = 1, Part = "P1", Duration = 600 }
                    }
                };

                var currentUrl = $"https://www.bilibili.com/video/{urlBvid}";

                var contextValid = SyncNavigationContext(state, currentUrl);
                return (!contextValid)
                    .Label($"CachedVideoId: {cachedBvid}, UrlVideoId: {urlBvid}, ContextValid: {contextValid}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-navigation-state-sync, Property Test**
    /// **Validates: syncNavigationContext returns true when state matches current URL**
    ///
    /// *For any* valid BVid with a non-empty page list,
    /// syncNavigationContext SHALL return true when the cached videoId matches the URL.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SyncNavigationContext_MatchingVideoId_ReturnsTrue()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
            select "BV" + new string(chars);

        var pageGen = Gen.Choose(1, 50);

        return Prop.ForAll(
            bvidGen.ToArbitrary(), pageGen.ToArbitrary(),
            (bvid, page) =>
            {
                var pageList = new List<PageInfo>();
                for (int i = 1; i <= page; i++)
                {
                    pageList.Add(new PageInfo { Cid = 1000 + i, Page = i, Part = $"P{i}", Duration = 600 });
                }

                var state = new NavigationState
                {
                    CurrentVideoId = bvid,
                    CurrentVideoIdType = "bvid",
                    CurrentPage = 1,
                    PageList = pageList
                };

                var currentUrl = $"https://www.bilibili.com/video/{bvid}";

                var contextValid = SyncNavigationContext(state, currentUrl);
                return contextValid
                    .Label($"BVid: {bvid}, Page: {page}, ContextValid: {contextValid}");
            });
    }

    #endregion
}