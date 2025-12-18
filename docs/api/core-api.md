# Core API

无需权限，提供基础功能。

## api.core.version

获取主程序版本号。

```javascript
var version = api.core.version;
api.log("主程序版本: " + version);
```

## api.core.log(message)

输出普通日志。

```javascript
api.core.log("这是一条日志");
// 或使用快捷方法
api.log("这是一条日志");
```

## api.core.warn(message)

输出警告日志。

```javascript
api.core.warn("这是一条警告");
```

## api.core.error(message)

输出错误日志。

```javascript
api.core.error("这是一条错误");
```
