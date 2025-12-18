# HTTP API

需要 `network` 权限。提供 HTTP 网络请求功能。

## api.http.get(url, options)

发起 GET 请求。

```javascript
var result = api.http.get("https://api.example.com/data", {
    headers: { "Authorization": "Bearer token" },
    timeout: 10000
});

if (result.success) {
    api.log("状态码: " + result.status);
    api.log("数据: " + result.data);
} else {
    api.log("错误: " + result.error);
}
```

## api.http.post(url, body, options)

发起 POST 请求。

```javascript
var result = api.http.post("https://api.example.com/submit", 
    { name: "test", value: 123 },
    {
        headers: { "Content-Type": "application/json" },
        timeout: 10000
    }
);
```

## 返回值

```javascript
{
    success: true,           // 请求是否成功
    status: 200,             // HTTP 状态码
    data: "响应内容",         // 响应体
    headers: { ... },        // 响应头
    error: null              // 错误信息（失败时）
}
```

## 注意事项

- `network` 权限是高敏感度权限，安装时会显示警告
- 请在插件说明中解释为何需要网络权限
- 建议设置合理的 timeout 避免请求阻塞
