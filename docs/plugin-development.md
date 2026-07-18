# 插件开发指南

本文档介绍如何为 AkashaNavigator 开发插件。

## 目录

- [快速开始](#快速开始)
- [插件结构](#插件结构)
- [生命周期](#生命周期)
- [权限系统](#权限系统)
- [设置界面](#设置界面)
- [调试技巧](#调试技巧)
- [最佳实践](#最佳实践)

---

## 快速开始

官方插件在
[AkashaPlugins](https://github.com/ColinXHL/akasha-plugins)
中开发和发布。仓库的 `catalog` 分支、根部 `repo.json` 以及每个插件的
`manifest.json` 共同组成插件中心的唯一目录；AkashaNavigator 的
`notice.json` 只负责应用更新，不接受插件版本或下载地址。

开发新插件时：

1. Fork 或克隆 AkashaPlugins
2. 在 `plugins/<plugin-id>/` 中添加 `manifest.json`、脚本和资源
3. 运行仓库校验与测试
4. 提交 PR；合并后由 CI 生成并发布 catalog

普通 JavaScript 插件使用 `repository` 分发。包含伴生进程的插件使用
`release` 分发，并在同一插件目录维护前端、Worker、协议和打包脚本。
catalog 中只暴露 `manifest.json` 的插件版本，协议版本不参与用户可见的
版本比较。

### 最小插件示例

下面的 `plugin.json` 是安装后运行时清单的最小示例。提交到 AkashaPlugins
时，应按仓库的 Manifest v2 模板编写 `manifest.json`，由 catalog 构建流程
校验并转换运行时清单。

**plugin.json**
```json
{
  "id": "my-plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "main": "main.js",
  "description": "一个简单的示例插件",
  "author": "Your Name",
  "permissions": []
}
```

**main.js**
```javascript
function onLoad(api) {
    api.log("Hello from my plugin!");
}

function onUnload(api) {
    api.log("Goodbye!");
}
```

本地验证可打包 ZIP 后通过插件中心的「从 ZIP 安装」导入。不要把主程序仓库
中的 `repo/plugins` 当作开发或发布目录；它不再参与插件版本检查。

---

## 插件结构

```
my-plugin/
├── plugin.json        # 插件清单（必需）
├── main.js            # 入口文件（必需）
├── README.md          # 说明文档（推荐）
├── settings_ui.json   # 设置界面定义（可选）
├── icon.png           # 插件图标（可选，64x64）
├── config.json        # 用户配置（自动生成）
└── storage/           # 数据存储目录（自动生成）
```

### plugin.json 字段说明

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| id | string | ✅ | 插件唯一标识符（小写字母和连字符） |
| name | string | ✅ | 插件显示名称 |
| version | string | ✅ | 版本号（语义化版本） |
| main | string | ✅ | 入口文件路径 |
| description | string | ❌ | 插件描述 |
| author | string | ❌ | 作者 |
| homepage | string | ❌ | 主页/仓库地址 |
| permissions | string[] | ❌ | 所需权限列表 |
| profiles | string[] | ❌ | 推荐的 Profile 列表 |
| defaultConfig | object | ❌ | 默认配置 |

---

## 生命周期

```
┌─────────────┐
│  加载插件   │
└──────┬──────┘
       ▼
┌─────────────┐
│  onLoad()   │  ← 初始化、注册监听器
└──────┬──────┘
       ▼
┌─────────────┐
│   运行中    │  ← 响应事件、执行逻辑
└──────┬──────┘
       ▼
┌─────────────┐
│ onUnload()  │  ← 清理资源、移除监听器
└──────┬──────┘
       ▼
┌─────────────┐
│  插件卸载   │
└─────────────┘
```

### onLoad(api)

插件加载时调用，用于初始化。

```javascript
function onLoad(api) {
    api.log("插件已加载");
    
    // 注册事件监听
    api.subtitle.onChanged(function(subtitle) {
        if (subtitle) {
            api.log("当前字幕: " + subtitle.content);
        }
    });
}
```

### onUnload(api)

插件卸载时调用，用于清理资源。

```javascript
function onUnload(api) {
    api.subtitle.removeAllListeners();
    api.overlay.hide();
    api.overlay.clear();
    api.log("插件已卸载");
}
```

> **注意**：即使 `onUnload` 中没有清理代码，系统也会自动清理所有资源，防止内存泄漏。

---

## 权限系统

插件需要在 `plugin.json` 中声明所需权限：

| 权限 | 说明 | API |
|------|------|-----|
| `subtitle` | 访问视频字幕 | api.subtitle |
| `overlay` | 显示覆盖层 | api.overlay |
| `player` | 控制视频播放 | api.player |
| `window` | 控制窗口状态 | api.window |
| `storage` | 数据持久化 | api.storage |
| `network` | HTTP 网络请求 | api.http |
| `events` | 应用事件监听 | api.event |
| `audio` | 语音识别 API | api.audio |

**无需权限的 API：**
- `api.core` - 日志、版本信息
- `api.config` - 插件配置读写
- `api.profile` - Profile 信息（只读）
- `api.log()` - 日志快捷方法

### 权限声明示例

```json
{
  "permissions": ["subtitle", "overlay"]
}
```

---

## 设置界面

通过 `settings_ui.json` 定义可视化设置界面。

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
    { "value": "text", "label": "文字" }
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
          "type": "number",
          "key": "duration",
          "label": "显示时长(毫秒)",
          "default": 3000,
          "min": 0,
          "max": 10000
        }
      ]
    },
    {
      "title": "覆盖层设置",
      "items": [
        {
          "type": "slider",
          "key": "overlay.opacity",
          "label": "透明度",
          "default": 0.8,
          "min": 0.1,
          "max": 1.0
        },
        {
          "type": "button",
          "label": "调整位置",
          "action": "enterEditMode"
        }
      ]
    }
  ]
}
```

---

## 调试技巧

### 使用日志

```javascript
log.info("普通日志");
log.warn("警告日志");
log.error("错误日志");
log.debug("调试日志");

// 支持参数化模板
log.info("用户 {Name} 执行了 {Action}", "Alice", "登录");
```

在应用的日志窗口（菜单 → 日志）中查看输出。

### 热重载

修改插件代码后，在插件中心点击「重载」按钮即可重新加载，无需重启应用。

### 常见错误

1. **权限不足**：检查 `plugin.json` 中是否声明了所需权限
2. **语法错误**：检查 JavaScript 语法，V8 引擎支持 ES6+
3. **API 不存在**：确认 API 名称拼写正确

---

## 最佳实践

### 1. 始终在 onUnload 中清理资源

```javascript
function onUnload(api) {
    api.subtitle.removeAllListeners();
    api.overlay.hide();
    api.overlay.clear();
}
```

### 2. 使用配置而非硬编码

```javascript
// ❌ 不推荐
var x = 43;

// ✅ 推荐
var x = api.config.get("overlay.x", 43);
```

### 3. 处理 API 不可用的情况

```javascript
if (!api.subtitle) {
    api.log("警告：字幕 API 不可用，请检查权限");
    return;
}
```

### 4. 使用有意义的日志

```javascript
api.log("插件名 v1.0.0 已加载");
api.log("识别到方向: " + direction + " (字幕: " + text + ")");
```

### 5. 只申请必要的权限

不要申请用不到的权限，这会影响用户信任度。

---

## 相关文档

- [API 参考](api/README.md)
- [插件发布指南](plugin-marketplace.md)
- [Profile 创建指南](profile-guide.md)
