# Player API

需要 `player` 权限。提供视频播放控制功能。

## 播放控制

### api.player.play()

开始播放。

```javascript
api.player.play();
```

### api.player.pause()

暂停播放。

```javascript
api.player.pause();
```

### api.player.togglePlay()

切换播放/暂停状态。

```javascript
api.player.togglePlay();
```

## 进度控制

### api.player.seek(seconds)

跳转到指定时间。

```javascript
api.player.seek(60);  // 跳转到 1 分钟
```

### api.player.seekRelative(seconds)

相对跳转。

```javascript
api.player.seekRelative(10);   // 前进 10 秒
api.player.seekRelative(-5);   // 后退 5 秒
```

### api.player.getCurrentTime()

获取当前播放时间（秒）。

```javascript
var time = api.player.getCurrentTime();
api.log("当前时间: " + time + " 秒");
```

### api.player.getDuration()

获取视频总时长（秒）。

```javascript
var duration = api.player.getDuration();
api.log("总时长: " + duration + " 秒");
```

## 播放速度

### api.player.setPlaybackRate(rate)

设置播放速度。

```javascript
api.player.setPlaybackRate(1.5);  // 1.5 倍速
api.player.setPlaybackRate(0.5);  // 0.5 倍速
```

### api.player.getPlaybackRate()

获取当前播放速度。

```javascript
var rate = api.player.getPlaybackRate();
```

## 音量控制

### api.player.setVolume(volume)

设置音量（0.0 - 1.0）。

```javascript
api.player.setVolume(0.5);  // 50% 音量
```

### api.player.getVolume()

获取当前音量。

```javascript
var volume = api.player.getVolume();
```

### api.player.setMuted(muted)

设置静音状态。

```javascript
api.player.setMuted(true);
```

### api.player.isMuted()

获取静音状态。

```javascript
var muted = api.player.isMuted();
```
