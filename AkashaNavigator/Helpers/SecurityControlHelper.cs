using System;
using System.IO;
using System.Security.AccessControl;

namespace AkashaNavigator.Helpers;

public static class SecurityControlHelper
{
    public static void AllowFullFolderSecurity(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        var info = new DirectoryInfo(folderPath);
        var access = info.GetAccessControl();
        access.AddAccessRule(
            new FileSystemAccessRule("Everyone", FileSystemRights.FullControl,
                                     InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                     PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(access);
    }
}
