# Subtitle API

需要 `subtitle` 权限。提供视频字幕访问功能。

## 属性

### api.subtitle.hasSubtitles

检查是否有字幕数据。

```javascript
if (api.subtitle.hasSubtitles) {
    api.log("字幕已加载");
}
```

## 方法

### api.subtitle.getCurrent(timeInSeconds)

根据时间戳获取当前字幕。

```javascript
var subtitle = api.subtitle.getCurrent(30.5);
if (subtitle) {
    api.log("字幕内容: " + subtitle.content);
}
```

**返回值：**
```javascript
{
    from: 30.0,      // 开始时间（秒）
    to: 33.5,        // 结束时间（秒）
    content: "字幕文本"
}
```

### api.subtitle.getAll()

获取所有字幕。

```javascript
var subtitles = api.subtitle.getAll();
subtitles.forEach(function(s) {
    api.log(s.from + " - " + s.to + ": " + s.content);
});
```

## 事件监听

### api.subtitle.onLoaded(callback)

监听字幕加载事件。返回监听器 ID。

```javascript
var listenerId = api.subtitle.onLoaded(function(subtitleData) {
    api.log("字幕已加载，共 " + subtitleData.body.length + " 条");
});
```

**subtitleData 结构：**
```javascript
{
    language: "zh-CN",
    body: [
        { from: 0.0, to: 2.5, content: "第一条字幕" },
        { from: 2.5, to: 5.0, content: "第二条字幕" }
    ],
    sourceUrl: "https://..."
}
```

### api.subtitle.onChanged(callback)

监听当前字幕变化。返回监听器 ID。

```javascript
var listenerId = api.subtitle.onChanged(function(subtitle) {
    if (subtitle) {
        api.log("当前字幕: " + subtitle.content);
    } else {
        api.log("无字幕");
    }
});
```

### api.subtitle.onCleared(callback)

监听字幕清除事件。返回监听器 ID。

```javascript
var listenerId = api.subtitle.onCleared(function() {
    api.log("字幕已清除");
});
```

### api.subtitle.removeListener(listenerId)

移除指定监听器。

```javascript
api.subtitle.removeListener(listenerId);
```

### api.subtitle.removeAllListeners()

移除所有字幕监听器。

```javascript
api.subtitle.removeAllListeners();
```

## 完整示例

```javascript
function onLoad(api) {
    // 监听字幕变化
    api.subtitle.onChanged(function(subtitle) {
        if (subtitle && subtitle.content.includes("东")) {
            api.overlay.showMarker("east", 3000);
        }
    });
}

function onUnload(api) {
    api.subtitle.removeAllListeners();
}
```
