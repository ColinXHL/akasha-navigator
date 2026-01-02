namespace AkashaNavigator.Models.PioneerNote
{
    /// <summary>
    /// 目录列表项（用于显示）
    /// </summary>
    public class FolderItem
    {
        /// <summary>
        /// 目录 ID（null 表示根目录）
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; } = "📁";

        /// <summary>
        /// 缩进级别
        /// </summary>
        public int Indent { get; set; }
    }
}
