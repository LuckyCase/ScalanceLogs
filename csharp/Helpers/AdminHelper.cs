using System.Security.Principal;

namespace SyslogViewer.Helpers;

public static class AdminHelper
{
    public static bool IsAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

    public static bool NeedsAdmin(int port) => port < 1024;
}
