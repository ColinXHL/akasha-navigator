## v1.4.0 🎉 Release Notes

**AkashaNavigator 1.4.0 正式版发布！** 本次版本完成了应用更新、插件仓库和自动化插件分发体系升级，并集中改善插件安装、设置、更新与运行稳定性。

### ✨ 插件仓库与在线更新

- 接入独立插件 Catalog，支持 GitHub、CNB 和自定义 Git 仓库
- “可用插件”页面支持直接安装、更新、订阅以及从 ZIP 导入插件
- 应用与插件下载支持多源测速、自动选择和失败回退
- 插件包安装前校验版本、文件大小和 SHA-256，更新失败时自动回滚
- 支持 Release 分发、用户文件保留以及需要独立后端进程的 Companion 插件

### 🎮 原神自动化与插件设置

- 原神 Profile 默认推荐 Akasha 原神自动化插件
- 支持分别配置自动拾取和自动剧情快捷键，并显示 OSD 状态提示
- 修复原神自动化设置窗口显示“此插件没有可配置的设置项”的问题
- 兼容已安装插件，可从仓库清单恢复设置路径，无需重新安装
- “按住窥视”功能改为默认关闭，可在窗口设置中按需启用

### 🛡️ 稳定性与安全

- 插件更新采用原子替换并保留用户配置，避免不完整安装
- 增加高风险权限确认、插件路径校验和 Companion 进程生命周期管理
- 修复插件热重载后旧字幕 API 仍访问已释放 V8 引擎的问题
- 插件卸载时统一清理字幕、窗口事件、热键和网络资源
- 修复预发布宿主版本比较、插件中心状态同步及设置窗口事件残留问题

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0/AkashaNavigator.Install.1.4.0.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0/AkashaNavigator_v1.4.0.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版。首次启用自动化插件时请确认权限提示，并熟悉插件提供的紧急停止热键。
