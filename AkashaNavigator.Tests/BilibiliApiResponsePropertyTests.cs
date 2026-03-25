using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests;

/// <summary>
/// B站API响应解析器属性测试
/// 验证 bilibili-page-list 插件的 API 响应解析逻辑
/// </summary>
public class BilibiliApiResponsePropertyTests
{
    /// <summary>
    /// 分P信息
    /// </summary>
    public class PageInfo
    {
        public long Cid { get; set; }
        public int Page { get; set; }
        public string Part { get; set; } = string.Empty;
        public int Duration { get; set; }
    }

    /// <summary>
    /// B站API响应结构
    /// </summary>
    public class BilibiliApiResponse
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<PageInfo>? Data { get; set; }
    }

    /// <summary>
    /// C# 版本的 API 响应解析器（与 JavaScript 实现逻辑一致）
    /// </summary>
    public static List<PageInfo> ParseApiResponse(object? response)
    {
        if (response == null)
        {
            return new List<PageInfo>();
        }

        BilibiliApiResponse? data;

        // 如果是字符串，尝试解析JSON
        if (response is string jsonString)
        {
            try
            {
                data = JsonSerializer.Deserialize<BilibiliApiResponse>(jsonString);
            }
            catch
            {
                return new List<PageInfo>();
            }
        }
        else if (response is BilibiliApiResponse apiResponse)
        {
            data = apiResponse;
        }
        else
        {
            return new List<PageInfo>();
        }

        if (data == null || data.Code != 0)
        {
            return new List<PageInfo>();
        }

        if (data.Data == null || !data.Data.Any())
        {
            return new List<PageInfo>();
        }

        return data.Data
            .Select(item => new PageInfo { Cid = item.Cid, Page = item.Page,
                                           Part = string.IsNullOrEmpty(item.Part) ? $"P{item.Page}" : item.Part,
                                           Duration = item.Duration })
            .ToList();
    }

#region Generators

    /// <summary>
    /// 生成有效的分P信息
    /// </summary>
    private static Gen<PageInfo> GenPageInfo(int pageNumber)
    {
        return from cid in Gen.Choose(1000000, 999999999) from duration in
            Gen.Choose(60, 7200) // 1分钟到2小时
            from titleLength in Gen.Choose(1, 50) from titleChars in Gen.ArrayOf(
                titleLength, Gen.Elements("abcdefghijklmnopqrstuvwxyz0123456789 ".ToCharArray()))
                select new PageInfo { Cid = cid, Page = pageNumber, Part = new string(titleChars).Trim(),
                                      Duration = duration };
    }

    /// <summary>
    /// 生成有效的API响应
    /// </summary>
    private static Gen<BilibiliApiResponse> GenValidApiResponse()
    {
        return from pageCount in Gen.Choose(1, 50)
            from pages in Gen.Sequence(Enumerable.Range(1, pageCount).Select(GenPageInfo))
                select new BilibiliApiResponse { Code = 0, Message = "0", Data = pages.ToList() };
    }

#endregion

#region Property 2 : API Response Parsing Completeness

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.4**
    ///
    /// *For any* valid Bilibili pagelist API response, the parser SHALL extract all page entries
    /// with their cid, page number, title, and duration fields intact.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseApiResponse_ValidResponse_ShouldExtractAllFields()
    {
        return Prop.ForAll(
            GenValidApiResponse().ToArbitrary(),
            apiResponse =>
            {
                var result = ParseApiResponse(apiResponse);

                // 验证数量一致
                var countMatches = result.Count == apiResponse.Data!.Count;

                // 验证每个字段都正确提取
                var allFieldsMatch = result
                                         .Zip(apiResponse.Data!,
                                              (parsed, original) =>
                                              {
                                                  var cidMatches = parsed.Cid == original.Cid;
                                                  var pageMatches = parsed.Page == original.Page;
                                                  var durationMatches = parsed.Duration == original.Duration;

                                                  // Part 字段：如果原始为空，应该生成 "P{page}"
                                                  var partMatches = string.IsNullOrEmpty(original.Part)
                                                                        ? parsed.Part == $"P{original.Page}"
                                                                        : parsed.Part == original.Part;

                                                  return cidMatches && pageMatches && durationMatches && partMatches;
                                              })
                                         .All(x => x);

                return (countMatches && allFieldsMatch)
                    .Label(
                        $"Count: {result.Count} (expected {apiResponse.Data!.Count}), AllFieldsMatch: {allFieldsMatch}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.4**
    ///
    /// *For any* valid API response, the number of parsed entries SHALL equal
    /// the number of entries in the response.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseApiResponse_ValidResponse_CountShouldMatch()
    {
        return Prop.ForAll(GenValidApiResponse().ToArbitrary(),
                           apiResponse =>
                           {
                               var result = ParseApiResponse(apiResponse);
                               var expectedCount = apiResponse.Data!.Count;

                               return (result.Count == expectedCount)
                                   .Label($"Parsed count: {result.Count}, Expected: {expectedCount}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.4**
    ///
    /// *For any* valid API response as JSON string, the parser SHALL correctly
    /// deserialize and extract all entries.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseApiResponse_JsonString_ShouldParseCorrectly()
    {
        return Prop.ForAll(GenValidApiResponse().ToArbitrary(),
                           apiResponse =>
                           {
                               // 序列化为JSON字符串
                               var jsonString = JsonSerializer.Serialize(apiResponse);

                               // 解析JSON字符串
                               var result = ParseApiResponse(jsonString);

                               // 验证结果
                               var countMatches = result.Count == apiResponse.Data!.Count;

                               return countMatches.Label(
                                   $"JSON parsing - Count: {result.Count}, Expected: {apiResponse.Data!.Count}");
                           });
    }

#endregion

#region Error Handling Tests

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.5**
    ///
    /// *For any* API response with non-zero error code, the parser SHALL return empty list.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseApiResponse_ErrorCode_ShouldReturnEmpty()
    {
        var errorCodeGen = Gen.Choose(-100000, 100000).Where(x => x != 0);

        return Prop.ForAll(
            errorCodeGen.ToArbitrary(),
            errorCode =>
            {
                var apiResponse =
                    new BilibiliApiResponse { Code = errorCode, Message = "Error", Data = new List<PageInfo>() };

                var result = ParseApiResponse(apiResponse);

                return (result.Count == 0).Label($"Error code: {errorCode}, Result count: {result.Count}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.5**
    ///
    /// *For any* null response, the parser SHALL return empty list.
    /// </summary>
    [Fact]
    public void ParseApiResponse_Null_ShouldReturnEmpty()
    {
        var result = ParseApiResponse(null);
        Assert.Empty(result);
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.5**
    ///
    /// *For any* malformed JSON string, the parser SHALL return empty list.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseApiResponse_MalformedJson_ShouldReturnEmpty()
    {
        var malformedJsonGen = Gen.Elements("{invalid json}", "not json at all", "{\"code\": 0, \"data\": [", "null",
                                            "undefined", "{\"code\": \"not a number\"}", "");

        return Prop.ForAll(
            malformedJsonGen.ToArbitrary(),
            malformedJson =>
            {
                var result = ParseApiResponse(malformedJson);

                return (result.Count == 0).Label($"Malformed JSON: {malformedJson}, Result count: {result.Count}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.5**
    ///
    /// *For any* API response with null or empty data array, the parser SHALL return empty list.
    /// </summary>
    [Fact]
    public void ParseApiResponse_EmptyData_ShouldReturnEmpty()
    {
        var responseWithNull = new BilibiliApiResponse { Code = 0, Message = "0", Data = null };

        var responseWithEmpty = new BilibiliApiResponse { Code = 0, Message = "0", Data = new List<PageInfo>() };

        var resultNull = ParseApiResponse(responseWithNull);
        var resultEmpty = ParseApiResponse(responseWithEmpty);

        Assert.Empty(resultNull);
        Assert.Empty(resultEmpty);
    }

#endregion

#region Edge Cases

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.4**
    ///
    /// *For any* page entry with empty title, the parser SHALL generate default title "P{page}".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseApiResponse_EmptyTitle_ShouldGenerateDefault()
    {
        return Prop.ForAll(Arb.From<PositiveInt>(),
                           pageNum =>
                           {
                               var apiResponse = new BilibiliApiResponse {
                                   Code = 0, Message = "0",
                                   Data = new List<PageInfo> { new PageInfo { Cid = 12345, Page = pageNum.Get,
                                                                              Part = "", // 空标题
                                                                              Duration = 600 } }
                               };

                               var result = ParseApiResponse(apiResponse);

                               return (result.Count == 1 && result[0].Part == $"P{pageNum.Get}")
                                   .Label($"Page: {pageNum.Get}, Generated title: {result[0].Part}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 2: API Response Parsing Completeness**
    /// **Validates: Requirements 1.4**
    ///
    /// *For any* large page list (up to 100 pages), the parser SHALL handle it correctly.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property ParseApiResponse_LargePageList_ShouldHandleCorrectly()
    {
        var largePageCountGen = Gen.Choose(50, 100);

        return Prop.ForAll(
            largePageCountGen.ToArbitrary(),
            pageCount =>
            {
                var pages =
                    Enumerable.Range(1, pageCount)
                        .Select(i => new PageInfo { Cid = 1000000 + i, Page = i, Part = $"第{i}集", Duration = 600 })
                        .ToList();

                var apiResponse = new BilibiliApiResponse { Code = 0, Message = "0", Data = pages };

                var result = ParseApiResponse(apiResponse);

                return (result.Count == pageCount).Label($"Page count: {pageCount}, Parsed count: {result.Count}");
            });
    }

#endregion
}
