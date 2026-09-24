using System.IO;

namespace WinSpaces.Services;

/// <summary>
/// Journal applicatif minimal : écrit dans
/// %LOCALAPPDATA%\WinSpaces\winspaces.log (aucune dépendance externe).
/// Utile en open-source pour diagnostiquer la détection des gestes et les
/// changements de bureau sans console.
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();
    private static string? _logPath;

    private static string LogPath
        => _logPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinSpaces",
            "winspaces.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Error(Exception ex) => Write("ERROR", ex.ToString());

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (dir is null) return;
                Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Le journal ne doit jamais faire tomber l'application.
        }
    }
}
