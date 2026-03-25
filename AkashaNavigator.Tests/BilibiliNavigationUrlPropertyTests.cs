using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests;

/// <summary>
/// B站导航URL构建属性测试
/// 验证 bilibili-page-list 插件的导航 URL 构建逻辑
/// </summary>
public class BilibiliNavigationUrlPropertyTests
{
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
    /// 解析URL以验证构建结果
    /// </summary>
    public static (bool isValid, string? videoId, string? idType, int page) ParseUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return (false, null, null, 1);
        }

        // 检查是否为B站视频URL
        var bilibiliPattern = new Regex(@"bilibili\.com/video/(BV[a-zA-Z0-9]+|av\d+)", RegexOptions.IgnoreCase);
        var match = bilibiliPattern.Match(url);

        if (!match.Success)
        {
            return (false, null, null, 1);
        }

        var videoIdStr = match.Groups[1].Value;
        string? videoId = null;
        string? idType = null;

        // 判断ID类型
        if (videoIdStr.StartsWith("BV", StringComparison.OrdinalIgnoreCase))
        {
            videoId = videoIdStr;
            idType = "bvid";
        }
        else if (videoIdStr.StartsWith("av", StringComparison.OrdinalIgnoreCase))
        {
            videoId = videoIdStr.Substring(2); // 去掉 'av' 前缀
            idType = "avid";
        }

        // 提取分P参数
        var pageMatch = Regex.Match(url, @"[?&]p=(\d+)");
        var page = pageMatch.Success ? int.Parse(pageMatch.Groups[1].Value) : 1;

        return (true, videoId, idType, page);
    }

#region Property 4 : Navigation URL Construction

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* valid BVid and page number, the constructed navigation URL SHALL be a valid Bilibili URL.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_BVid_ShouldProduceValidUrl()
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
                var url = BuildNavigationUrl(bvid, "bvid", page);
                var (isValid, parsedVideoId, parsedIdType, parsedPage) = ParseUrl(url);

                return (isValid && parsedVideoId == bvid && parsedIdType == "bvid" && parsedPage == page)
                    .Label(
                        $"URL: {url}, Valid: {isValid}, VideoId: {parsedVideoId} (expected {bvid}), Page: {parsedPage} (expected {page})");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* valid AVid and page number, the constructed navigation URL SHALL be a valid Bilibili URL.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_AVid_ShouldProduceValidUrl()
    {
        var avidGen = Gen.Choose(1, 999999999);
        var pageGen = Gen.Choose(1, 100);

        return Prop.ForAll(
            avidGen.ToArbitrary(), pageGen.ToArbitrary(),
            (avid, page) =>
            {
                var url = BuildNavigationUrl(avid.ToString(), "avid", page);
                var (isValid, parsedVideoId, parsedIdType, parsedPage) = ParseUrl(url);

                return (isValid && parsedVideoId == avid.ToString() && parsedIdType == "avid" && parsedPage == page)
                    .Label(
                        $"URL: {url}, Valid: {isValid}, VideoId: {parsedVideoId} (expected {avid}), Page: {parsedPage} (expected {page})");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* video ID and page=1, the constructed URL SHALL NOT contain a page parameter.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_Page1_ShouldNotHavePageParam()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select "BV" +
            new string(chars);

        return Prop.ForAll(bvidGen.ToArbitrary(),
                           bvid =>
                           {
                               var url = BuildNavigationUrl(bvid, "bvid", 1);
                               var hasPageParam = url.Contains("?p=");

                               return (!hasPageParam).Label($"URL: {url}, HasPageParam: {hasPageParam}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* video ID and page>1, the constructed URL SHALL contain the correct page parameter.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_PageGreaterThan1_ShouldHavePageParam()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select "BV" +
            new string(chars);

        var pageGen = Gen.Choose(2, 100);

        return Prop.ForAll(bvidGen.ToArbitrary(), pageGen.ToArbitrary(),
                           (bvid, page) =>
                           {
                               var url = BuildNavigationUrl(bvid, "bvid", page);
                               var expectedParam = $"?p={page}";
                               var hasCorrectParam = url.Contains(expectedParam);

                               return hasCorrectParam.Label(
                                   $"URL: {url}, Expected: {expectedParam}, HasCorrectParam: {hasCorrectParam}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* BVid, the constructed URL SHALL start with the correct base URL and contain the BVid.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_BVid_ShouldHaveCorrectFormat()
    {
        var bvidGen =
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select "BV" +
            new string(chars);

        var pageGen = Gen.Choose(1, 100);

        return Prop.ForAll(bvidGen.ToArbitrary(), pageGen.ToArbitrary(),
                           (bvid, page) =>
                           {
                               var url = BuildNavigationUrl(bvid, "bvid", page);
                               var startsWithBase = url.StartsWith("https://www.bilibili.com/video/");
                               var containsBvid = url.Contains(bvid);

                               return (startsWithBase && containsBvid)
                                   .Label(
                                       $"URL: {url}, StartsWithBase: {startsWithBase}, ContainsBvid: {containsBvid}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* AVid, the constructed URL SHALL start with the correct base URL and contain 'av' prefix.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_AVid_ShouldHaveCorrectFormat()
    {
        var avidGen = Gen.Choose(1, 999999999);
        var pageGen = Gen.Choose(1, 100);

        return Prop.ForAll(
            avidGen.ToArbitrary(), pageGen.ToArbitrary(),
            (avid, page) =>
            {
                var url = BuildNavigationUrl(avid.ToString(), "avid", page);
                var startsWithBase = url.StartsWith("https://www.bilibili.com/video/");
                var containsAvPrefix = url.Contains($"av{avid}");

                return (startsWithBase && containsAvPrefix)
                    .Label($"URL: {url}, StartsWithBase: {startsWithBase}, ContainsAvPrefix: {containsAvPrefix}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 4: Navigation URL Construction (Round-Trip)**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* video ID and page number, constructing a URL and then parsing it
    /// SHALL yield the same video ID and page number.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildNavigationUrl_RoundTrip_ShouldPreserveData()
    {
        var videoIdGen = Gen.OneOf(
            // BVid
            from chars in Gen.ArrayOf(
                10, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
                select("BV" + new string(chars), "bvid"),
            // AVid
            from avid in Gen.Choose(1, 999999999) select(avid.ToString(), "avid"));

        var pageGen = Gen.Choose(1, 100);

        return Prop.ForAll(
            videoIdGen.ToArbitrary(), pageGen.ToArbitrary(),
            (videoIdData, page) =>
            {
                var (videoId, idType) = videoIdData;
                var url = BuildNavigationUrl(videoId, idType, page);
                var (isValid, parsedVideoId, parsedIdType, parsedPage) = ParseUrl(url);

                return (isValid && parsedVideoId == videoId && parsedIdType == idType && parsedPage == page)
                    .Label(
                        $"URL: {url}, VideoId: {parsedVideoId} (expected {videoId}), Type: {parsedIdType} (expected {idType}), Page: {parsedPage} (expected {page})");
            });
    }

#endregion

#region Edge Cases

    /// <summary>
    /// 边界测试：Page = 0 应该不包含页面参数（虽然不是有效输入，但测试健壮性）
    /// </summary>
    [Fact]
    public void BuildNavigationUrl_Page0_ShouldNotHavePageParam()
    {
        var url = BuildNavigationUrl("BV1xx411c7mD", "bvid", 0);
        Assert.DoesNotContain("?p=", url);
    }

    /// <summary>
    /// 边界测试：负数页码应该不包含页面参数
    /// </summary>
    [Fact]
    public void BuildNavigationUrl_NegativePage_ShouldNotHavePageParam()
    {
        var url = BuildNavigationUrl("BV1xx411c7mD", "bvid", -1);
        Assert.DoesNotContain("?p=", url);
    }

    /// <summary>
    /// 示例测试：验证具体的 BVid URL 构建
    /// </summary>
    [Fact]
    public void BuildNavigationUrl_BVid_Example()
    {
        var url = BuildNavigationUrl("BV1xx411c7mD", "bvid", 1);
        Assert.Equal("https://www.bilibili.com/video/BV1xx411c7mD", url);

        var urlWithPage = BuildNavigationUrl("BV1xx411c7mD", "bvid", 3);
        Assert.Equal("https://www.bilibili.com/video/BV1xx411c7mD?p=3", urlWithPage);
    }

    /// <summary>
    /// 示例测试：验证具体的 AVid URL 构建
    /// </summary>
    [Fact]
    public void BuildNavigationUrl_AVid_Example()
    {
        var url = BuildNavigationUrl("170001", "avid", 1);
        Assert.Equal("https://www.bilibili.com/video/av170001", url);

        var urlWithPage = BuildNavigationUrl("170001", "avid", 5);
        Assert.Equal("https://www.bilibili.com/video/av170001?p=5", urlWithPage);
    }

#endregion
}
