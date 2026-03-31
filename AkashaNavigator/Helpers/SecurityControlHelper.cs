using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AkashaNavigator.Helpers;

public static class SecurityControlHelper
{
    public static void AllowFullFolderSecurity(string folderPath)
    {
        if (!IsElevated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        var info = new DirectoryInfo(folderPath);
        var access = info.GetAccessControl(AccessControlSections.All);
        var inherits = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        var everyoneRule = new FileSystemAccessRule(
            "Everyone",
            FileSystemRights.FullControl,
            inherits,
            PropagationFlags.None,
            AccessControlType.Allow);

        var usersRule = new FileSystemAccessRule(
            "Users",
            FileSystemRights.FullControl,
            inherits,
            PropagationFlags.None,
            AccessControlType.Allow);

        access.ModifyAccessRule(AccessControlModification.Add, everyoneRule, out _);
        access.ModifyAccessRule(AccessControlModification.Add, usersRule, out _);
        info.SetAccessControl(access);
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
