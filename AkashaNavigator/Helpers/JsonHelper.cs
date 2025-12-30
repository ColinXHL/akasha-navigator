using System;
using System.IO;
using System.Text.Json;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Helpers
{
    /// <summary>
    /// JSON 序列化辅助类
    /// 提供统一的 JSON 序列化/反序列化配置和便捷方法
    /// 文件 I/O 操作返回 Result 类型以提供统一的错误处理
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 用于读取的选项（大小写不敏感）
        /// </summary>
        public static JsonSerializerOptions ReadOptions { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 用于写入的选项（缩进格式）
        /// </summary>
        public static JsonSerializerOptions WriteOptions { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// 反序列化 JSON 字符串
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="json">JSON 字符串</param>
        /// <returns>反序列化后的对象，失败返回 default</returns>
        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, ReadOptions);
        }

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <returns>JSON 字符串</returns>
        public static string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, WriteOptions);
        }

        /// <summary>
        /// 从文件加载并反序列化 JSON
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化后的对象，失败返回 Result</returns>
        public static Result<T> LoadFromFile<T>(string filePath)
        {
            // 验证路径
            if (string.IsNullOrWhiteSpace(filePath))
                return Result<T>.Failure(Error.Validation("INVALID_PATH", "文件路径为空"));

            // 检查文件是否存在
            if (!File.Exists(filePath))
                return Result<T>.Failure(Error.FileSystem("FILE_NOT_FOUND", $"文件不存在: {filePath}", filePath: filePath));

            try
            {
                var json = File.ReadAllText(filePath);
                var result = Deserialize<T>(json);

                if (result == null)
                    return Result<T>.Failure(Error.Serialization("DESERIALIZATION_NULL", $"反序列化结果为 null: {filePath}"));

                return Result<T>.Success(result);
            }
            catch (JsonException ex)
            {
                return Result<T>.Failure(Error.Serialization("JSON_PARSE_ERROR", $"JSON 解析错误: {ex.Message}", ex));
            }
            catch (IOException ex)
            {
                return Result<T>.Failure(Error.FileSystem("IO_READ_ERROR", $"读取文件失败: {ex.Message}", ex, filePath));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<T>.Failure(Error.Permission("READ_ACCESS_DENIED", $"无权限读取文件: {filePath}", $"无法读取文件 {Path.GetFileName(filePath)}", ex));
            }
        }

        /// <summary>
        /// 序列化对象并保存到文件
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="obj">要保存的对象</param>
        /// <returns>保存结果</returns>
        public static Result SaveToFile<T>(string filePath, T obj)
        {
            // 验证路径
            if (string.IsNullOrWhiteSpace(filePath))
                return Result.Failure(Error.Validation("INVALID_PATH", "文件路径为空"));

            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化并写入文件
                var json = Serialize(obj);
                File.WriteAllText(filePath, json);
                return Result.Success();
            }
            catch (IOException ex)
            {
                return Result.Failure(Error.FileSystem("IO_WRITE_ERROR", $"写入文件失败: {ex.Message}", ex, filePath));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure(Error.Permission("WRITE_ACCESS_DENIED", $"无权限写入文件: {filePath}", $"无法写入文件 {Path.GetFileName(filePath)}", ex));
            }
        }
    }
}
