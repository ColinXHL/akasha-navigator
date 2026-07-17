using System;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 单个下载源的测速结果。
/// </summary>
public sealed record DownloadSourceMeasurement(
    DownloadSourceInfo Source,
    bool IsSuccess,
    long BytesRead,
    double BytesPerSecond,
    TimeSpan TimeToFirstByte,
    TimeSpan EstimatedDownloadTime,
    string ErrorMessage);
