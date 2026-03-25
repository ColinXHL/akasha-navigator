using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// B站URL解析器属性测试
/// 验证 bilibili-page-list 插件的 URL 解析逻辑
/// </summary>
public class BilibiliUrlParserPropertyTests
{
    /// <summary>
    /// URL 解析结果
    /// </summary>
    public class UrlParseResult
    {
        public bool IsBilibili { get; set; }
        public string? VideoId { get; set; }
        public string? VideoIdType { get; set; }
        public int CurrentPage { get; set; } = 1;
    }

    /// <summary>
    /// C# 版本的 URL 解析器（与 JavaScript 实现逻辑一致）
    /// </summary>
    public static UrlParseResult ParseUrl(string? url)
    {
        var result = new UrlParseResult { IsBilibili = false, VideoId = null, VideoIdType = null, CurrentPage = 1 };

        if (string.IsNullOrEmpty(url))
        {
            return result;
        }

        // 检查是否为B站视频URL
        var bilibiliPattern = new Regex(@"bilibili\.com/video/(BV[a-zA-Z0-9]+|av\d+)", RegexOptions.IgnoreCase);
        var match = bilibiliPattern.Match(url);

        if (!match.Success)
        {
            return result;
        }

        result.IsBilibili = true;
        var videoIdStr = match.Groups[1].Value;

        // 判断ID类型
        if (videoIdStr.StartsWith("BV", StringComparison.OrdinalIgnoreCase))
        {
            result.VideoId = videoIdStr;
            result.VideoIdType = "bvid";
        }
        else if (videoIdStr.StartsWith("av", StringComparison.OrdinalIgnoreCase))
        {
            result.VideoId = videoIdStr.Substring(2); // 去掉 'av' 前缀
            result.VideoIdType = "avid";
        }

        // 提取分P参数
        var pageMatch = Regex.Match(url, @"[?&]p=(\d+)");
        if (pageMatch.Success)
        {
            result.CurrentPage = int.Parse(pageMatch.Groups[1].Value);
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

#region Property 1 : URL Parsing Correctness

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* valid BVid, the URL parser SHALL correctly extract the video ID and ID type.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseUrl_BVid_ShouldExtractCorrectly()
    {
        // 生成有效的 BVid（BV + 10个字母数字字符）
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select "BV" +
            new string(chars);

        return Prop.ForAll(
            bvidGen.ToArbitrary(),
            bvid =>
            {
                var url = $"https://www.bilibili.com/video/{bvid}";
                var result = ParseUrl(url);

                return (result.IsBilibili && result.VideoId == bvid && result.VideoIdType == "bvid" &&
                        result.CurrentPage == 1)
                    .Label(
                        $"URL: {url}, IsBilibili: {result.IsBilibili}, VideoId: {result.VideoId}, Type: {result.VideoIdType}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* valid AVid, the URL parser SHALL correctly extract the video ID and ID type.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseUrl_AVid_ShouldExtractCorrectly()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            avid =>
            {
                var url = $"https://www.bilibili.com/video/av{avid.Get}";
                var result = ParseUrl(url);

                return (result.IsBilibili && result.VideoId == avid.Get.ToString() && result.VideoIdType == "avid" &&
                        result.CurrentPage == 1)
                    .Label(
                        $"URL: {url}, IsBilibili: {result.IsBilibili}, VideoId: {result.VideoId}, Type: {result.VideoIdType}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* valid video URL with page parameter, the parser SHALL extract the correct page number.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseUrl_WithPageParam_ShouldExtractPageNumber()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select "BV" +
            new string(chars);

        var pageGen = Gen.Choose(1, 1000);

        return Prop.ForAll(bvidGen.ToArbitrary(), pageGen.ToArbitrary(),
                           (bvid, page) =>
                           {
                               var url = $"https://www.bilibili.com/video/{bvid}?p={page}";
                               var result = ParseUrl(url);

                               return (result.IsBilibili && result.VideoId == bvid && result.VideoIdType == "bvid" &&
                                       result.CurrentPage == page)
                                   .Label($"URL: {url}, CurrentPage: {result.CurrentPage}, Expected: {page}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* non-Bilibili URL, the parser SHALL return isBilibili: false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseUrl_NonBilibiliUrl_ShouldReturnFalse()
    {
        var nonBilibiliDomains = new[] { "youtube.com", "youtu.be",   "vimeo.com",   "twitch.tv",
                                         "google.com",  "github.com", "example.com", "localhost" };

        var domainGen = Gen.Elements(nonBilibiliDomains);
        var pathGen = Gen.Elements("/video/123", "/watch?v=abc", "/channel/xyz", "");

        return Prop.ForAll(domainGen.ToArbitrary(), pathGen.ToArbitrary(),
                           (domain, path) =>
                           {
                               var url = $"https://www.{domain}{path}";
                               var result = ParseUrl(url);

                               return (!result.IsBilibili && result.VideoId == null && result.VideoIdType == null)
                                   .Label($"URL: {url}, IsBilibili: {result.IsBilibili}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* null or empty URL, the parser SHALL return isBilibili: false.
    /// </summary>
    [Fact]
    public void ParseUrl_NullOrEmpty_ShouldReturnFalse()
    {
        var nullResult = ParseUrl(null);
        var emptyResult = ParseUrl("");
        var whitespaceResult = ParseUrl("   ");

        Assert.False(nullResult.IsBilibili);
        Assert.False(emptyResult.IsBilibili);
        // 注意：空白字符串不是空字符串，但也不是有效的B站URL
        Assert.False(whitespaceResult.IsBilibili);
    }

#endregion

#region Round - Trip Property(URL Construction and Parsing)

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness (Round-Trip)**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* valid BVid and page number, constructing a URL and then parsing it
    /// SHALL yield the same video ID and page number.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UrlRoundTrip_BVid_ShouldPreserveData()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select "BV" +
            new string(chars);

        var pageGen = Gen.Choose(1, 100);

        return Prop.ForAll(
            bvidGen.ToArbitrary(), pageGen.ToArbitrary(),
            (bvid, page) =>
            {
                // 构建 URL
                var url = BuildNavigationUrl(bvid, "bvid", page);

                // 解析 URL
                var result = ParseUrl(url);

                // 验证往返一致性
                var videoIdMatches = result.VideoId == bvid;
                var typeMatches = result.VideoIdType == "bvid";
                var pageMatches = result.CurrentPage == page;

                return (videoIdMatches && typeMatches && pageMatches)
                    .Label(
                        $"URL: {url}, VideoId: {result.VideoId} (expected {bvid}), Page: {result.CurrentPage} (expected {page})");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 1: URL Parsing Correctness (Round-Trip)**
    /// **Validates: Requirements 1.1, 1.2, 7.1**
    ///
    /// *For any* valid AVid and page number, constructing a URL and then parsing it
    /// SHALL yield the same video ID and page number.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UrlRoundTrip_AVid_ShouldPreserveData()
    {
        var avidGen = Gen.Choose(1, 999999999);
        var pageGen = Gen.Choose(1, 100);

        return Prop.ForAll(
            avidGen.ToArbitrary(), pageGen.ToArbitrary(),
            (avid, page) =>
            {
                // 构建 URL
                var url = BuildNavigationUrl(avid.ToString(), "avid", page);

                // 解析 URL
                var result = ParseUrl(url);

                // 验证往返一致性
                var videoIdMatches = result.VideoId == avid.ToString();
                var typeMatches = result.VideoIdType == "avid";
                var pageMatches = result.CurrentPage == page;

                return (videoIdMatches && typeMatches && pageMatches)
                    .Label(
                        $"URL: {url}, VideoId: {result.VideoId} (expected {avid}), Page: {result.CurrentPage} (expected {page})");
            });
    }

#endregion
}
}
