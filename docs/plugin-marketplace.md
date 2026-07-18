# 插件发布指南

本文档介绍如何将你的插件发布到 AkashaNavigator 插件市场。

## 发布前准备

### 1. 完善 plugin.json

确保你的 `plugin.json` 包含完整的元数据：

```json
{
  "id": "my-awesome-plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "main": "main.js",
  "description": "一句话描述插件功能",
  "author": "Your Name",
  "homepage": "https://github.com/yourname/my-plugin",
  "permissions": ["subtitle", "overlay"],
  "profiles": ["genshin"],
  "defaultConfig": {
    "enabled": true,
    "duration": 3000
  }
}
```

**必填字段：**
- `id` - 唯一标识符，使用小写字母和连字符
- `name` - 显示名称
- `version` - 语义化版本号
- `main` - 入口文件
- `description` - 简短描述
- `author` - 作者名称
- `permissions` - 所需权限列表

**推荐字段：**
- `homepage` - 项目主页或仓库地址
- `profiles` - 推荐使用的 Profile 列表
- `defaultConfig` - 默认配置

### 2. 添加 README.md

在插件目录中添加 `README.md`，包含：

- 插件功能介绍
- 使用说明
- 配置选项说明
- 截图或 GIF 演示（推荐）

### 3. 添加设置界面（可选）

如果插件有可配置选项，可以添加 `settings_ui.json` 提供可视化设置界面：

```json
{
  "sections": [
    {
      "title": "基本设置",
      "items": [
        {
          "type": "checkbox",
          "key": "enabled",
          "label": "启用插件",
          "default": true
        },
        {
          "type": "number",
          "key": "duration",
          "label": "显示时长(毫秒)",
          "default": 3000,
          "min": 0,
          "max": 10000
        }
      ]
    }
  ]
}
```

详见 [设置界面定义](#设置界面定义) 章节。

## 发布流程

### 方式一：提交到官方仓库

1. Fork [AkashaPlugins](https://github.com/ColinXHL/akasha-plugins)
2. 在 `plugins/<plugin-id>/` 中添加 Manifest v2、代码、资源和测试
3. 运行仓库提供的 manifest、索引和测试校验
4. 提交 Pull Request
5. 合并后由 CI 生成 `repo.json` 并发布 `catalog` 分支

### 方式二：自托管

自托管源也必须提供与官方仓库相同的 catalog 结构，而不是单独维护一个
`registry.json` 或在应用更新清单中添加 ZIP 地址：

1. 维护 `plugins/*/manifest.json` 和根部 `repo.json`
2. 将可安装内容发布到 `catalog` 分支
3. 对带后端的插件发布带大小和 SHA-256 的 Release 资产
4. 用户在插件仓库设置中选择自定义 Git URL

GitHub、CNB 和自定义 Git URL 是同一逻辑 catalog 的同步或自托管渠道。

## 插件目录结构

```
my-plugin/
├── plugin.json        # 插件清单（必需）
├── main.js            # 入口文件（必需）
├── README.md          # 说明文档（推荐）
├── settings_ui.json   # 设置界面定义（可选）
├── icon.png           # 插件图标（可选，64x64）
└── assets/            # 资源文件（可选）
    └── marker.png
```

## 设置界面定义

### 支持的控件类型

#### 文本输入 (text)

```json
{
  "type": "text",
  "key": "apiKey",
  "label": "API 密钥",
  "default": "",
  "placeholder": "请输入 API 密钥"
}
```

#### 数字输入 (number)

```json
{
  "type": "number",
  "key": "duration",
  "label": "显示时长(毫秒)",
  "default": 3000,
  "min": 0,
  "max": 10000,
  "step": 100
}
```

#### 复选框 (checkbox)

```json
{
  "type": "checkbox",
  "key": "enabled",
  "label": "启用功能",
  "default": true
}
```

#### 下拉框 (select)

```json
{
  "type": "select",
  "key": "style",
  "label": "显示样式",
  "options": [
    { "value": "arrow", "label": "箭头" },
    { "value": "text", "label": "文字" },
    { "value": "both", "label": "箭头+文字" }
  ],
  "default": "arrow"
}
```

#### 滑块 (slider)

```json
{
  "type": "slider",
  "key": "opacity",
  "label": "透明度",
  "default": 0.8,
  "min": 0.1,
  "max": 1.0,
  "step": 0.1
}
```

#### 按钮 (button)

```json
{
  "type": "button",
  "label": "调整位置",
  "action": "enterEditMode"
}
```

内置动作：
- `enterEditMode` - 进入覆盖层编辑模式
- `resetConfig` - 重置配置为默认值
- `openPluginFolder` - 打开插件目录

自定义动作会触发插件的 `onSettingAction(actionName)` 回调。

#### 分组 (group)

```json
{
  "type": "group",
  "title": "高级设置",
  "items": [
    { "type": "checkbox", "key": "debug", "label": "调试模式" }
  ]
}
```

### 完整示例

```json
{
  "sections": [
    {
      "title": "基本设置",
      "items": [
        {
          "type": "checkbox",
          "key": "enabled",
          "label": "启用插件",
          "default": true
        },
        {
          "type": "select",
          "key": "style",
          "label": "显示样式",
          "options": [
            { "value": "arrow", "label": "箭头" },
            { "value": "text", "label": "文字" }
          ],
          "default": "arrow"
        }
      ]
    },
    {
      "title": "覆盖层设置",
      "items": [
        {
          "type": "number",
          "key": "overlay.size",
          "label": "大小",
          "default": 200,
          "min": 100,
          "max": 500
        },
        {
          "type": "slider",
          "key": "overlay.opacity",
          "label": "透明度",
          "default": 0.8,
          "min": 0.1,
          "max": 1.0,
          "step": 0.1
        },
        {
          "type": "button",
          "label": "调整位置和大小",
          "action": "enterEditMode"
        }
      ]
    }
  ]
}
```

## 权限说明

在发布前，确保只申请必要的权限：

| 权限 | 说明 | 敏感度 |
|------|------|--------|
| `subtitle` | 访问视频字幕 | 低 |
| `overlay` | 显示覆盖层 | 低 |
| `player` | 控制视频播放 | 低 |
| `window` | 控制窗口状态 | 中 |
| `storage` | 数据持久化 | 低 |
| `network` | HTTP 网络请求 | 高 ⚠️ |
| `events` | 应用事件监听 | 低 |
| `audio` | 语音识别 | 中 |

高敏感度权限会在安装时显示警告，请在 README 中说明为何需要该权限。

## 审核标准

提交到官方仓库的插件需要满足：

1. **功能完整** - 插件功能正常工作
2. **无恶意代码** - 不包含恶意或危险代码
3. **权限最小化** - 只申请必要的权限
4. **文档完善** - 包含清晰的使用说明
5. **代码质量** - 代码结构清晰，无明显 bug

## 版本更新

更新插件时：

1. 更新 `manifest.json` 中唯一的插件 `version`
2. 在 README 中添加更新日志
3. 提交 PR
4. 等待测试、Release（如需要）和 catalog 发布全部成功

插件中心、启动检查和手动检查都只比较 catalog 版本。不要在
AkashaNavigator 的 `notice.json`、`repo/plugins` 或其他远程目录重复发布
插件版本。

版本号遵循语义化版本规范：
- `MAJOR.MINOR.PATCH`
- 例如：`1.0.0` → `1.0.1`（修复 bug）→ `1.1.0`（新功能）→ `2.0.0`（破坏性变更）

## 常见问题

### Q: 如何测试插件？

在本地 Profile 的 `plugins/` 目录下创建插件文件夹，重启应用即可加载。

### Q: 如何调试插件？

使用 `api.log()` 输出日志，在应用的日志窗口中查看。

### Q: 插件可以依赖其他插件吗？

目前不支持插件依赖，请确保插件独立运行。

---

相关文档：
- [插件开发指南](plugin-development.md)
- [API 参考](api/README.md)
- [Profile 创建指南](profile-guide.md)
