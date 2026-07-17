# AkashaNavigator 插件仓库

官方和社区验证的 AkashaNavigator 插件集合。

## 目录结构

```
plugins/
├── README.md                    # 本文件
├── registry.json                # 插件索引（供插件市场使用）
└── genshin-direction-marker/    # 原神方向标记插件
    ├── plugin.json              # 插件清单
    ├── main.js                  # 插件入口
    └── README.md                # 插件说明
```

## 可用插件

| 插件 | 版本 | 描述 |
|------|------|------|
| [genshin-direction-marker](./genshin-direction-marker/) | 2.0.0 | 识别攻略视频中的方位词，在小地图上显示方向标记 |

## 安装插件

### 方式一：通过插件市场（推荐）

1. 打开 AkashaNavigator
2. 进入设置 → 插件管理
3. 浏览并安装所需插件

### 方式二：从本地 ZIP 安装

1. 从可信的插件发布页下载 ZIP 插件包，不要手动解压
2. 打开“插件中心 → 可用插件”，点击“从 ZIP 安装”
3. 安装后在“已安装插件”中把插件添加到目标 Profile

再次导入更高版本的同 ID 插件包会执行更新；相同或更低版本会被拒绝。Profile 设置独立存储，更新插件本体时会保留。

## 开发插件

请参阅 [插件开发文档](../docs/plugin-api.md)

## 贡献插件

1. Fork 本仓库
2. 在 `plugins/` 下创建插件目录
3. 提交 Pull Request

### 插件要求

- 必须包含 `plugin.json` 清单文件
- 必须包含 `README.md` 说明文档
- 代码需通过基本审核

## 许可证

各插件遵循其自身的许可证，默认为 MIT。
