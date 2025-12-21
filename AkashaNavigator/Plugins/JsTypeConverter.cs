using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// JavaScript 类型转换工具类
/// 集中处理 C# 与 JavaScript 之间的类型转换
/// </summary>
public static class JsTypeConverter
{
#region C #to JavaScript Conversion

    /// <summary>
    /// 将 C# 对象转换为 JavaScript 友好的对象
    /// </summary>
    /// <param name="value">C# 对象</param>
    /// <param name="engine">V8 脚本引擎（可选，用于创建原生 JS 对象）</param>
    /// <returns>JavaScript 友好的对象</returns>
    public static object? ToJs(object? value, V8ScriptEngine? engine = null)
    {
        if (value == null)
            return null;

        var type = value.GetType();

        // 处理基本类型 - 直接返回
        if (IsPrimitiveType(type))
            return value;

        // 处理字符串
        if (value is string)
            return value;

        // 处理 DateTime
        if (value is DateTime dt)
            return dt.ToString("O"); // ISO 8601 格式

        // 处理 Guid
        if (value is Guid guid)
            return guid.ToString();

        // 处理 JsonElement（从 System.Text.Json 反序列化的结果）
        if (value is JsonElement jsonElement)
            return ConvertJsonElement(jsonElement, engine);

        // 处理字典类型
        if (value is IDictionary dict)
            return ConvertDictionaryToJs(dict, engine);

        // 处理数组和列表
        if (value is IEnumerable enumerable && !(value is string))
            return ConvertEnumerableToJs(enumerable, engine);

        // 处理复杂对象 - 使用反射转换为字典
        return ConvertObjectToJs(value, engine);
    }

    /// <summary>
    /// 创建 JavaScript 原生对象
    /// </summary>
    /// <param name="engine">V8 脚本引擎</param>
    /// <returns>JavaScript 对象</returns>
    public static dynamic? CreateJsObject(V8ScriptEngine engine)
    {
        if (engine == null)
            return null;

        try
        {
            return engine.Evaluate("({})");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 创建 JavaScript 原生数组
    /// </summary>
    /// <param name="engine">V8 脚本引擎</param>
    /// <returns>JavaScript 数组</returns>
    public static dynamic? CreateJsArray(V8ScriptEngine engine)
    {
        if (engine == null)
            return null;

        try
        {
            return engine.Evaluate("[]");
        }
        catch
        {
            return null;
        }
    }

#endregion

#region JavaScript to C #Conversion

    /// <summary>
    /// 将 JavaScript 对象转换为指定的 C# 类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsValue">JavaScript 值</param>
    /// <returns>转换后的 C# 对象</returns>
    public static T? FromJs<T>(object? jsValue)
    {
        if (jsValue == null || jsValue is Undefined)
            return default;

        var targetType = typeof(T);

        // 如果已经是目标类型，直接返回
        if (jsValue is T typedValue)
            return typedValue;

        // 处理可空类型
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // 处理基本类型转换
            if (underlyingType == typeof(int))
                return (T)(object)Convert.ToInt32(jsValue);
            if (underlyingType == typeof(long))
                return (T)(object)Convert.ToInt64(jsValue);
            if (underlyingType == typeof(double))
                return (T)(object)Convert.ToDouble(jsValue);
            if (underlyingType == typeof(float))
                return (T)(object)Convert.ToSingle(jsValue);
            if (underlyingType == typeof(decimal))
                return (T)(object)Convert.ToDecimal(jsValue);
            if (underlyingType == typeof(bool))
                return (T)(object)Convert.ToBoolean(jsValue);
            if (underlyingType == typeof(string))
                return (T)(object)(jsValue?.ToString() ?? string.Empty);

            // 处理字典类型
            if (targetType == typeof(Dictionary<string, object?>) || targetType == typeof(IDictionary<string, object?>))
            {
                var dict = ToDictionary(jsValue);
                return (T)(object)dict;
            }

            // 处理数组类型
            if (targetType.IsArray)
            {
                return ConvertToArray<T>(jsValue);
            }

            // 处理 List 类型
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return ConvertToList<T>(jsValue);
            }

            // 尝试直接转换
            return (T)Convert.ChangeType(jsValue, underlyingType);
        }
        catch (Exception ex)
        {
            Services.LogService.Instance.Warn("JsTypeConverter", "FromJs<{TypeName}> failed: {ErrorMessage}",
                                              typeof(T).Name, ex.Message);
            return default;
        }
    }

    /// <summary>
    /// 将 JavaScript 对象转换为字典
    /// </summary>
    /// <param name="jsObject">JavaScript 对象</param>
    /// <returns>字典，如果转换失败返回空字典</returns>
    public static Dictionary<string, object?> ToDictionary(object? jsObject)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (jsObject == null || jsObject is Undefined)
            return result;

        // 如果已经是字典类型
        if (jsObject is Dictionary<string, object?> dict)
            return dict;

        if (jsObject is IDictionary<string, object> idict)
        {
            foreach (var kvp in idict)
            {
                result[kvp.Key] = ConvertJsValue(kvp.Value);
            }
            return result;
        }

        if (jsObject is IDictionary genericDict)
        {
            foreach (DictionaryEntry entry in genericDict)
            {
                var key = entry.Key?.ToString();
                if (key != null)
                {
                    result[key] = ConvertJsValue(entry.Value);
                }
            }
            return result;
        }

        // 处理 ClearScript 的 ScriptObject
        if (jsObject is ScriptObject scriptObj)
        {
            return ConvertScriptObjectToDictionary(scriptObj);
        }

        // 处理 PropertyBag（ClearScript 的动态属性包，实现了 IDictionary<string, object>）
        if (jsObject is PropertyBag propertyBag)
        {
            foreach (var kvp in propertyBag)
            {
                result[kvp.Key] = ConvertJsValue(kvp.Value);
            }
            return result;
        }

        // 处理动态对象（ClearScript 返回的 JavaScript 对象）
        var type = jsObject.GetType();
        if (type.FullName?.Contains("ClearScript") == true || type.FullName?.Contains("V8") == true)
        {
            return ConvertDynamicObjectToDictionary(jsObject);
        }

        // 尝试使用反射获取属性
        try
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(jsObject);
                    result[prop.Name] = ConvertJsValue(value);
                }
            }
        }
        catch
        {
            // 忽略反射错误
        }

        return result;
    }

    /// <summary>
    /// 尝试从 JavaScript 对象获取指定属性
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="jsObject">JavaScript 对象</param>
    /// <param name="key">属性名</param>
    /// <param name="value">输出值</param>
    /// <returns>是否成功获取</returns>
    public static bool TryGetProperty<T>(object? jsObject, string key, out T? value)
    {
        value = default;

        if (jsObject == null || string.IsNullOrEmpty(key))
            return false;

        try
        {
            var dict = ToDictionary(jsObject);
            if (dict.TryGetValue(key, out var rawValue))
            {
                value = FromJs<T>(rawValue);
                return true;
            }
        }
        catch
        {
            // 忽略错误
        }

        return false;
    }

#endregion

#region Private Helper Methods

    /// <summary>
    /// 检查是否为基本类型
    /// </summary>
    private static bool IsPrimitiveType(Type type)
    {
        // 注意：DateTime 和 Guid 不在这里，因为它们需要特殊处理
        return type.IsPrimitive || type == typeof(decimal);
    }

    /// <summary>
    /// 转换 JsonElement 为适当的类型
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element, V8ScriptEngine? engine)
    {
        switch (element.ValueKind)
        {
        case JsonValueKind.Null:
        case JsonValueKind.Undefined:
            return null;
        case JsonValueKind.True:
            return true;
        case JsonValueKind.False:
            return false;
        case JsonValueKind.Number:
            if (element.TryGetInt32(out var intVal))
                return intVal;
            if (element.TryGetInt64(out var longVal))
                return longVal;
            return element.GetDouble();
        case JsonValueKind.String:
            return element.GetString();
        case JsonValueKind.Array:
            var list = new List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                list.Add(ConvertJsonElement(item, engine));
            }
            return list;
        case JsonValueKind.Object:
            var dict = new Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = ConvertJsonElement(prop.Value, engine);
            }
            return dict;
        default:
            return element.ToString();
        }
    }

    /// <summary>
    /// 将字典转换为 JavaScript 对象
    /// </summary>
    private static object ConvertDictionaryToJs(IDictionary dict, V8ScriptEngine? engine)
    {
        var result = new Dictionary<string, object?>();
        foreach (DictionaryEntry entry in dict)
        {
            var key = entry.Key?.ToString();
            if (key != null)
            {
                result[key] = ToJs(entry.Value, engine);
            }
        }
        return result;
    }

    /// <summary>
    /// 将可枚举集合转换为 JavaScript 数组
    /// </summary>
    private static object ConvertEnumerableToJs(IEnumerable enumerable, V8ScriptEngine? engine)
    {
        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            list.Add(ToJs(item, engine));
        }
        return list.ToArray();
    }

    /// <summary>
    /// 将复杂对象转换为 JavaScript 对象（使用反射）
    /// </summary>
    private static object ConvertObjectToJs(object obj, V8ScriptEngine? engine)
    {
        var result = new Dictionary<string, object?>();
        var type = obj.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead)
                continue;

            try
            {
                var value = prop.GetValue(obj);
                // 使用 camelCase 命名
                var jsName = ToCamelCase(prop.Name);
                result[jsName] = ToJs(value, engine);
            }
            catch
            {
                // 忽略无法读取的属性
            }
        }

        return result;
    }

    /// <summary>
    /// 将 ScriptObject 转换为字典
    /// </summary>
    private static Dictionary<string, object?> ConvertScriptObjectToDictionary(ScriptObject scriptObj)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var propertyNames = scriptObj.PropertyNames;
            foreach (var name in propertyNames)
            {
                var value = scriptObj[name];
                result[name] = ConvertJsValue(value);
            }
        }
        catch
        {
            // 忽略错误
        }

        return result;
    }

    /// <summary>
    /// 将动态对象转换为字典（用于 ClearScript 返回的对象）
    /// </summary>
    private static Dictionary<string, object?> ConvertDynamicObjectToDictionary(object dynamicObj)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 尝试作为 dynamic 访问
            dynamic dyn = dynamicObj;

            // 尝试获取属性名列表
            // ClearScript 的 JavaScript 对象通常实现了 IEnumerable<string> 的 PropertyNames
            var type = dynamicObj.GetType();
            var propertyNamesProperty = type.GetProperty("PropertyNames");

            if (propertyNamesProperty != null)
            {
                var names = propertyNamesProperty.GetValue(dynamicObj) as IEnumerable<string>;
                if (names != null)
                {
                    foreach (var name in names)
                    {
                        try
                        {
                            var indexer = type.GetProperty("Item");
                            if (indexer != null)
                            {
                                var value = indexer.GetValue(dynamicObj, new object[] { name });
                                result[name] = ConvertJsValue(value);
                            }
                        }
                        catch
                        {
                            // 忽略单个属性的错误
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略错误
        }

        return result;
    }

    /// <summary>
    /// 转换 JavaScript 值为 C# 值
    /// </summary>
    private static object? ConvertJsValue(object? value)
    {
        if (value == null || value is Undefined)
            return null;

        // 基本类型直接返回
        if (value is string || value is bool || value is int || value is long || value is double || value is float ||
            value is decimal)
            return value;

        // 处理 ScriptObject
        if (value is ScriptObject scriptObj)
        {
            // 检查是否是数组
            if (IsJsArray(scriptObj))
            {
                return ConvertJsArrayToList(scriptObj);
            }
            return ConvertScriptObjectToDictionary(scriptObj);
        }

        // 处理其他 ClearScript 类型
        var type = value.GetType();
        if (type.FullName?.Contains("ClearScript") == true || type.FullName?.Contains("V8") == true)
        {
            return ConvertDynamicObjectToDictionary(value);
        }

        return value;
    }

    /// <summary>
    /// 检查 ScriptObject 是否是数组
    /// </summary>
    private static bool IsJsArray(ScriptObject scriptObj)
    {
        try
        {
            // JavaScript 数组有 length 属性
            var length = scriptObj["length"];
            return length != null && !(length is Undefined);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将 JavaScript 数组转换为 List
    /// </summary>
    private static List<object?> ConvertJsArrayToList(ScriptObject scriptObj)
    {
        var result = new List<object?>();

        try
        {
            var length = Convert.ToInt32(scriptObj["length"]);
            for (var i = 0; i < length; i++)
            {
                var item = scriptObj[i];
                result.Add(ConvertJsValue(item));
            }
        }
        catch
        {
            // 忽略错误
        }

        return result;
    }

    /// <summary>
    /// 将 JavaScript 值转换为数组
    /// </summary>
    private static T? ConvertToArray<T>(object jsValue)
    {
        var elementType = typeof(T).GetElementType();
        if (elementType == null)
            return default;

        var list = new List<object?>();

        if (jsValue is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                list.Add(ConvertJsValue(item));
            }
        }
        else if (jsValue is ScriptObject scriptObj && IsJsArray(scriptObj))
        {
            list = ConvertJsArrayToList(scriptObj);
        }

        var array = Array.CreateInstance(elementType, list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var convertedValue = list[i] != null ? Convert.ChangeType(list[i], elementType) : null;
            array.SetValue(convertedValue, i);
        }

        return (T)(object)array;
    }

    /// <summary>
    /// 将 JavaScript 值转换为 List
    /// </summary>
    private static T? ConvertToList<T>(object jsValue)
    {
        var genericArgs = typeof(T).GetGenericArguments();
        if (genericArgs.Length == 0)
            return default;

        var elementType = genericArgs[0];
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        if (jsValue is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var convertedItem = ConvertJsValue(item);
                if (convertedItem != null)
                {
                    list.Add(Convert.ChangeType(convertedItem, elementType));
                }
                else
                {
                    list.Add(null);
                }
            }
        }
        else if (jsValue is ScriptObject scriptObj && IsJsArray(scriptObj))
        {
            var items = ConvertJsArrayToList(scriptObj);
            foreach (var item in items)
            {
                if (item != null)
                {
                    list.Add(Convert.ChangeType(item, elementType));
                }
                else
                {
                    list.Add(null);
                }
            }
        }

        return (T)list;
    }

    /// <summary>
    /// 将 PascalCase 转换为 camelCase
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        if (char.IsLower(name[0]))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

#endregion
}
}
