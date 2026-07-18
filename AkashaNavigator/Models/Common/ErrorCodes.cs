namespace AkashaNavigator.Models.Common
{
/// <summary>
/// 插件相关错误码
/// </summary>
public static class PluginErrorCodes
{
    /// <summary>
    /// 插件已安装
    /// </summary>
    public const string AlreadyInstalled = "PLUGIN_ALREADY_INSTALLED";

    /// <summary>
    /// 源目录不存在
    /// </summary>
    public const string SourceNotFound = "PLUGIN_SOURCE_NOT_FOUND";

    /// <summary>
    /// 清单无效
    /// </summary>
    public const string InvalidManifest = "PLUGIN_INVALID_MANIFEST";

    /// <summary>
    /// 文件复制失败
    /// </summary>
    public const string CopyFailed = "PLUGIN_COPY_FAILED";

    /// <summary>
    /// 插件未安装
    /// </summary>
    public const string NotInstalled = "PLUGIN_NOT_INSTALLED";

    /// <summary>
    /// 插件被引用
    /// </summary>
    public const string HasReferences = "PLUGIN_HAS_REFERENCES";

    /// <summary>
    /// 文件删除失败
    /// </summary>
    public const string DeleteFailed = "PLUGIN_DELETE_FAILED";

    /// <summary>
    /// 用户拒绝高风险权限
    /// </summary>
    public const string PermissionConsentDeclined = "PLUGIN_PERMISSION_CONSENT_DECLINED";

    /// <summary>
    /// 插件压缩包无效或不安全
    /// </summary>
    public const string InvalidPackage = "PLUGIN_INVALID_PACKAGE";

    /// <summary>
    /// 插件压缩包版本不高于已安装版本
    /// </summary>
    public const string VersionNotNewer = "PLUGIN_VERSION_NOT_NEWER";

    /// <summary>
    /// 远程目录中没有可下载的插件包。
    /// </summary>
    public const string RemotePackageNotFound = "PLUGIN_REMOTE_PACKAGE_NOT_FOUND";

    /// <summary>
    /// 当前宿主版本低于插件要求。
    /// </summary>
    public const string HostVersionTooLow = "PLUGIN_HOST_VERSION_TOO_LOW";

    /// <summary>
    /// 所有远程下载源均失败。
    /// </summary>
    public const string RemoteDownloadFailed = "PLUGIN_REMOTE_DOWNLOAD_FAILED";

    /// <summary>
    /// 用户取消远程插件下载。
    /// </summary>
    public const string RemoteDownloadCanceled = "PLUGIN_REMOTE_DOWNLOAD_CANCELED";

    public const string RepositoryPluginNotFound = "PLUGIN_REPOSITORY_NOT_FOUND";

    public const string RepositoryManifestInvalid = "PLUGIN_REPOSITORY_MANIFEST_INVALID";

    public const string DistributionUnsupported = "PLUGIN_DISTRIBUTION_UNSUPPORTED";

    public const string InstallTransactionFailed = "PLUGIN_INSTALL_TRANSACTION_FAILED";
}

/// <summary>
/// Profile 相关错误码
/// </summary>
public static class ProfileErrorCodes
{
    /// <summary>
    /// Profile 名称为空
    /// </summary>
    public const string NameEmpty = "PROFILE_NAME_EMPTY";

    /// <summary>
    /// Profile 已存在
    /// </summary>
    public const string AlreadyExists = "PROFILE_ALREADY_EXISTS";

    /// <summary>
    /// Profile 不存在
    /// </summary>
    public const string NotFound = "PROFILE_NOT_FOUND";

    /// <summary>
    /// 不能删除默认 Profile
    /// </summary>
    public const string IsDefault = "PROFILE_IS_DEFAULT";

    /// <summary>
    /// 删除失败
    /// </summary>
    public const string DeleteFailed = "PROFILE_DELETE_FAILED";
}
}
