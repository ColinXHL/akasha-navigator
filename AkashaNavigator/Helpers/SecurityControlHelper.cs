using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Serilog;

namespace AkashaNavigator.Helpers;

public static class SecurityControlHelper
{
    public static bool AllowFullFolderSecurity(string folderPath)
    {
        if (!IsElevated())
        {
            Log.Warning("[{Source}] Skip ACL update: process is not elevated", nameof(SecurityControlHelper));
            return false;
        }

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            Log.Warning("[{Source}] Skip ACL update: invalid or missing folder {FolderPath}", nameof(SecurityControlHelper),
                        folderPath);
            return false;
        }

        try
        {
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

            Log.Information("[{Source}] ACL updated for folder {FolderPath}", nameof(SecurityControlHelper), folderPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}] ACL update failed for folder {FolderPath}", nameof(SecurityControlHelper),
                      folderPath);
            return false;
        }
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool HasExpectedFolderSecurity(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        try
        {
            var info = new DirectoryInfo(folderPath);
            var access = info.GetAccessControl(AccessControlSections.Access);
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            return HasAllowFullControlRule(access, everyoneSid) && HasAllowFullControlRule(access, usersSid);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{Source}] Failed to inspect ACL for folder {FolderPath}", nameof(SecurityControlHelper),
                        folderPath);
            return false;
        }
    }

    private static bool HasAllowFullControlRule(DirectorySecurity access, SecurityIdentifier expectedSid)
    {
        var rules = access.GetAccessRules(true, true, typeof(SecurityIdentifier))
                          .OfType<FileSystemAccessRule>();

        return rules.Any(rule =>
            rule.AccessControlType == AccessControlType.Allow &&
            rule.IdentityReference is SecurityIdentifier sid &&
            sid.Equals(expectedSid) &&
            (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);
    }
}
