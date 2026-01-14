using AkashaNavigator.Plugins.Apis;
using FsCheck;
using FsCheck.Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// HttpApi 属性测试
/// 验证 IDisposable 实现的正确性
/// </summary>
public class HttpApiPropertyTests
{
    /// <summary>
    /// **Feature: code-cleanup-2026-01, Property 1: HttpApi IDisposable 正确实现**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* HttpApi instance, calling Dispose() should release resources
    /// and not throw exceptions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Dispose_ShouldNotThrow()
    {
        return Prop.ForAll(Arb.From<bool>(),
                           _ =>
                           {
                               var api = new HttpApi("test-plugin", new[] { "https://*" });
                               try
                               {
                                   api.Dispose();
                                   return true;
                               }
                               catch
                               {
                                   return false;
                               }
                           });
    }

    /// <summary>
    /// **Feature: code-cleanup-2026-01, Property 2: HttpApi Dispose 幂等性**
    /// **Validates: Requirements 3.2**
    ///
    /// *For any* HttpApi instance and any number of Dispose calls (1-10),
    /// calling Dispose() multiple times should not throw exceptions
    /// and should have the same effect as calling it once.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Dispose_IsIdempotent()
    {
        return Prop.ForAll(Arb.From<PositiveInt>(), count =>
                                                    {
                                                        var api = new HttpApi("test-plugin", new[] { "https://*" });
                                                        var callCount = (count.Get % 10) + 1; // 1-10 次调用

                                                        try
                                                        {
                                                            for (int i = 0; i < callCount; i++)
                                                            {
                                                                api.Dispose();
                                                            }
                                                            return true;
                                                        }
                                                        catch
                                                        {
                                                            return false;
                                                        }
                                                    });
    }

    /// <summary>
    /// **Feature: code-cleanup-2026-01, Property 1: HttpApi IDisposable 正确实现**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* HttpApi instance created with different plugin IDs,
    /// Dispose should work correctly regardless of the plugin ID.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Dispose_WorksWithAnyPluginId()
    {
        return Prop.ForAll(Arb.From<NonEmptyString>(), pluginId =>
                                                       {
                                                           var api = new HttpApi(pluginId.Get, new[] { "https://*" });
                                                           try
                                                           {
                                                               api.Dispose();
                                                               return true;
                                                           }
                                                           catch
                                                           {
                                                               return false;
                                                           }
                                                       });
    }
}
}
