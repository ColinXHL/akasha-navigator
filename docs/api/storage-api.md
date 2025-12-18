# Storage API

需要 `storage` 权限。提供数据持久化功能。

数据存储在 `{插件目录}/storage/{key}.json`。

## api.storage.save(key, data)

保存数据。

```javascript
api.storage.save("settings", { theme: "dark", fontSize: 14 });
api.storage.save("history", [1, 2, 3]);
```

## api.storage.load(key)

加载数据。

```javascript
var settings = api.storage.load("settings");
if (settings) {
    api.log("主题: " + settings.theme);
}
```

## api.storage.delete(key)

删除数据。

```javascript
api.storage.delete("oldData");
```

## api.storage.exists(key)

检查数据是否存在。

```javascript
if (api.storage.exists("cache")) {
    // ...
}
```

## api.storage.list()

列出所有存储的键名。

```javascript
var keys = api.storage.list();
keys.forEach(function(key) {
    api.log("存储键: " + key);
});
```
