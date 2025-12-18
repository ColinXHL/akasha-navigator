# Window API

需要 `window` 权限。提供窗口状态控制功能。

## 透明度

### api.window.setOpacity(opacity)

设置窗口透明度（0.2 - 1.0）。

```javascript
api.window.setOpacity(0.5);
```

### api.window.getOpacity()

获取当前透明度。

```javascript
var opacity = api.window.getOpacity();
```

## 鼠标穿透

### api.window.setClickThrough(enabled)

设置鼠标穿透模式。

```javascript
api.window.setClickThrough(true);
```

### api.window.isClickThrough()

获取穿透模式状态。

```javascript
var clickThrough = api.window.isClickThrough();
```

## 窗口置顶

### api.window.setTopmost(topmost)

设置窗口置顶状态。

```javascript
api.window.setTopmost(true);
```

### api.window.isTopmost()

获取置顶状态。

```javascript
var topmost = api.window.isTopmost();
```

## 窗口位置

### api.window.getBounds()

获取窗口位置和大小。

```javascript
var bounds = api.window.getBounds();
api.log("位置: " + bounds.x + ", " + bounds.y);
api.log("大小: " + bounds.width + " x " + bounds.height);
```
