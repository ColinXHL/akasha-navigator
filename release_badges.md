## v1.4.0-alpha.1 🎉 Release Notes

**AkashaNavigator 1.4.0-alpha.1 更新发布！** 本次升级重构了应用与插件的分发链路：应用更新、远程插件和插件资源现在使用同一份可校验的在线清单，并支持多下载源自动回退。

### ✨ 新增功能

**📡 统一在线更新清单**
- 新增 v2 更新清单模型，同时兼容原有应用更新字段
- 支持 ETag 条件请求和本地缓存，网络不可用时可回退至最近一次有效清单
- 应用更新、远程插件和插件资源共享同一套版本与完整性校验

**🧩 远程插件中心**
- “可用插件”页面可直接展示、安装和更新在线插件
- 下载过程显示进度并支持取消，失败时自动切换 GitHub、CNB 等备用源
- 插件包安装前校验文件大小、SHA-256、宿主版本和插件版本

**📚 可独立更新的插件资源**
- Akasha 原神自动化的 BetterGI 拾取黑名单可脱离插件包单独更新
- 资源更新校验最低插件版本、文件大小、SHA-256、JSON 格式和条目数量
- 使用原子替换并保留旧版本回退，Worker 启动时优先读取已验证资源

### 🛡️ 稳定性改进

- 下载源按用户偏好、历史成功记录和可用性排序
- 高级设置新增首选下载源配置
- 远程目录异常不会影响已安装插件和本地 ZIP 安装
- Companion Worker 通过受控环境变量读取宿主管理的插件数据目录
- 统一语义化版本比较，正确处理 Alpha 等预发布版本

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.1/AkashaNavigator.Install.1.4.0-alpha.1.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.0-alpha.1/AkashaNavigator_v1.4.0-alpha.1.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用安装版。本版本属于 Alpha 通道；启用自动化插件前请确认高风险权限提示，并熟悉插件提供的紧急停止热键。
