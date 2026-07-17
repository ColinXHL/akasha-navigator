## v1.3.0-alpha.4 🎉 Release Notes

**AkashaNavigator 1.3.0-alpha.4 更新发布！** 本次重点加入安全的进程外自动化插件链路、窗口边缘居中吸附和统一退出机制，并改善插件安装与设置体验。

### ✨ 新增功能

**🤖 安全的进程外自动化插件**
- 新增 `companion` 高风险权限及受宿主管理的 Worker 生命周期
- 插件启停、Profile 切换、更新、卸载和应用退出时会同步停止关联进程
- 支持从本地 ZIP 安装或更新插件，并校验版本、目录边界、文件数量、包大小及 SHA-256 清单
- 为 Akasha 原神自动化插件提供完整宿主能力，普通 JavaScript 插件不能直接绕过权限启动任意进程

**🧲 窗口边缘居中吸附**
- 顶部吸附改为显示器物理顶部，不再受顶部任务栏工作区边界影响
- 新增上、下边缘水平居中及左、右边缘垂直居中锚点
- 中心吸附使用独立阈值与滞后，减少拖动时在居中和自由贴边之间抖动
- 保留原有四角贴边，并继续使用物理像素和多显示器坐标

**📦 插件安装与设置增强**
- 插件中心新增“从 ZIP 安装”，支持同 ID 高版本插件原位更新并保留 Profile 配置
- 动态设置面板新增多行文本、辅助说明和受限的插件内相对目录动作
- 多行输入框改为左上角输入，统一深色滚动条，并正确处理焦点与内外层滚轮切换
- 文件夹动作会校验路径必须位于插件目录内，避免目录穿越

### 🛡️ 稳定性改进

**统一退出流程**
- 新增幂等的 `ShutdownCoordinator`，按顺序停止热键、定时器、插件、Worker、覆盖层和附属窗口
- 退出时显式释放 WebView2，并记录浏览器进程组退出及各阶段耗时
- 应用退出时统一释放根 DI 容器，使已解析的 `IDisposable` 单例正确清理

**多显示器与控制栏**
- 进一步统一控制栏的物理像素定位和 WPF 坐标状态
- 修复不同 DPI、显示器切换以及隐藏后重新显示时控制栏可能漂移的问题

### 🐛 问题修复

- 修复插件设置多行输入框看似无法聚焦、鼠标滚轮不能滚动名单内容的问题
- 修复清空插件快捷键后配置值可能没有同步的问题
- 修复 WebView2 和根 ServiceProvider 未完整释放而增加残留进程概率的问题
- 修复插件 ZIP 更新失败时错误信息不够明确的问题

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.3.0-alpha.4/AkashaNavigator.Install.1.3.0-alpha.4.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.3.0-alpha.4/AkashaNavigator_v1.3.0-alpha.4.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版。本版本属于 Alpha 通道，首次启用自动化插件前请确认高风险权限提示，并熟悉插件提供的紧急停止热键。

发布日期: 2026-07-17
