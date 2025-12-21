# Core API

无需权限，提供基础功能。

## api.core.version

获取主程序版本号。

```javascript
var version = api.core.version;
log.info("主程序版本: " + version);
```

## api.core.logger

日志代理对象，提供 debug/info/warn/error 方法。

### logger.debug(message, ...args)

输出调试日志。

```javascript
api.core.logger.debug("调试信息");
api.core.logger.debug("用户 {Name} 登录", "Alice");
```

### logger.info(message, ...args)

输出普通日志。

```javascript
api.core.logger.info("这是一条日志");
api.core.logger.info("处理了 {Count} 个项目", 10);
```

### logger.warn(message, ...args)

输出警告日志。

```javascript
api.core.logger.warn("这是一条警告");
api.core.logger.warn("配置项 {Key} 未找到", "theme");
```

### logger.error(message, ...args)

输出错误日志。

```javascript
api.core.logger.error("这是一条错误");
api.core.logger.error("操作失败: {Reason}", "网络超时");
```

## 全局快捷方法

插件系统提供了全局的 `log` 对象，可以直接使用：

```javascript
log.debug("调试信息");
log.info("普通日志");
log.warn("警告日志");
log.error("错误日志");
```

这等同于使用 `api.core.logger`。
