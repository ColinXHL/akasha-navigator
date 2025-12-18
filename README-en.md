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
  <a href="https://github.com/ColinXHL/sandrone-player/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/ColinXHL/sandrone-player/total?logo=github&style=flat-square&color=1E9BFA"></a>
  <a href="https://github.com/ColinXHL/sandrone-player/releases"><img alt="Release" src="https://img.shields.io/github/v/release/ColinXHL/sandrone-player?logo=visualstudio&style=flat-square&color=1E9BFA"></a>
</div>

<br/>

<div align="center">
🌟 Click the Star button in the top right corner to receive update notifications on your Github homepage~
</div>

<br/>

[English](./README-en.md) | [中文](./README.md)

SandronePlayer · A floating web player built with WPF + WebView2 for Windows, designed for watching tutorial/guide videos while gaming.

## Features

* Core Features
  * **Always on Top**: Floating window stays above games and other applications
  * **Global Hotkeys**: Control playback without leaving your game, fully customizable
  * **Mouse Click-Through**: Interact with apps behind the player without affecting gameplay
  * **Adjustable Opacity**: Set transparency from 20% to 100%
  * **Edge Snapping**: Window automatically snaps to screen edges
  * **Cookie Persistence**: Stay logged in to Bilibili and other websites

* Plugin System
  * **JavaScript Plugins**: JS plugin architecture powered by V8 engine
  * **Permission Control**: Plugins declare required permissions (subtitle, overlay, player, window, storage, network, events)
  * **Plugin Marketplace**: Subscribe to sources and install plugins with one click
  * **Hot Reload**: Reload plugins during development without restarting

* Profile System
  * **Game Configurations**: Configure dedicated hotkeys and plugins for different games
  * **Profile Marketplace**: Share and download configurations from others
  * **Import/Export**: Easy backup and migration of configurations

* Other Features
  * **History**: Automatically track browsing history
  * **Bookmarks**: Save favorite pages
  * **Subtitle Support**: Parse video subtitles for plugin access
  * **Overlay System**: Plugins can create custom UI overlays

<div align="center">
  <p>Dual-window architecture: Separate player window and control bar window</p>
</div>

## Screenshots

> *Coming soon...*

## Download

> [!NOTE]
> Download: [⚡Github Download](https://github.com/ColinXHL/sandrone-player/releases)
>
> Portable version - all data stored in `User/` folder relative to executable.

## Usage

Your system needs to meet the following requirements:
  * Windows 10 or higher (64-bit)
  * [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/latest/runtime) (system will prompt to download if not installed)
  * [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed on Windows 10/11)

**⚠️ Notes:**
1. Administrator privileges required (for global hotkeys in games)
2. Window position may need adjustment after resizing

## Default Hotkeys

| Key | Function |
|-----|----------|
| `` ` `` | Play / Pause |
| `5` | Seek Backward (5s) |
| `6` | Seek Forward (5s) |
| `7` | Decrease Opacity |
| `8` | Increase Opacity |
| `0` | Toggle Click-Through |

> 💡 Hotkeys are fully customizable in Settings. Modifier keys (Ctrl, Alt, Shift) are supported.

## Documentation

- [User Guide](docs/user-guide.md) - Installation, usage, FAQ
- [Plugin Development](docs/plugin-development.md) - Getting started with plugins
- [API Reference](docs/api/README.md) - Plugin API documentation
- [Profile Guide](docs/profile-guide.md) - Creating and publishing profiles

## Build from Source

```powershell
# Clone the repository
git clone https://github.com/ColinXHL/sandrone-player.git
cd sandrone-player

# Build
dotnet build -c Release

# Run
dotnet run --project SandronePlayer

# Run Tests
dotnet test
```

## FAQ

* Why does it need administrator privileges?
  * Games usually run with administrator privileges. Without admin rights, the software cannot simulate keyboard operations.

* Which video sites are supported?
  * Theoretically supports all web videos, primarily optimized for Bilibili.

## Acknowledgments

This project would not be possible without:
* [kachina-installer](https://github.com/YuehaiTeam/kachina-installer)
* [better-genshin-impact](https://github.com/babalae/better-genshin-impact)
* [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
* [ClearScript](https://github.com/nicksoftware/ClearScript) - V8 JavaScript Engine

## License

![MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

## Feedback

Submit an [Issue](https://github.com/ColinXHL/sandrone-player/issues)
