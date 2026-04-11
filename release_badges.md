## v1.3.0-alpha.1 🎉 Release Notes

**AkashaNavigator 1.3.0-alpha.1 更新发布！** 带来老板键隐藏模式，并继续强化播放器、控制栏与插件系统的稳定性

### ✨ 新增功能

**🕶️ 老板键隐藏模式**
- 新增老板键隐藏模式，一键隐藏窗口后仅保留恢复入口
- 隐藏模式下会拦截其他快捷键，避免误触影响当前桌面场景

**🎬 播放与导航协同增强**
- B站页面与宿主播放器的倍速状态现在可以更稳地保持同步
- 分P列表插件在跨视频切换后能正确刷新当前分P和导航上下文

### 🐛 问题修复

- 修复鼠标穿透场景下控制栏仍可能自动弹出的干扰问题
- 修复跨视频切换后分P导航仍可能沿用旧视频状态的问题
- 修复 B 站页面切换、导航等场景下播放器倍速可能被重置的问题
- 深化依赖注入、插件边界与持久化链路整理，降低插件运行与配置写入的异常风险

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.3.0-alpha.1/AkashaNavigator.Install.1.3.0-alpha.1.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.3.0-alpha.1/AkashaNavigator_v1.3.0-alpha.1.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版

发布日期: 2026-04-11
