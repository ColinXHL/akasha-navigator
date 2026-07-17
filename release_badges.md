## v1.4.0-alpha.2 🎉 Release Notes

**AkashaNavigator 1.4.0-alpha.2 更新发布！** 本次 Alpha 更新完善了 GitHub / CNB 双源选择，并为原神自动化插件补齐可配置快捷键与 OSD 状态提示。

### ✨ 新增功能

**🚀 统一下载源测速**
- 在高级设置顶部集中测试 GitHub 与 CNB 的实际安装包下载速度
- 测速结果同时用于应用更新推荐和插件下载自动选源
- 测速完成后自动同步应用更新源与插件下载源，并以结果卡片清晰展示速度、首字节时间和推荐源
- 应用更新支持手动选择 GitHub 或 CNB，插件下载仍保留自动选择与失败回退

**🎮 原神自动化快捷键**
- 原神 Profile 默认推荐 Akasha 原神自动化插件
- 插件设置页可独立配置自动拾取与自动剧情快捷键
- 默认使用 `F9` 切换自动拾取、`F12` 切换自动剧情
- 快捷键切换后显示自动消失的 OSD 状态提示

### 🎨 界面与稳定性

- 优化插件快捷键设置控件的比例、间距和层级
- 修复 OSD 在非 UI 线程调用时无法正常显示或消失的问题
- 下载源测速采用限时自适应读取，每个源最多读取 8 MiB，兼顾速度判断与流量消耗
- 测速结果缓存 6 小时，并在网络或下载失败时保留备用源回退能力

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.2/AkashaNavigator.Install.1.4.0-alpha.2.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.2/AkashaNavigator_v1.4.0-alpha.2.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版。本版本属于 Alpha 通道；首次启用自动化插件时请确认权限提示，并根据需要调整快捷键。
