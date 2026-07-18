using System;
using System.IO;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 已校验的临时插件包。释放对象时删除临时文件。
/// </summary>
public sealed class DownloadedPluginPackage : IDisposable
{
    public DownloadedPluginPackage(string filePath, string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        FilePath = Path.GetFullPath(filePath);
        SourceId = sourceId;
    }

    public string FilePath { get; }

    public string SourceId { get; }

    public void Dispose()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // 临时下载文件由系统后续清理。
        }
    }
}
