using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests;

/// <summary>
/// B站分P列表渲染属性测试
/// 验证 bilibili-page-list 插件的渲染逻辑
/// </summary>
public class BilibiliPageListRenderPropertyTests
{
    /// <summary>
    /// 分P信息
    /// </summary>
    public class PageInfo
    {
        public int Cid { get; set; }
        public int Page { get; set; }
        public string Part { get; set; } = string.Empty;
        public int Duration { get; set; }
    }

    /// <summary>
    /// 渲染输出（简化模型）
    /// </summary>
    public class RenderOutput
    {
        public List<RenderedItem> Items { get; set; } = new();
    }

    /// <summary>
    /// 渲染的列表项
    /// </summary>
    public class RenderedItem
    {
        public int Page { get; set; }
        public bool IsActive { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>
    /// 模拟渲染分P列表（简化版本，只关注活动状态逻辑）
    /// </summary>
    public static RenderOutput RenderPageList(List<PageInfo> pageList, int activePage)
    {
        var output = new RenderOutput();

        foreach (var pageItem in pageList)
        {
            var isActive = pageItem.Page == activePage;
            output.Items.Add(new RenderedItem { Page = pageItem.Page, IsActive = isActive, Title = pageItem.Part });
        }

        return output;
    }

#region Property 3 : Page Item Rendering with Active State

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 3: Page Item Rendering with Active State**
    /// **Validates: Requirements 2.2, 2.3**
    ///
    /// *For any* page list and active page index, the renderer SHALL produce output where
    /// exactly one item is marked as active, and that item's page number matches the active page index.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RenderPageList_WithActivePage_ShouldHighlightExactlyOne()
    {
        // 生成分P列表（1-50个分P）
        var pageListGen = Gen.Choose(1, 50).SelectMany(
            count =>
            {
                var pages = new List<PageInfo>();
                for (int i = 1; i <= count; i++)
                {
                    pages.Add(new PageInfo { Cid = 1000000 + i, Page = i, Part = $"第{i}集", Duration = 600 + i * 10 });
                }
                return Gen.Constant(pages);
            });

        return Prop.ForAll(pageListGen.ToArbitrary(),
                           pageList =>
                           {
                               if (pageList.Count == 0)
                               {
                                   return true.ToProperty(); // 空列表跳过
                               }

                               // 随机选择一个活动页
                               var activePage = pageList[new System.Random().Next(pageList.Count)].Page;

                               // 渲染
                               var output = RenderPageList(pageList, activePage);

                               // 验证：恰好有一个项被标记为活动
                               var activeItems = output.Items.Where(item => item.IsActive).ToList();
                               var hasExactlyOneActive = activeItems.Count == 1;

                               // 验证：活动项的页码与指定的活动页匹配
                               var activePageMatches = activeItems.Count == 1 && activeItems[0].Page == activePage;

                               return (hasExactlyOneActive && activePageMatches)
                                   .Label($"PageList Count: {pageList.Count}, Active Page: {activePage}, " +
                                          $"Active Items: {activeItems.Count}, " +
                                          $"Active Page Matches: {activePageMatches}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 3: Page Item Rendering with Active State**
    /// **Validates: Requirements 2.2, 2.3**
    ///
    /// *For any* page list, when the active page is the first page,
    /// the first rendered item SHALL be marked as active.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RenderPageList_FirstPageActive_ShouldHighlightFirst()
    {
        var pageListGen = Gen.Choose(1, 50).SelectMany(
            count =>
            {
                var pages = new List<PageInfo>();
                for (int i = 1; i <= count; i++)
                {
                    pages.Add(new PageInfo { Cid = 1000000 + i, Page = i, Part = $"第{i}集", Duration = 600 + i * 10 });
                }
                return Gen.Constant(pages);
            });

        return Prop.ForAll(pageListGen.ToArbitrary(),
                           pageList =>
                           {
                               if (pageList.Count == 0)
                               {
                                   return true.ToProperty();
                               }

                               // 活动页设为第一页
                               var activePage = 1;

                               // 渲染
                               var output = RenderPageList(pageList, activePage);

                               // 验证：第一项被标记为活动
                               var firstItemActive = output.Items.Count > 0 && output.Items[0].IsActive;

                               // 验证：只有第一项是活动的
                               var onlyFirstActive = output.Items.Count(item => item.IsActive) == 1;

                               return (firstItemActive && onlyFirstActive)
                                   .Label($"PageList Count: {pageList.Count}, First Item Active: {firstItemActive}, " +
                                          $"Only First Active: {onlyFirstActive}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 3: Page Item Rendering with Active State**
    /// **Validates: Requirements 2.2, 2.3**
    ///
    /// *For any* page list, when the active page is the last page,
    /// the last rendered item SHALL be marked as active.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RenderPageList_LastPageActive_ShouldHighlightLast()
    {
        var pageListGen = Gen.Choose(1, 50).SelectMany(
            count =>
            {
                var pages = new List<PageInfo>();
                for (int i = 1; i <= count; i++)
                {
                    pages.Add(new PageInfo { Cid = 1000000 + i, Page = i, Part = $"第{i}集", Duration = 600 + i * 10 });
                }
                return Gen.Constant(pages);
            });

        return Prop.ForAll(pageListGen.ToArbitrary(),
                           pageList =>
                           {
                               if (pageList.Count == 0)
                               {
                                   return true.ToProperty();
                               }

                               // 活动页设为最后一页
                               var activePage = pageList.Count;

                               // 渲染
                               var output = RenderPageList(pageList, activePage);

                               // 验证：最后一项被标记为活动
                               var lastItemActive = output.Items.Count > 0 && output.Items[^1].IsActive;

                               // 验证：只有最后一项是活动的
                               var onlyLastActive = output.Items.Count(item => item.IsActive) == 1;

                               return (lastItemActive && onlyLastActive)
                                   .Label($"PageList Count: {pageList.Count}, Last Item Active: {lastItemActive}, " +
                                          $"Only Last Active: {onlyLastActive}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 3: Page Item Rendering with Active State**
    /// **Validates: Requirements 2.2, 2.3**
    ///
    /// *For any* page list, when the active page does not exist in the list,
    /// no items SHALL be marked as active.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RenderPageList_InvalidActivePage_ShouldHighlightNone()
    {
        var pageListGen = Gen.Choose(1, 50).SelectMany(
            count =>
            {
                var pages = new List<PageInfo>();
                for (int i = 1; i <= count; i++)
                {
                    pages.Add(new PageInfo { Cid = 1000000 + i, Page = i, Part = $"第{i}集", Duration = 600 + i * 10 });
                }
                return Gen.Constant(pages);
            });

        return Prop.ForAll(pageListGen.ToArbitrary(),
                           pageList =>
                           {
                               if (pageList.Count == 0)
                               {
                                   return true.ToProperty();
                               }

                               // 活动页设为不存在的页码（超出范围）
                               var activePage = pageList.Count + 100;

                               // 渲染
                               var output = RenderPageList(pageList, activePage);

                               // 验证：没有项被标记为活动
                               var noActiveItems = output.Items.All(item => !item.IsActive);

                               return noActiveItems.Label(
                                   $"PageList Count: {pageList.Count}, Active Page: {activePage}, " +
                                   $"No Active Items: {noActiveItems}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 3: Page Item Rendering with Active State**
    /// **Validates: Requirements 2.2, 2.3**
    ///
    /// *For any* page list, the total number of rendered items SHALL equal
    /// the number of pages in the input list.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RenderPageList_ShouldRenderAllPages()
    {
        var pageListGen = Gen.Choose(1, 50).SelectMany(
            count =>
            {
                var pages = new List<PageInfo>();
                for (int i = 1; i <= count; i++)
                {
                    pages.Add(new PageInfo { Cid = 1000000 + i, Page = i, Part = $"第{i}集", Duration = 600 + i * 10 });
                }
                return Gen.Constant(pages);
            });

        return Prop.ForAll(pageListGen.ToArbitrary(),
                           pageList =>
                           {
                               if (pageList.Count == 0)
                               {
                                   return true.ToProperty();
                               }

                               // 随机活动页
                               var activePage = pageList[new System.Random().Next(pageList.Count)].Page;

                               // 渲染
                               var output = RenderPageList(pageList, activePage);

                               // 验证：渲染的项数等于输入列表的项数
                               var countMatches = output.Items.Count == pageList.Count;

                               return countMatches.Label(
                                   $"Input Count: {pageList.Count}, Output Count: {output.Items.Count}");
                           });
    }

#endregion

#region Unit Tests for Edge Cases

    /// <summary>
    /// 空列表应该返回空输出
    /// </summary>
    [Fact]
    public void RenderPageList_EmptyList_ShouldReturnEmpty()
    {
        var pageList = new List<PageInfo>();
        var output = RenderPageList(pageList, 1);

        Assert.Empty(output.Items);
    }

    /// <summary>
    /// 单页列表应该正确标记活动状态
    /// </summary>
    [Fact]
    public void RenderPageList_SinglePage_ShouldMarkActive()
    {
        var pageList = new List<PageInfo> { new() { Cid = 1000001, Page = 1, Part = "第1集", Duration = 600 } };

        var output = RenderPageList(pageList, 1);

        Assert.Single(output.Items);
        Assert.True(output.Items[0].IsActive);
        Assert.Equal(1, output.Items[0].Page);
    }

    /// <summary>
    /// 多页列表应该只标记一个活动项
    /// </summary>
    [Fact]
    public void RenderPageList_MultiplePages_ShouldMarkOnlyOneActive()
    {
        var pageList = new List<PageInfo> { new() { Cid = 1000001, Page = 1, Part = "第1集", Duration = 600 },
                                            new() { Cid = 1000002, Page = 2, Part = "第2集", Duration = 610 },
                                            new() { Cid = 1000003, Page = 3, Part = "第3集", Duration = 620 } };

        var output = RenderPageList(pageList, 2);

        Assert.Equal(3, output.Items.Count);
        Assert.Single(output.Items.Where(item => item.IsActive));
        Assert.Equal(2, output.Items.First(item => item.IsActive).Page);
    }

#endregion
}
