# 原神 Profile

专为《原神》游戏优化的配置方案，帮助玩家在观看攻略视频时获得最佳体验。

## 基本信息

| 属性 | 值 |
|------|-----|
| ID | `genshin` |
| 版本 | 1.0.0 |
| 作者 | AkashaNavigator |
| 图标 | 🎮 |
| 目标游戏 | 原神 |

## 功能特点

- 自动检测原神进程，自动切换到此 Profile
- 智能鼠标检测：游戏内鼠标隐藏时自动开启点击穿透
- 方向标记插件：识别攻略视频中的方向指示

## 包含插件

| 插件 | 说明 |
|------|------|
| `genshin-direction-marker` | 方向标记插件，识别视频中的方向指示并在覆盖层显示 |
| `smart-cursor-detection` | 智能鼠标检测，根据游戏内鼠标状态自动切换点击穿透 |

## 默认设置

- 默认 URL: `https://www.bilibili.com`
- 自动切换: 开启
- 进程匹配: `GenshinImpact.exe`, `YuanShen.exe`

## 插件预设配置

### smart-cursor-detection

```json
{
  "processWhitelist": "YuanShen, GenshinImpact",
  "minOpacity": 0.2
}
```

- `processWhitelist`: 仅在原神进程前台时启用智能检测
- `minOpacity`: 点击穿透模式下的最低透明度

## 使用说明

1. 安装此 Profile 后，启动原神游戏
2. 程序会自动检测到原神进程并切换到此配置
3. 在游戏中隐藏鼠标时，播放器自动进入点击穿透模式
4. 显示鼠标时，可正常与播放器交互

## 适用场景

- 观看原神攻略视频
- 跟随视频完成任务、解谜
- 查看圣遗物/武器推荐
