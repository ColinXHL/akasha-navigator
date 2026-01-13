# 插件 API 参考

AkashaNavigator 插件系统基于 V8 JavaScript 引擎，支持 ES6+ 语法。

## API 列表

| API | 权限 | 说明 |
|-----|------|------|
| [Core API](core-api.md) | 无 | 日志、版本信息 |
| [Config API](config-api.md) | 无 | 插件配置读写 |
| [Subtitle API](subtitle-api.md) | `subtitle` | 视频字幕访问 |
| [Overlay API](overlay-api.md) | `overlay` | 覆盖层绘制 |
| [Player API](player-api.md) | `player` | 视频播放控制 |
| [Window API](window-api.md) | `window` | 窗口状态控制 |
| [Storage API](storage-api.md) | `storage` | 数据持久化 |
| [HTTP API](http-api.md) | `network` | 网络请求 |
| [Event API](event-api.md) | `events` | 应用事件监听 |


## 快速示例

```javascript
function onLoad(api) {
    // 日志（无需权限）
    api.log("插件已加载");
    
    // 配置（无需权限）
    var enabled = api.config.get("enabled", true);
    
    // 字幕监听（需要 subtitle 权限）
    api.subtitle.onChanged(function(subtitle) {
        if (subtitle) {
            api.log("字幕: " + subtitle.content);
        }
    });
    
    // 覆盖层绘制（需要 overlay 权限）
    api.overlay.setSize(200, 200);
    api.overlay.drawText("Hello", 10, 10, { fontSize: 16 });
    api.overlay.show();
}

function onUnload(api) {
    api.subtitle.removeAllListeners();
    api.overlay.clear();
    api.overlay.hide();
}
```

## Profile 信息

无需权限，只读访问当前 Profile 信息：

```javascript
api.log("Profile ID: " + api.profile.id);
api.log("Profile 名称: " + api.profile.name);
api.log("Profile 目录: " + api.profile.directory);
```

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 2.1.0 | 2026-01 | 新增 HotkeyApi、WebViewApi，重构插件目录结构 |
| 2.0.0 | 2025-12 | 迁移到 V8 引擎，支持 ES6+ |
| 1.0.0 | 2025-12 | 初始版本（Jint 引擎） |
