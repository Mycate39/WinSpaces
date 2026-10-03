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
    private static string? _lastLevel;
    private static string? _lastMessage;
    private static int _repeatCount;
    private static string? _logPath;
    private static bool _debugEnabled = false;

    public static bool IsDebugEnabled => _debugEnabled;

    public static void SetDebugEnabled(bool enabled) => _debugEnabled = enabled;

    private static string LogPath
        => _logPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinSpaces",
            "winspaces.log");

    /// <summary>Chemin du journal courant.</summary>
    public static string FilePath => LogPath;

    /// <summary>Chemin de l'archive créée par la rotation (au-delà de 5 Mo).</summary>
    public static string ArchivePath => LogPath + ".old";

    /// <summary>Dossier contenant les journaux.</summary>
    public static string DirectoryPath => Path.GetDirectoryName(LogPath)!;

    /// <summary>Vide le journal courant (l'archive est conservée).</summary>
    public static void Clear()
    {
        try
        {
            lock (Gate)
            {
                if (File.Exists(LogPath))
                    File.WriteAllText(LogPath, string.Empty);
            }
        }
        catch
        {
            // Le journal ne doit jamais faire tomber l'application.
        }
    }

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
                // Message identique au précédent : on compte au lieu d'écrire, pour
                // qu'une boucle ne puisse plus noyer le journal sous des milliers de lignes.
                if (level == _lastLevel && message == _lastMessage)
                {
                    _repeatCount++;
                    return;
                }

                RotateIfNeeded();
                var now = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                var text = _repeatCount > 0
                    ? $"[{now}] [{_lastLevel}] (message précédent répété {_repeatCount} fois){Environment.NewLine}"
                    : string.Empty;
                File.AppendAllText(LogPath, text + $"[{now}] [{level}] {message}{Environment.NewLine}");

                _lastLevel = level;
                _lastMessage = message;
                _repeatCount = 0;
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
            File.Move(LogPath, ArchivePath, overwrite: true);
    }
}
