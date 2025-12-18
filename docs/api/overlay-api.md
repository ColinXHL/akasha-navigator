# Overlay API

需要 `overlay` 权限。提供覆盖层绘制功能。

## 位置和大小

### api.overlay.setPosition(x, y)

设置覆盖层位置（逻辑像素）。

```javascript
api.overlay.setPosition(100, 100);
```

### api.overlay.setSize(width, height)

设置覆盖层大小。

```javascript
api.overlay.setSize(200, 200);
```

### api.overlay.getRect()

获取覆盖层位置和大小。

```javascript
var rect = api.overlay.getRect();
api.log("位置: " + rect.x + ", " + rect.y);
api.log("大小: " + rect.width + " x " + rect.height);
```

## 显示控制

### api.overlay.show()

显示覆盖层。

```javascript
api.overlay.show();
```

### api.overlay.hide()

隐藏覆盖层。

```javascript
api.overlay.hide();
```

## 方向标记

### api.overlay.showMarker(direction, duration)

显示方向标记。

```javascript
// 显示北方向标记，常驻
api.overlay.showMarker("north", 0);

// 显示东方向标记，3秒后消失
api.overlay.showMarker("east", 3000);
```

**支持的方向：**
- `north`, `n`, `up`
- `northeast`, `ne`
- `east`, `e`, `right`
- `southeast`, `se`
- `south`, `s`, `down`
- `southwest`, `sw`
- `west`, `w`, `left`
- `northwest`, `nw`

### api.overlay.clearMarkers()

清除所有方向标记。

```javascript
api.overlay.clearMarkers();
```

## 绘图

### api.overlay.drawText(text, x, y, options)

绘制文本，返回元素 ID。

```javascript
var id = api.overlay.drawText("Hello", 10, 10, {
    fontSize: 16,
    color: "#FFFFFF",
    backgroundColor: "#000000",
    opacity: 0.8,
    duration: 3000  // 3秒后消失，0为常驻
});
```

### api.overlay.drawRect(x, y, width, height, options)

绘制矩形，返回元素 ID。

```javascript
var id = api.overlay.drawRect(10, 10, 100, 50, {
    fill: "#FF0000",
    stroke: "#FFFFFF",
    strokeWidth: 2,
    opacity: 0.5,
    cornerRadius: 5
});
```

### api.overlay.drawImage(path, x, y, options)

绘制图片，返回元素 ID。路径相对于插件目录。

```javascript
var id = api.overlay.drawImage("icon.png", 10, 10, {
    width: 32,
    height: 32,
    opacity: 1.0
});
```

### api.overlay.removeElement(elementId)

移除指定绘图元素。

```javascript
api.overlay.removeElement(id);
```

### api.overlay.clear()

清除所有绘图元素。

```javascript
api.overlay.clear();
```

## 编辑模式

### api.overlay.enterEditMode()

进入编辑模式（可拖拽调整位置和大小）。

```javascript
api.overlay.enterEditMode();
```

### api.overlay.exitEditMode()

退出编辑模式，自动保存配置。

```javascript
api.overlay.exitEditMode();
```
