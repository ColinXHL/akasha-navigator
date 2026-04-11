# Bilibili Playback Rate Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 仅针对 B 站视频页面，让分P/推荐/站内视频切换后的倍速稳定继承用户最后一次明确设置的权威倍速，同时允许用户通过网页倍速菜单或软件热键/UI 将权威倍速改回 `1.0x`。

**Architecture:** 采用“权威倍速 + 显式用户意图 + 导航恢复窗口”的状态机方案，替代依赖零散事件名推断用户意图的做法。实现直接在 `main` 分支进行，不创建 worktree；执行方式采用 TDD（每个行为先写失败测试）+ SDD（按任务切分实现并在任务间复核）。

**Tech Stack:** .NET 8, WPF, WebView2 injected JavaScript, xUnit

---

## File Structure

### 现有实现边界
- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
  - 引入 `authoritativeRate` 驱动的状态流。
  - 增加显式用户倍速意图和导航窗口状态。
  - 识别网页倍速菜单操作并只在显式意图成立时更新权威值。
  - 在视频切换窗口内只做恢复，不把站点临时重置的 `1.0` 写回权威值。
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`
  - 保持宿主倍速设置路径继续通过 `akasha:set-playback-rate` 向页面声明显式意图。
  - 仅做必要同步，不新增第二套权威状态源。
- Modify: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`
  - 补充脚本级回归测试，约束状态机关键行为和选择器识别。

### 不新增新文件
- 本次不拆新 JS 模块，不新增 C# 服务。
- 所有状态继续收敛在 `InjectedScripts.js` 的 Playback State Manager 内，避免引入第二处状态中心。

## Task 1: 建立“只有显式用户意图才能写权威倍速”的测试护栏

**Files:**
- Modify: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 先写失败测试，约束脚本存在显式意图门控函数**

```csharp
[Fact]
public void InjectedScript_ShouldContainExplicitRateIntentGate()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("function markExplicitRateIntent", script, StringComparison.Ordinal);
    Assert.Contains("function hasRecentExplicitRateIntent", script, StringComparison.Ordinal);
    Assert.Contains("function shouldPersistRateAsAuthority", script, StringComparison.Ordinal);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ShouldContainExplicitRateIntentGate"`
Expected: FAIL，提示缺少新状态机函数。

- [ ] **Step 3: 再写失败测试，约束 `video-ratechange` 不得直接依赖普通用户交互写权威值**

```csharp
[Fact]
public void InjectedScript_VideoRateChange_ShouldNotPersistAuthorityFromGenericUserInteraction()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.DoesNotContain("markExplicitRateRequest(rate, 'video-ratechange')", script, StringComparison.Ordinal);

    Assert.Contains("shouldPersistRateAsAuthority(rate, 'video-ratechange')", script, StringComparison.Ordinal);
}
```

- [ ] **Step 4: 运行新增测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_VideoRateChange_ShouldNotPersistAuthorityFromGenericUserInteraction"`
Expected: FAIL，当前实现仍直接从 `hasRecentUserInteraction()` 推断。

## Task 2: 建立“网页倍速菜单属于权威输入”的测试护栏

**Files:**
- Modify: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 写失败测试，约束脚本存在 B 站倍速菜单选择器和识别函数**

```csharp
[Fact]
public void InjectedScript_ShouldRecognizeBilibiliPlaybackRateMenuInteraction()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("const PLAYBACK_RATE_MENU_SELECTORS = [", script, StringComparison.Ordinal);
    Assert.Contains("function isPlaybackRateMenuElement", script, StringComparison.Ordinal);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ShouldRecognizeBilibiliPlaybackRateMenuInteraction"`
Expected: FAIL，当前脚本尚未显式识别网页倍速菜单。

- [ ] **Step 3: 写失败测试，约束点击网页倍速菜单时会登记显式倍速意图**

```csharp
[Fact]
public void InjectedScript_PlaybackRateMenuClick_ShouldMarkExplicitRateIntent()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("markExplicitRateIntent(rate, 'bilibili-rate-menu')", script, StringComparison.Ordinal);
}
```

- [ ] **Step 4: 运行测试确认失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_PlaybackRateMenuClick_ShouldMarkExplicitRateIntent"`
Expected: FAIL，当前脚本尚未在网页菜单点击路径下标记显式意图。

## Task 3: 重构脚本状态机，显式区分“权威写入”和“导航恢复”

**Files:**
- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 在脚本中将“显式倍速请求”重命名并扩展为“显式倍速意图”状态**

```javascript
let lastExplicitRateIntent = null;

function markExplicitRateIntent(rate, source) {
    const normalizedRate = Number(rate) || 1.0;
    if (normalizedRate <= 0) {
        return;
    }

    lastExplicitRateIntent = {
        playbackRate: normalizedRate,
        source: String(source || 'unknown'),
        updatedAt: Date.now()
    };
}

function hasRecentExplicitRateIntent(rate) {
    if (!lastExplicitRateIntent) {
        return false;
    }

    if ((Date.now() - Number(lastExplicitRateIntent.updatedAt || 0)) > 2500) {
        return false;
    }

    return approxEqual(lastExplicitRateIntent.playbackRate, rate);
}
```

- [ ] **Step 2: 增加导航窗口和权威写入判定函数，禁止 generic interaction 直接写权威值**

```javascript
let navigationWindowUntil = 0;

function beginNavigationWindow() {
    navigationWindowUntil = Date.now() + 3000;
}

function isWithinNavigationWindow() {
    return Date.now() <= navigationWindowUntil;
}

function shouldPersistRateAsAuthority(rate, reason) {
    const normalizedRate = Number(rate) || 1.0;
    if (normalizedRate <= 0) {
        return false;
    }

    if (hasRecentExplicitRateIntent(normalizedRate)) {
        return true;
    }

    if (isWithinNavigationWindow()) {
        return false;
    }

    return false;
}
```

- [ ] **Step 3: 将 `saveGlobalRate` 的调用点改成“只有通过判定函数才持久化”**

```javascript
video.addEventListener('ratechange', function () {
    const rate = Number(video.playbackRate) || 1.0;
    if (shouldPersistRateAsAuthority(rate, 'video-ratechange')) {
        saveGlobalRate(rate, 'video-ratechange');
    }
    saveSnapshot('video-ratechange');
});
```

并同步替换其它判断中的：

```javascript
matchesRecentExplicitRateRequest(...)
```

为：

```javascript
hasRecentExplicitRateIntent(...)
```

- [ ] **Step 4: 运行 Task 1 的两个测试，确认转绿**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests"`
Expected: PASS

## Task 4: 识别网页倍速菜单，并把它纳入显式权威输入

**Files:**
- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 为 B 站倍速菜单添加选择器和目标识别函数**

```javascript
const PLAYBACK_RATE_MENU_SELECTORS = [
    '.bpx-player-ctrl-playbackrate-menu-item',
    '.bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item',
    '[data-value][class*="playbackrate"]',
    '[class*="speed-menu"] [data-value]'
];

function isPlaybackRateMenuElement(element) {
    if (!(element instanceof Element)) {
        return false;
    }

    return !!element.closest(PLAYBACK_RATE_MENU_SELECTORS.join(', '));
}
```

- [ ] **Step 2: 增加从菜单元素解析倍速数值的函数**

```javascript
function extractPlaybackRateFromElement(element) {
    const matched = element instanceof Element ? element.closest(PLAYBACK_RATE_MENU_SELECTORS.join(', ')) : null;
    if (!matched) {
        return null;
    }

    const candidates = [
        matched.getAttribute('data-value'),
        matched.getAttribute('data-rate'),
        matched.textContent
    ];

    for (let i = 0; i < candidates.length; i++) {
        const text = String(candidates[i] || '');
        const match = text.match(/(0\.5|0\.75|1(?:\.0)?|1\.25|1\.5|1\.75|2(?:\.0)?|3(?:\.0)?)/);
        if (match) {
            return Number(match[1]);
        }
    }

    return null;
}
```

- [ ] **Step 3: 在全局 click 捕获中，识别网页倍速菜单点击并登记显式意图**

```javascript
if (isPlaybackRateMenuElement(target)) {
    const rate = extractPlaybackRateFromElement(target);
    if (rate && rate > 0) {
        markExplicitRateIntent(rate, 'bilibili-rate-menu');
        postPlaybackDebug('explicitRateIntent', {
            source: 'bilibili-rate-menu',
            rate: rate
        });
    }
    return;
}
```

- [ ] **Step 4: 运行 Task 2 的两个测试，确认转绿**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests"`
Expected: PASS

## Task 5: 把分P/推荐/站内视频切换统一纳入导航恢复窗口

**Files:**
- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
- Test: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 写失败测试，约束脚本存在导航窗口入口**

```csharp
[Fact]
public void InjectedScript_ShouldOpenNavigationWindowForPartAndHistoryNavigation()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("beginNavigationWindow();", script, StringComparison.Ordinal);
    Assert.Contains("savePendingSnapshot('pushState'", script, StringComparison.Ordinal);
}
```

- [ ] **Step 2: 运行测试确认失败或部分失败**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ShouldOpenNavigationWindowForPartAndHistoryNavigation"`
Expected: FAIL，当前尚未在所有导航入口显式打开统一导航窗口。

- [ ] **Step 3: 在分P点击、`pushState`、`replaceState`、`beforeunload`、`pagehide`、新 video 生命周期入口统一开启导航窗口**

```javascript
function noteVideoNavigation(reason) {
    beginNavigationWindow();
    postPlaybackDebug('navigationWindowOpened', { reason: reason });
}

// examples
if (isPartNavigationElement(target)) {
    markPartNavigationClick();
    noteVideoNavigation('part-navigation-click');
    saveSnapshot('part-navigation-click');
    savePendingSnapshot('part-navigation-click');
}

history.pushState = function () {
    const previousSnapshot = readPendingSnapshot() || readSnapshot();
    const result = originalPushState.apply(this, arguments);
    noteVideoNavigation('pushState');
    saveSnapshot('pushState', null, previousSnapshot);
    savePendingSnapshot('pushState', null, previousSnapshot);
    return result;
};
```

- [ ] **Step 4: 在恢复流程中仅消费 `authoritativeRate`/snapshot，不把导航窗口内的 `1.0` 反写成权威值**

```javascript
function isTransientReason(reason) {
    const transientReasons = {
        'pushState': true,
        'replaceState': true,
        'beforeunload': true,
        'pagehide': true,
        'video-ratechange': true,
        'video-ended': true,
        'video-loadedmetadata': true,
        'video-playing': true,
        'part-navigation-click': true,
        'mode-poll': true,
        'rate-poll': true
    };

    return !!transientReasons[String(reason || '')];
}

function shouldSkipGlobalRateOverwrite(targetRate, reason) {
    const previousRate = readGlobalRate();
    const isDowngradeToDefault = approxEqual(targetRate, 1.0) && previousRate > 1.0;
    if (!isDowngradeToDefault) {
        return false;
    }

    if (hasRecentExplicitRateIntent(targetRate)) {
        return false;
    }

    if (isWithinNavigationWindow()) {
        return true;
    }

    return isTransientReason(reason);
}
```

- [ ] **Step 5: 运行 Task 5 测试确认通过**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_ShouldOpenNavigationWindowForPartAndHistoryNavigation"`
Expected: PASS

## Task 6: 保持宿主热键/UI 作为权威输入路径，并补全全量验证

**Files:**
- Modify: `AkashaNavigator/Scripts/InjectedScripts.js`
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`
- Modify: `AkashaNavigator.Tests/PlaybackStateScriptTests.cs`

- [ ] **Step 1: 写失败测试，约束宿主设置倍速事件走显式意图路径**

```csharp
[Fact]
public void InjectedScript_HostSetPlaybackRate_ShouldMarkExplicitRateIntent()
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
    var script = File.ReadAllText(path);

    Assert.Contains("markExplicitRateIntent(rate, 'host-set-playback-rate')", script, StringComparison.Ordinal);
}
```

- [ ] **Step 2: 运行测试确认失败或保持红灯**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests.InjectedScript_HostSetPlaybackRate_ShouldMarkExplicitRateIntent"`
Expected: 如果尚未完成重命名则 FAIL。

- [ ] **Step 3: 更新宿主事件监听实现，确保 `akasha:set-playback-rate` 只声明显式意图并写入权威值**

```javascript
window.addEventListener('akasha:set-playback-rate', function (event) {
    const detail = event && event.detail ? event.detail : null;
    const rate = detail && typeof detail.rate !== 'undefined' ? Number(detail.rate) : 1.0;
    markExplicitRateIntent(rate, 'host-set-playback-rate');
    saveGlobalRate(rate, 'host-set-playback-rate');
    postPlaybackDebug('explicitRateIntent', {
        source: 'host-set-playback-rate',
        rate: rate
    });
});
```

`PlayerWindow.xaml.cs` 仅保留当前：

```csharp
window.dispatchEvent(new CustomEvent('akasha:set-playback-rate', {
    detail: { rate: 1.25 }
}));
```

不新增第二个 C# 端全局倍速存储源。

- [ ] **Step 4: 运行整个脚本测试集**

Run: `dotnet test --filter "FullyQualifiedName~PlaybackStateScriptTests"`
Expected: PASS

- [ ] **Step 5: 运行更大范围回归，确认主工程测试未被破坏**

Run: `dotnet test`
Expected: PASS（允许存在已有 warning，但不能有新的 fail）

- [ ] **Step 6: 手工验证 B 站真实场景**

Run: `dotnet run --project AkashaNavigator`

手工验证清单：
- 软件热键调到 `1.75x`，点网页“下一P”，新视频应恢复 `1.75x`
- 网页倍速菜单调到 `1.5x`，点同站推荐视频，应恢复 `1.5x`
- 网页倍速菜单改回 `1.0x`，再切分P，必须保持 `1.0x`
- 切视频时日志若出现 `video-ratechange -> 1.0`，不能把全局权威值从非 `1.0` 覆盖掉

- [ ] **Step 7: 提交修改**

```bash
git add AkashaNavigator/Scripts/InjectedScripts.js AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs AkashaNavigator.Tests/PlaybackStateScriptTests.cs docs/superpowers/plans/2026-04-10-bilibili-playback-rate-authority-plan.md
git commit -m "fix(player): preserve authoritative bilibili playback rate across navigation"
```
