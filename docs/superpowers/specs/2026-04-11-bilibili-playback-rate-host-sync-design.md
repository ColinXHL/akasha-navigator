# Bilibili Playback Rate Host Sync Design

## Goal

解决 B 站网页播放器倍速状态与宿主 `PlayerWindow._currentPlaybackRate` 缓存分叉的问题，使：

- 网页端手动改成 `1.5x` 后，宿主按 `+` 从 `1.5x` 继续到 `1.75x`
- 网页端手动改回 `1.0x` 后，宿主按 `+` 从 `1.0x` 继续到 `1.25x`
- 仅同步 B 站网页播放器里的倍速变化，不扩大到所有网站

## Problem Statement

当前系统已经能把 B 站网页倍速菜单的修改写入网页侧“权威倍速”，并用于分P/推荐/站内视频切换后的恢复。但宿主热键 `IncreasePlaybackRateAsync()` / `DecreasePlaybackRateAsync()` 仍然只依赖 `PlayerWindow._currentPlaybackRate` 作为计算基准。

`_currentPlaybackRate` 目前只在宿主执行 `SetPlaybackRateAsync()` 时更新。网页内手动改倍速不会回写这个缓存，导致：

1. 网页真实倍速已经变成 `1.5x`
2. 宿主缓存仍是 `1.0x`
3. 用户按 `+`
4. 宿主从 `1.0x` 算出 `1.25x`

这会造成网页状态、全局权威状态、宿主 OSD 展示状态三者不一致。

## Design Summary

采用双保险同步方案：

1. 宿主热键执行前，优先读取网页当前真实 `video.playbackRate`
2. B 站网页播放器内的显式倍速变化，主动通过 WebView2 `postMessage` 回传宿主，更新 `_currentPlaybackRate`

该方案不引入第二套权威状态存储。网页侧现有的“权威倍速”仍是切视频恢复的唯一事实来源；宿主侧 `_currentPlaybackRate` 只作为热键和 OSD 的本地缓存，并通过双向同步尽量保持与网页当前状态一致。

## Scope

### In Scope

- 仅 B 站视频页的网页倍速变化同步到宿主
- 宿主 `+/-/重置倍速` 以网页真实值优先作为计算基准
- 复用现有 `window.chrome.webview.postMessage(...)` -> `CoreWebView2_WebMessageReceived(...)` 通道
- 增加针对 JS 与 C# 双向同步行为的测试

### Out of Scope

- 非 B 站网站的倍速同步
- 新增跨页面或跨进程持久化的宿主倍速状态源
- 重构现有 playback-state authority / navigation-window 设计
- 改动用户可见的热键配置或步进规则

## Architecture

### 1. Host Reads Live Playback Rate Before Step Operations

在 `PlayerWindow` 中新增一个读取当前页面真实倍速的异步 helper，例如 `TryGetLivePlaybackRateAsync()`。

行为：

- 通过 `ExecuteScriptAsync` 查询 `document.querySelector('video')?.playbackRate`
- 解析成功且在有效范围内时，返回真实倍速
- 读取失败或页面无视频时，回退到 `_currentPlaybackRate`

随后：

- `IncreasePlaybackRateAsync()` 基于“网页真实值优先”计算目标值
- `DecreasePlaybackRateAsync()` 基于“网页真实值优先”计算目标值
- `ResetPlaybackRateAsync()` 仍直接设为 `1.0`

这样即便网页回传同步偶尔漏掉，热键路径仍会以页面真实状态为准。

### 2. Bilibili Page Sends Playback Rate Sync Messages To Host

在 `InjectedScripts.js` 的 B 站 playback-state manager 内新增一个仅用于宿主缓存同步的消息发送函数，例如 `postPlaybackRateSync(rate, source)`。

消息格式：

```json
{
  "type": "playback_rate_sync",
  "rate": 1.5,
  "source": "bilibili-rate-menu",
  "url": "https://www.bilibili.com/video/..."
}
```

发送条件：

- 当前页面是 B 站视频页
- 倍速值有效
- 倍速与上次发送值不同，避免刷屏

触发时机：

- 网页倍速菜单驱动的显式倍速变化最终写入视频后
- 宿主 `akasha:set-playback-rate` 成功作用到视频后也可以同步一次宿主缓存，保持单一代码路径

不在普通 transient `ratechange`、切分P自动恢复、站点自动重置等路径上无差别发送。

### 3. Host Receives Sync Messages And Updates Local Cache Only

在 `PlayerWindow.CoreWebView2_WebMessageReceived(...)` 中新增 `playback_rate_sync` 分支：

- 校验 `type == "playback_rate_sync"`
- 校验 `rate` 有效并在允许范围内
- 校验当前 URL / 消息 URL 属于 B 站视频页
- 仅更新 `_currentPlaybackRate`
- 写 debug log，便于区分来源：`bilibili-rate-menu` / `host-set-playback-rate`

这里不反向写入新的全局状态，不改导航恢复状态机，不新增配置文件字段。

### 4. Source Of Truth Boundaries

- 网页侧 authority/globalRate：切视频恢复的事实来源
- 网页真实 `video.playbackRate`：宿主热键计算时的实时基准
- 宿主 `_currentPlaybackRate`：本地展示和快捷键兜底缓存

优先级：

1. 热键执行时优先读取网页真实值
2. 读不到时回退宿主缓存
3. 宿主缓存通过 B 站回传消息持续被动同步

## Data Flow

### Flow A: 网页菜单改倍速 -> 宿主同步

1. 用户在 B 站播放器菜单中点击 `1.5x`
2. `InjectedScripts.js` 识别为 `bilibili-rate-menu`
3. 现有 authority 逻辑更新网页侧权威倍速
4. 脚本向宿主发送 `playback_rate_sync(rate=1.5)`
5. `PlayerWindow` 收到消息并更新 `_currentPlaybackRate = 1.5`

### Flow B: 宿主按 `+`

1. `IncreasePlaybackRateAsync()` 先读取当前网页真实 `video.playbackRate`
2. 若读取到 `1.5`，则计算目标值为 `1.75`
3. 调用现有 `SetPlaybackRateAsync(1.75)`
4. 现有 authority 路径更新网页侧状态
5. 宿主缓存也被更新到 `1.75`

### Flow C: 网页改回 `1.0x` 后宿主按 `+`

1. 网页菜单改回 `1.0x`
2. authority 更新为 `1.0`
3. 宿主收到 `playback_rate_sync(1.0)`
4. 用户按 `+`
5. 宿主先读网页真实值 `1.0`，计算成 `1.25`

## Error Handling

### JS -> Host Sync Failures

- `postMessage` 失败时忽略，不阻断网页本身的倍速变更
- 失败只影响宿主缓存同步，不影响网页 authority

### Host Live-Rate Read Failures

- 若 `ExecuteScriptAsync` 失败、返回值无法解析、无视频元素，则回退 `_currentPlaybackRate`
- 记录 debug 日志，不向用户弹错误

### Invalid Sync Payloads

- 若消息缺少 `rate`、数值越界、URL 非 B 站视频页，则直接丢弃

## Testing Strategy

### JavaScript Tests

- 验证 B 站菜单显式倍速变更会发送 `playback_rate_sync`
- 验证自动恢复/临时 `ratechange` 不会误发宿主同步
- 验证 `1.0x` 显式改动也会发送宿主同步

### C# Tests

- 验证 `CoreWebView2_WebMessageReceived` 处理 `playback_rate_sync` 时仅更新 `_currentPlaybackRate`
- 验证 `IncreasePlaybackRateAsync()` / `DecreasePlaybackRateAsync()` 以网页真实值优先计算
- 验证读取失败时回退 `_currentPlaybackRate`

### Manual Verification

1. 网页菜单改成 `1.5x`，按 `+`，结果应为 `1.75x`
2. 网页菜单改回 `1.0x`，按 `+`，结果应为 `1.25x`
3. 宿主热键设置 `1.75x` 后切分P，仍保持 `1.75x`
4. 切分P后立刻用网页菜单改成 `1.0x`，下一个视频仍保持 `1.0x`

## Risks And Mitigations

### Risk: 宿主读取实时倍速时序不稳定

Mitigation:

- 读取失败时退回 `_currentPlaybackRate`
- 不依赖单次读取作为唯一同步手段

### Risk: JS 消息发送过于频繁

Mitigation:

- 仅在显式 B 站倍速变更成功后发送
- 做最近一次发送值去重

### Risk: 再次把自动恢复当成用户输入

Mitigation:

- 宿主同步消息只从显式菜单 / 显式宿主设置路径发出
- 不从 transient navigation restore 直接发宿主同步

## Acceptance Criteria

- B 站网页菜单改倍速后，宿主热键从该真实值继续加减
- B 站网页菜单改回 `1.0x` 后，宿主热键从 `1.0x` 起算
- 分P/推荐/站内视频切换后的 authority 恢复行为不回退
- 仅 B 站视频页会触发网页 -> 宿主倍速同步
- 不新增第二套宿主 authority 状态
