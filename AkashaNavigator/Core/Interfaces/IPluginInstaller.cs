using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 统一处理聚合仓库和本地 ZIP 插件安装。
/// </summary>
public interface IPluginInstaller
{
    Result<InstalledPluginInfo> InstallOrUpdateRepositoryPlugin(
        string pluginId);

    Result<InstalledPluginInfo> InstallPackage(string archivePath);
}
