namespace AkashaNavigator.Helpers
{
/// <summary>
/// 窗口可见性控制器
/// 封装窗口可见性切换逻辑，便于测试
/// </summary>
public class WindowVisibilityController
{
    private bool _isHidden;

    /// <summary>
    /// 窗口是否隐藏
    /// </summary>
    public bool IsHidden => _isHidden;

    /// <summary>
    /// 窗口是否显示
    /// </summary>
    public bool IsVisible => !_isHidden;

    /// <summary>
    /// 切换窗口可见性
    /// </summary>
    /// <returns>切换后的可见性状态（true = 显示, false = 隐藏）</returns>
    public bool ToggleVisibility()
    {
        _isHidden = !_isHidden;
        return IsVisible;
    }

    /// <summary>
    /// 显示窗口
    /// </summary>
    public void Show()
    {
        _isHidden = false;
    }

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    public void Hide()
    {
        _isHidden = true;
    }
}
}
