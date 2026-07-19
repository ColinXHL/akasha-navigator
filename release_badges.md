## v1.4.0-alpha.4 🎉 Release Notes

**AkashaNavigator 1.4.0-alpha.4 更新发布！** 本次 Alpha 更新修复了新版仓库插件设置界面无法加载的问题。

### 🛠️ 插件设置

- 修复仓库插件安装时未将 `settings` 路径写入运行时清单的问题
- 设置窗口现在会按插件清单声明的相对路径加载设置界面
- 兼容已经安装的仓库插件，可从随包保留的仓库清单恢复设置路径，无需重新安装插件
- 继续兼容设置文件位于插件根目录的旧版插件
- 拒绝越出插件目录的设置文件路径

### 🎮 原神自动化

- 修复 Akasha 原神自动化 0.4.4 设置窗口显示“此插件没有可配置的设置项”
- 升级主程序后即可恢复设置界面，无需更新或重新安装原神自动化插件

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.4/AkashaNavigator.Install.1.4.0-alpha.4.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.4/AkashaNavigator_v1.4.0-alpha.4.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版。本版本属于 Alpha 通道。
