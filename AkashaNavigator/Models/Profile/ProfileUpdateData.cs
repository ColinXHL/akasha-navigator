namespace AkashaNavigator.Models.Profile
{
/// <summary>
/// Profile 更新数据传输对象
/// 用于传递部分更新数据到 ProfileManager
/// </summary>
public class ProfileUpdateData
{
    /// <summary>
    /// 新名称（null 表示不更新）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 新图标（null 表示不更新）
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 默认设置（null 表示不更新）
    /// </summary>
    public ProfileDefaults? Defaults { get; set; }

    /// <summary>
    /// 鼠标检测配置（null 表示不更新）
    /// </summary>
    public CursorDetectionConfig? CursorDetection { get; set; }

    /// <summary>
    /// 是否清除鼠标检测配置（设为 true 时将 CursorDetection 设为 null）
    /// 当用户禁用 Profile 级别的鼠标检测覆盖时使用
    /// </summary>
    public bool ClearCursorDetection { get; set; }
}
}
