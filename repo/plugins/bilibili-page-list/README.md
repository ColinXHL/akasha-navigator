# B站分P列表插件

显示B站视频分P列表独立窗口，支持快速切换分P（快捷键/按钮）。

## 功能

- 自动检测B站视频URL
- 获取并显示视频分P列表
- 点击分P项快速跳转
- 高亮显示当前播放的分P
- 快捷键控制显示/隐藏
- **快速切换上一个/下一个分P（快捷键 + 面板按钮）**
- 弹幕/字幕控制按钮

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| Alt+P | 切换分P列表显示 |
| Alt+D | 弹幕开关 |
| Alt+S | 字幕开关 |
| **Alt+Left** | **上一个分P** |
| **Alt+Right** | **下一个分P** |

## 面板按钮

- **◀** - 上一个分P（到达第一个时禁用）
- **▶** - 下一个分P（到达最后一个时禁用）
- **弹** - 弹幕开关
- **字** - 字幕开关

## 配置

在插件设置中可以配置：

- 快捷键绑定
- 窗口位置和尺寸

## 支持的URL格式

- `https://www.bilibili.com/video/BVxxxxxxxxxx`
- `https://www.bilibili.com/video/BVxxxxxxxxxx?p=2`
- `https://www.bilibili.com/video/avxxxxxxx`
- `https://www.bilibili.com/video/avxxxxxxx?p=3`

## 注意事项

- 单P视频不会显示分P列表
- 需要网络权限访问B站API
- 切换分P时会显示OSD提示（格式：P2/10: 第二章）
- 到达边界时会显示提示，不会循环跳转
