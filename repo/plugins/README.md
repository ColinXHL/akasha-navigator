# AkashaNavigator 旧内置插件目录

普通插件已经迁移到独立的
[AkashaPlugins](https://github.com/ColinXHL/akasha-plugins) 聚合仓库。

主程序不再随安装包分发普通插件源码。用户通过“插件中心 → 可用插件”从官方
catalog 安装插件；旧版本已经安装的内置插件会保留原有本体、Profile 关联和配置，
并在官方 catalog 首次成功同步时认领为官方订阅。

`registry.json` 暂时保留为空索引，供旧版注册表服务兼容读取；该兼容层将在后续
清理阶段移除。
