# Event API

需要 `events` 权限。提供应用程序事件监听功能。

## api.event.on(eventName, callback)

注册事件监听器。

```javascript
api.event.on("playStateChanged", function(data) {
    api.log("播放状态: " + (data.playing ? "播放中" : "已暂停"));
});

api.event.on("opacityChanged", function(data) {
    api.log("透明度: " + data.opacity);
});
```

## api.event.off(eventName, callback)

取消事件监听。

```javascript
// 移除特定监听器
api.event.off("playStateChanged", myCallback);

// 移除该事件的所有监听器
api.event.off("playStateChanged");
```

## 支持的事件

| 事件名 | 数据 | 说明 |
|--------|------|------|
| `playStateChanged` | `{ playing: boolean }` | 播放状态变化 |
| `timeUpdate` | `{ currentTime: number }` | 播放时间更新 |
| `opacityChanged` | `{ opacity: number }` | 透明度变化 |
| `clickThroughChanged` | `{ enabled: boolean }` | 穿透模式变化 |
| `urlChanged` | `{ url: string }` | URL 变化 |
| `profileChanged` | `{ profileId: string }` | Profile 切换 |

## 示例

```javascript
function onLoad(api) {
    api.event.on("urlChanged", function(data) {
        api.log("页面切换到: " + data.url);
    });
    
    api.event.on("profileChanged", function(data) {
        api.log("Profile 切换到: " + data.profileId);
    });
}
```
