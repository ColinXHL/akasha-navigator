# Bilibili Playback Rate Host Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 B 站网页播放器手动改倍速后，宿主热键继续以网页真实倍速为基准加减，并把网页端显式倍速变化同步回宿主缓存。

**Architecture:** 使用“双保险”同步方案：宿主在 `+/-` 操作前先读取网页真实 `video.playbackRate`，同时 B 站网页显式倍速变化通过现有 WebView2 `postMessage` 通道回传宿主更新 `_currentPlaybackRate`。实现直接在 `main` 分支进行，不创建 worktree；执行方式采用 TDD（每个行为先写失败测试）+ SDD（按任务切分实现并在任务间复核）。

**Tech Stack:** .NET 8, WPF, WebView2, injected JavaScript, xUnit

---

## File Structure

- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
  - 增加 B 站网页显式倍速 -> 宿主同步消息发送函数
  - 仅在显式倍速变化成功后发送 `playback_rate_sync`
  - 做宿主同步去重，避免重复刷消息
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`
  - 增加读取网页实时倍速的 helper
  - 在 `IncreasePlaybackRateAsync()` / `DecreasePlaybackRateAsync()` 中优先使用网页真实倍速
  - 在 `CoreWebView2_WebMessageReceived(...)` 中处理 `playback_rate_sync`
- Modify: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`
  - 为 JS 宿主同步消息补行为测试
- Create: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`
  - 为宿主实时读值和消息处理补测试

## Task 1: 为 B 站网页倍速同步消息建立 JS 测试护栏

**Files:**
- Modify: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 写失败测试，约束脚本存在宿主同步消息函数**

```csharp
[Fact]
public void InjectedScript_ShouldContainPlaybackRateSyncMessageSupport()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("type: 'playback_rate_sync'", script, StringComparison.Ordinal);
    Assert.Contains("function postPlaybackRateSync", script, StringComparison.Ordinal);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ShouldContainPlaybackRateSyncMessageSupport"`
Expected: FAIL，当前脚本尚未声明该消息路径。

- [ ] **Step 3: 写失败测试，约束 B 站网页显式倍速变化会触发宿主同步**

```csharp
[Fact]
public void InjectedScript_ExplicitBilibiliRateChange_ShouldPostPlaybackRateSync()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("postPlaybackRateSync(rate, 'bilibili-rate-menu')", script, StringComparison.Ordinal);
}
```

- [ ] **Step 4: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ExplicitBilibiliRateChange_ShouldPostPlaybackRateSync"`
Expected: FAIL，当前脚本尚未把网页显式倍速变化同步回宿主。

## Task 2: 实现 B 站网页 -> 宿主倍速同步消息

**Files:**
- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 实现宿主同步消息函数和去重状态**

```javascript
let lastPostedPlaybackRateSync = null;

function postPlaybackRateSync(rate, source) {
    const normalizedRate = Number(rate) || 0;
    if (normalizedRate <= 0 || !isBilibiliVideoPage()) {
        return;
    }

    if (lastPostedPlaybackRateSync && approxEqual(lastPostedPlaybackRateSync.rate, normalizedRate)) {
        return;
    }

    lastPostedPlaybackRateSync = {
        rate: normalizedRate,
        source: String(source || 'unknown')
    };

    if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
        window.chrome.webview.postMessage(JSON.stringify({
            type: 'playback_rate_sync',
            rate: normalizedRate,
            source: String(source || 'unknown'),
            url: window.location.href
        }));
    }
}
```

- [ ] **Step 2: 在显式网页倍速菜单成功落地后发送宿主同步**

```javascript
video.addEventListener('ratechange', function () {
    const rate = Number(video.playbackRate) || 1.0;
    if (shouldPersistRateAsAuthority(rate, 'video-ratechange')) {
        saveGlobalRate(rate, 'video-ratechange');

        if (hasRecentExplicitRateIntent(rate) && lastExplicitRateIntent && lastExplicitRateIntent.source === 'bilibili-rate-menu') {
            postPlaybackRateSync(rate, 'bilibili-rate-menu');
        }
    }

    saveSnapshot('video-ratechange');
});
```

- [ ] **Step 3: 将 Task 1 两个测试升级为 ClearScript/V8 行为测试**

```csharp
[Fact]
public void InjectedScript_ShouldContainPlaybackRateSyncMessageSupport()
{
    using var harness = PlaybackScriptHarness.Create();

    Assert.True(harness.HasFunction("postPlaybackRateSync"));
    Assert.True(harness.ScriptContains("type: 'playback_rate_sync'"));
}

[Fact]
public void InjectedScript_ExplicitBilibiliRateChange_ShouldPostPlaybackRateSync()
{
    using var harness = PlaybackScriptHarness.Create();

    harness.InitializeTracking();
    harness.TriggerBilibiliRateMenuSelection(1.5);
    harness.TriggerVideoRateChange(1.5);

    var message = harness.GetLastPostedMessage();
    Assert.NotNull(message);
    Assert.Contains("\"type\":\"playback_rate_sync\"", message, StringComparison.Ordinal);
    Assert.Contains("\"rate\":1.5", message, StringComparison.Ordinal);
}
```

- [ ] **Step 4: 运行 Task 1 的两个测试确认通过**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ShouldContainPlaybackRateSyncMessageSupport|FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ExplicitBilibiliRateChange_ShouldPostPlaybackRateSync"`
Expected: PASS

## Task 3: 为宿主读取网页真实倍速建立 C# 测试护栏

**Files:**
- Create: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`
- Test: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`

- [ ] **Step 1: 写失败测试，约束宿主加速前优先使用网页真实倍速**

```csharp
[Fact]
public async Task IncreasePlaybackRateAsync_ShouldUseLiveVideoRateBeforeCachedRate()
{
    var window = CreatePlayerWindowWithScriptQueue(returningJson: "1.5");

    await window.IncreasePlaybackRateAsync();

    Assert.Equal(1.75, window.CurrentPlaybackRate, 3);
    Assert.Contains("SetPlaybackRate(1.75)", window.ExecutedScripts);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.IncreasePlaybackRateAsync_ShouldUseLiveVideoRateBeforeCachedRate"`
Expected: FAIL，当前实现仍从缓存 `1.0` 起算。

- [ ] **Step 3: 写失败测试，约束读取失败时回退缓存值**

```csharp
[Fact]
public async Task IncreasePlaybackRateAsync_ShouldFallBackToCachedRateWhenLiveReadFails()
{
    var window = CreatePlayerWindowWithScriptQueue(returningJson: "null", currentPlaybackRate: 1.25);

    await window.IncreasePlaybackRateAsync();

    Assert.Equal(1.5, window.CurrentPlaybackRate, 3);
    Assert.Contains("SetPlaybackRate(1.5)", window.ExecutedScripts);
}
```

- [ ] **Step 4: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.IncreasePlaybackRateAsync_ShouldFallBackToCachedRateWhenLiveReadFails"`
Expected: FAIL，当前实现尚未区分实时值与缓存值。

## Task 4: 实现宿主以网页真实倍速优先的热键计算

**Files:**
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`
- Test: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`

- [ ] **Step 1: 为 `PlayerWindow` 增加读取网页实时倍速 helper**

```csharp
private async Task<double?> TryGetLivePlaybackRateAsync()
{
    if (WebView.CoreWebView2 == null)
        return null;

    const string script = @"(function() {
        var video = document.querySelector('video');
        return video ? video.playbackRate : null;
    })();";

    try
    {
        var raw = await WebView.CoreWebView2.ExecuteScriptAsync(script);
        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
            return null;

        if (double.TryParse(raw.Trim('"'), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var rate))
        {
            return rate is >= MinPlaybackRate and <= MaxPlaybackRate ? rate : null;
        }
    }
    catch (Exception ex)
    {
        _logService.Debug(nameof(PlayerWindow), "TryGetLivePlaybackRateAsync failed: {Message}", ex.Message);
    }

    return null;
}
```

- [ ] **Step 2: 让 `IncreasePlaybackRateAsync()` / `DecreasePlaybackRateAsync()` 优先使用网页实时倍速**

```csharp
public async Task IncreasePlaybackRateAsync()
{
    var baseRate = await TryGetLivePlaybackRateAsync() ?? _currentPlaybackRate;
    await SetPlaybackRateAsync(baseRate + PlaybackRateStep);
}

public async Task DecreasePlaybackRateAsync()
{
    var baseRate = await TryGetLivePlaybackRateAsync() ?? _currentPlaybackRate;
    await SetPlaybackRateAsync(baseRate - PlaybackRateStep);
}
```

- [ ] **Step 3: 运行 Task 3 的两个测试确认通过**

Run: `dotnet test --filter "FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.IncreasePlaybackRateAsync_ShouldUseLiveVideoRateBeforeCachedRate|FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.IncreasePlaybackRateAsync_ShouldFallBackToCachedRateWhenLiveReadFails"`
Expected: PASS

## Task 5: 为宿主接收 `playback_rate_sync` 建立测试护栏

**Files:**
- Modify: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`
- Test: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`

- [ ] **Step 1: 写失败测试，约束宿主收到 B 站同步消息后更新缓存**

```csharp
[Fact]
public void WebMessageReceived_PlaybackRateSyncFromBilibili_ShouldUpdateCurrentPlaybackRate()
{
    var window = CreatePlayerWindowForWebMessageTests("https://www.bilibili.com/video/BV1test");

    window.SimulateWebMessage("{\"type\":\"playback_rate_sync\",\"rate\":1.5,\"source\":\"bilibili-rate-menu\",\"url\":\"https://www.bilibili.com/video/BV1test\"}");

    Assert.Equal(1.5, window.CurrentPlaybackRate, 3);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.WebMessageReceived_PlaybackRateSyncFromBilibili_ShouldUpdateCurrentPlaybackRate"`
Expected: FAIL，当前宿主尚未处理该消息类型。

- [ ] **Step 3: 写失败测试，约束非 B 站同步消息被忽略**

```csharp
[Fact]
public void WebMessageReceived_PlaybackRateSyncFromNonBilibili_ShouldBeIgnored()
{
    var window = CreatePlayerWindowForWebMessageTests("https://www.youtube.com/watch?v=test");

    window.SimulateWebMessage("{\"type\":\"playback_rate_sync\",\"rate\":1.5,\"source\":\"bilibili-rate-menu\",\"url\":\"https://www.youtube.com/watch?v=test\"}");

    Assert.Equal(1.0, window.CurrentPlaybackRate, 3);
}
```

- [ ] **Step 4: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.WebMessageReceived_PlaybackRateSyncFromNonBilibili_ShouldBeIgnored"`
Expected: FAIL，当前宿主尚未实现过滤逻辑。

## Task 6: 实现宿主 `playback_rate_sync` 处理与完整验证

**Files:**
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`
- Test: `AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 在 `CoreWebView2_WebMessageReceived(...)` 中处理 `playback_rate_sync`**

```csharp
if (type == "playback_rate_sync")
{
    var rate = doc.RootElement.TryGetProperty("rate", out var rateEl) ? rateEl.GetDouble() : 0;
    var syncUrl = doc.RootElement.TryGetProperty("url", out var syncUrlEl)
        ? syncUrlEl.GetString() ?? string.Empty
        : string.Empty;

    if (rate is >= MinPlaybackRate and <= MaxPlaybackRate && IsBilibiliVideoUrl(syncUrl))
    {
        _currentPlaybackRate = rate;
        _logService.Debug(nameof(PlayerWindow), "Playback rate synced from web page: {Rate}", rate);
    }

    return;
}
```

- [ ] **Step 2: 为 `PlayerWindow` 增加最小 URL 判断 helper**

```csharp
private static bool IsBilibiliVideoUrl(string? url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        return false;

    return uri.Host.Contains("bilibili.com", StringComparison.OrdinalIgnoreCase) &&
           uri.AbsolutePath.StartsWith("/video/", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 3: 运行 Task 5 的两个测试确认通过**

Run: `dotnet test --filter "FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.WebMessageReceived_PlaybackRateSyncFromBilibili_ShouldUpdateCurrentPlaybackRate|FullyQualifiedName~PlayerWindowPlaybackRateSyncTests.WebMessageReceived_PlaybackRateSyncFromNonBilibili_ShouldBeIgnored"`
Expected: PASS

- [ ] **Step 4: 运行聚合测试集确认同步链路无回归**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests|FullyQualifiedName~PlayerWindowPlaybackRateSyncTests"`
Expected: PASS

- [ ] **Step 5: 运行完整测试集**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 6: 手工验证真实场景**

Run: `dotnet run --project AkashaNavigator`

手工验证清单：
- 网页菜单改成 `1.5x`，按 `+` 后结果应为 `1.75x`
- 网页菜单改回 `1.0x`，按 `+` 后结果应为 `1.25x`
- 网页菜单改成 `1.5x` 后，OSD 显示的下一次加速应基于 `1.5x`
- 分P切换后的自动恢复仍以网页 authority 为准，不出现宿主缓存回写覆盖

- [ ] **Step 7: 提交修改**

```bash
git add AkashaNavigator/Scripts/InjectedScripts.js AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs AkashaNavigator.Tests/PlaybackStateScriptTests.cs AkashaNavigator.Tests/Windows/PlayerWindowPlaybackRateSyncTests.cs docs/superpowers/specs/2026-04-11-bilibili-playback-rate-host-sync-design.md docs/superpowers/plans/2026-04-11-bilibili-playback-rate-host-sync-plan.md
git commit -m "fix(player): sync bilibili playback rate between web and host"
```
