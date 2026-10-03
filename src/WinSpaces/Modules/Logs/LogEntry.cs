using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace WinSpaces.Modules.Logs;

/// <summary>Niveau d'une entrée du journal (ordre = gravité croissante).</summary>
public enum LogLevelKind
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Entrée du journal WinSpaces. Une entrée peut s'étendre sur plusieurs lignes
/// (trace de pile d'une exception) : les lignes suivantes y sont ajoutées.
/// </summary>
public sealed class LogEntry : INotifyPropertyChanged
{
    private const int SummaryMaxLength = 300;
    private readonly StringBuilder _message;
    private string? _messageCache;

    public LogEntry(DateTime? timestamp, LogLevelKind kind, string firstLine)
    {
        Timestamp = timestamp;
        Kind = kind;
        _message = new StringBuilder(firstLine);
        Summary = firstLine.Length > SummaryMaxLength ? firstLine[..SummaryMaxLength] + "…" : firstLine;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime? Timestamp { get; }
    public LogLevelKind Kind { get; }

    /// <summary>Texte complet (toutes les lignes).</summary>
    public string Message => _messageCache ??= _message.ToString();

    /// <summary>Première ligne, tronquée pour l'affichage en liste.</summary>
    public string Summary { get; }

    /// <summary>Vrai si l'entrée comporte des lignes de détail (trace de pile…).</summary>
    public bool HasDetails { get; private set; }

    public string TimeText => Timestamp?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "--:--:--";

    public string DateTimeText => Timestamp?.ToString("dddd d MMMM yyyy, HH:mm:ss", CultureInfo.GetCultureInfo("fr-FR")) ?? "Date inconnue";

    public string LevelLabel => Kind switch
    {
        LogLevelKind.Error => "Erreur",
        LogLevelKind.Warning => "Avert.",
        LogLevelKind.Debug => "Debug",
        _ => "Info"
    };

    internal void AppendLine(string line)
    {
        _message.Append(Environment.NewLine).Append(line);
        _messageCache = null;
        HasDetails = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDetails)));
    }
}
