<div align="center">
  <h1 align="center">
    <a href="https://github.com/ColinXHL/sandrone-player"><img src="assets/sandrone-player-logo.png" width="128"></a>
    <br/>
    <a href="https://github.com/ColinXHL/sandrone-player">SandronePlayer</a>
  </h1>
</div>

<br/>

<div align="center">
  <a href="https://dotnet.microsoft.com/zh-cn/download/dotnet/latest/runtime"><img alt="Windows" src="https://img.shields.io/badge/platform-Windows-blue?logo=windowsxp&style=flat-square&color=1E9BFA" /></a>
  <a href="https://github.com/ColinXHL/sandrone-player/releases"><img alt="下载数" src="https://img.shields.io/github/downloads/ColinXHL/sandrone-player/total?logo=github&style=flat-square&color=1E9BFA"></a>
  <a href="https://github.com/ColinXHL/sandrone-player/releases"><img alt="Release" src="https://img.shields.io/github/v/release/ColinXHL/sandrone-player?logo=visualstudio&style=flat-square&color=1E9BFA"></a>
</div>

<br/>

<div align="center">
🌟 点一下右上角的 Star，Github 主页就能收到软件更新通知了哦~
</div>

<div align="center">
    <img src="https://img.alicdn.com/imgextra/i1/2042484851/O1CN01OL1E1v1lhoM7Wdmup_!!2042484851.gif" alt="Star" width="186" height="60">
  </a>
</div>

<br/>

[English](./README-en.md) | [中文](./README.md)

SandronePlayer · 悬浮攻略播放器，一个基于 WPF + WebView2 的 Windows 悬浮网页播放器，专为游戏时观看攻略视频设计。

## 功能

* 核心功能
  * **始终置顶**：悬浮窗口始终显示在游戏和其他应用上方
  * **全局快捷键**：无需切换窗口即可控制播放，支持自定义组合键
  * **鼠标穿透**：开启后可与播放器下方的应用交互，不影响游戏操作
  * **透明度调节**：支持 20% 到 100% 透明度设置
  * **边缘吸附**：窗口自动吸附到屏幕边缘
  * **Cookie 持久化**：保持 B 站等网站的登录状态

* 插件系统
  * **JavaScript 插件**：基于 V8 引擎的 JS 插件架构
  * **权限控制**：插件需声明所需权限（字幕、覆盖层、播放器、窗口、存储、网络、事件）
  * **插件市场**：支持订阅源，一键安装插件
  * **热重载**：开发时无需重启即可重载插件

* Profile 系统
  * **游戏配置**：为不同游戏配置专属快捷键和插件
  * **配置市场**：分享和下载他人的游戏配置
  * **导入导出**：方便备份和迁移配置

* 其他功能
  * **历史记录**：自动记录浏览历史
  * **收藏夹**：收藏常用页面
  * **字幕支持**：解析视频字幕，供插件访问
  * **覆盖层系统**：插件可创建自定义 UI 覆盖层

<div align="center">
  <p>双窗口架构：独立的播放器窗口和控制栏窗口</p>
</div>

## 截图

> *即将添加...*

## 下载

> [!NOTE]
> 下载地址：[⚡Github 下载](https://github.com/ColinXHL/sandrone-player/releases)
>
> 便携版，所有数据存储在程序目录的 `User/` 文件夹中。

## 使用方法

你的系统需要满足以下条件：
  * Windows 10 或更高版本的 64 位系统
  * [.NET 8 运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/latest/runtime)（没有的话，启动程序，系统会提示下载安装）
  * [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)（Windows 10/11 通常已预装）

**⚠️注意：**
1. 需要管理员权限运行（用于在游戏中使用全局快捷键）
2. 窗口大小变化时可能需要重新调整位置

## 默认快捷键

| 按键 | 功能 |
|------|------|
| `` ` `` | 播放 / 暂停 |
| `5` | 后退 5 秒 |
| `6` | 前进 5 秒 |
| `7` | 降低透明度 |
| `8` | 增加透明度 |
| `0` | 切换鼠标穿透 |

> 💡 快捷键可在设置中自定义，支持组合键（Ctrl、Alt、Shift）。

## 从源码构建

```powershell
# 克隆仓库
git clone https://github.com/ColinXHL/sandrone-player.git
cd sandrone-player

# 构建
dotnet build -c Release

# 运行
dotnet run --project SandronePlayer

# 运行测试
dotnet test
```

## FAQ

* 为什么需要管理员权限？
  * 因为游戏通常以管理员权限启动，软件不以管理员权限启动的话没有权限模拟键盘操作。

* 支持哪些视频网站？
  * 理论上支持所有网页视频，主要针对 B 站优化。

## 致谢

本项目的完成离不开以下项目：
* [kachina-installer](https://github.com/YuehaiTeam/kachina-installer)
* [better-genshin-impact](https://github.com/babalae/better-genshin-impact)
* [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
* [ClearScript](https://github.com/nicksoftware/ClearScript) - V8 JavaScript 引擎

## 许可证

![MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

## 问题反馈

提 [Issue](https://github.com/ColinXHL/sandrone-player/issues)
