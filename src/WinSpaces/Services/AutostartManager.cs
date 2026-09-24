using System.IO;
using Microsoft.Win32;

namespace WinSpaces.Services;

/// <summary>
/// Gestion du lancement automatique via la clé de registre
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
internal static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinSpaces";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exe = Path.Combine(AppContext.BaseDirectory, "WinSpaces.exe");
                key.SetValue(ValueName, $"\"{exe}\" --autostart");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}