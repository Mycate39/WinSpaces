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
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private static int _writesSinceSizeCheck;
    private static string? _logPath;
    private static bool _debugEnabled = false;

    public static bool IsDebugEnabled => _debugEnabled;

    public static void SetDebugEnabled(bool enabled) => _debugEnabled = enabled;

    private static string LogPath
        => _logPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinSpaces",
            "winspaces.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Error(Exception ex) => Write("ERROR", ex.ToString());

    public static void Debug(string message) => Write("DEBUG", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (dir is null) return;
                Directory.CreateDirectory(dir);
                RotateIfNeeded();
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Le journal ne doit jamais faire tomber l'application.
        }
    }

    /// <summary>
    /// Le journal grossissait sans limite (surtout en debug, une ligne par
    /// WM_INPUT) : au-delà de 5 Mo il est renommé en .old. Vérifié toutes les
    /// 200 écritures pour ne pas interroger le disque à chaque ligne.
    /// </summary>
    private static void RotateIfNeeded()
    {
        if (++_writesSinceSizeCheck < 200) return;
        _writesSinceSizeCheck = 0;

        var info = new FileInfo(LogPath);
        if (info.Exists && info.Length > MaxLogBytes)
            File.Move(LogPath, LogPath + ".old", overwrite: true);
    }
}
