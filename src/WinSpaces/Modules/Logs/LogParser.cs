using System.Globalization;
using System.Text.RegularExpressions;

namespace WinSpaces.Modules.Logs;

/// <summary>
/// Analyse le format d'AppLog : « [yyyy-MM-dd HH:mm:ss] [LEVEL] message ».
/// Les lignes qui ne commencent pas par un en-tête prolongent l'entrée
/// précédente (exceptions sur plusieurs lignes). Le parseur garde cet état
/// entre deux appels, ce qui permet une lecture incrémentale du fichier.
/// </summary>
public sealed class LogParser
{
    private static readonly Regex Header = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] \[(?<lvl>[A-Za-z]+)\] ?(?<msg>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private LogEntry? _current;

    /// <summary>
    /// Traite une ligne. Retourne la nouvelle entrée si la ligne en ouvre une,
    /// ou null si elle a été ajoutée à l'entrée en cours (ou ignorée).
    /// </summary>
    public LogEntry? Feed(string line)
    {
        line = line.TrimEnd('\r');

        var match = Header.Match(line);
        if (match.Success)
        {
            DateTime? timestamp = DateTime.TryParseExact(match.Groups["ts"].Value, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts) ? ts : null;
            _current = new LogEntry(timestamp, ParseLevel(match.Groups["lvl"].Value), match.Groups["msg"].Value);
            return _current;
        }

        if (line.Length == 0) return null;

        if (_current is null)
        {
            // Texte sans en-tête en début de fichier : on le garde tel quel.
            _current = new LogEntry(null, LogLevelKind.Info, line);
            return _current;
        }

        _current.AppendLine(line);
        return null;
    }

    public static LogLevelKind ParseLevel(string level) => level.ToUpperInvariant() switch
    {
        "ERROR" or "ERR" or "FATAL" => LogLevelKind.Error,
        "WARN" or "WARNING" => LogLevelKind.Warning,
        "DEBUG" or "TRACE" => LogLevelKind.Debug,
        _ => LogLevelKind.Info
    };

    /// <summary>Analyse un bloc de lignes complet (ordre chronologique).</summary>
    public static List<LogEntry> ParseAll(IEnumerable<string> lines)
    {
        var parser = new LogParser();
        var result = new List<LogEntry>();
        foreach (var line in lines)
        {
            var entry = parser.Feed(line);
            if (entry is not null) result.Add(entry);
        }
        return result;
    }
}
