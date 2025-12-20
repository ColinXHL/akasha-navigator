using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// AboutWindow - 关于窗口
/// </summary>
public partial class AboutWindow : AnimatedWindow
{
    private const string GitHubUrl = "https://github.com/ColinXHL/akasha-navigator";

    /// <summary>
    /// 版本号（从程序集读取，包含完整语义化版本）
    /// </summary>
    public string VersionText { get; }

    public AboutWindow()
    {
        // 使用 InformationalVersion 获取完整版本号（包含 alpha/beta 等后缀）
        var infoVersion = Assembly.GetExecutingAssembly()
                              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion;

        if (!string.IsNullOrEmpty(infoVersion))
        {
            // 移除可能的 +commitHash 后缀
            var plusIndex = infoVersion.IndexOf('+');
            VersionText = plusIndex > 0 ? $"v{infoVersion[..plusIndex]}" : $"v{infoVersion}";
        }
        else
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v0.0.0";
        }

        DataContext = this;
        InitializeComponent();
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 关闭按钮
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    /// <summary>
    /// 打开 GitHub 仓库
    /// </summary>
    private void BtnGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = GitHubUrl, UseShellExecute = true });
        }
        catch
        {
            // 忽略打开链接失败
        }
    }
}
}
