## v1.4.0-alpha.3 🎉 Release Notes

**AkashaNavigator 1.4.0-alpha.3 更新发布！** 本次 Alpha 更新完成了官方插件仓库体系迁移，并集中改善插件中心的安装、更新和状态同步体验。

### ✨ 插件仓库体系

- 接入独立的 AkashaPlugins catalog，统一支持 GitHub、CNB 和自定义 Git 仓库
- 新增插件订阅、仓库刷新和原子安装流程，更新失败时不会留下不完整安装
- 自动保留插件声明的用户数据和配置，支持目录形式的 `savedFiles`
- 支持 Release 分发、完整性校验以及需要独立后端进程的 companion 插件
- 内置插件迁移到官方 catalog，移除旧版 registry、资源更新和遗留更新路径

### 🎨 插件中心

- 重做“可用插件”页面布局，使导航、工具栏、状态和卡片风格保持一致
- 检查更新后将可更新插件自动置顶，便于快速定位
- 修复“已安装插件”和“可用插件”使用不同 ViewModel 实例造成的状态不同步
- 页面切换时刷新实际显示的页面，安装、订阅和更新状态保持一致
- 支持从 ZIP 导入插件包，并集中管理仓库渠道和自动更新选项

### 🛠️ 兼容性与稳定性

- 修复预发布宿主被误判为低于同版本线稳定最低要求的问题
- 统一宿主版本来源，避免插件 API 与实际应用版本不一致
- 修复 `savedFiles` 目录标记被判定为无效路径导致插件更新失败的问题
- 更新完成后正确保留目录内用户文件
- 修复设置窗口关闭后旧 ViewModel 仍接收全局事件的问题
- 补充插件安装、版本比较、页面同步和事件生命周期回归测试

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.3/AkashaNavigator.Install.1.4.0-alpha.3.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.3/AkashaNavigator_v1.4.0-alpha.3.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版。本版本属于 Alpha 通道；插件安装和更新前请确认权限与后端组件提示。
