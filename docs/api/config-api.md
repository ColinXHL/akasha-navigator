# Config API

无需权限，管理插件配置。配置自动保存到 `config.json` 文件。

## api.config.get(key, defaultValue)

获取配置值，支持点号路径。

```javascript
var x = api.config.get("overlay.x", 100);
var name = api.config.get("name", "default");
```

## api.config.set(key, value)

设置配置值，自动保存到文件。

```javascript
api.config.set("overlay.x", 200);
api.config.set("enabled", true);
```

## api.config.has(key)

检查配置键是否存在。

```javascript
if (api.config.has("customSetting")) {
    // ...
}
```

## api.config.remove(key)

移除配置键。

```javascript
api.config.remove("oldSetting");
```
