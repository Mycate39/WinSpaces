using System.IO;
using System.Text;

namespace WinSpaces.Modules.Logs;

/// <summary>
/// Lecture incrémentale d'un fichier journal pendant qu'AppLog y écrit :
/// ne relit que les octets ajoutés depuis la dernière lecture et détecte la
/// rotation / l'effacement (fichier devenu plus court).
/// </summary>
public sealed class LogFileTail
{
    private long _position;
    private string _pendingLine = string.Empty;

    public LogFileTail(string path) => Path = path;

    public string Path { get; }

    public LogParser Parser { get; private set; } = new();

    /// <summary>
    /// Lit les nouvelles lignes complètes. <paramref name="wasReset"/> vaut vrai
    /// si le fichier a été tronqué ou remplacé : l'appelant doit tout recharger.
    /// </summary>
    public List<string> ReadNewLines(out bool wasReset)
    {
        wasReset = false;
        var lines = new List<string>();

        if (!File.Exists(Path))
        {
            if (_position > 0) { Reset(); wasReset = true; }
            return lines;
        }

        // FileShare.ReadWrite | Delete : AppLog continue d'écrire et la rotation
        // doit pouvoir renommer le fichier pendant la lecture.
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length < _position)
        {
            Reset();
            wasReset = true;
        }
        if (stream.Length == _position) return lines;

        stream.Seek(_position, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 64 * 1024, leaveOpen: true);
        var text = _pendingLine + reader.ReadToEnd();
        _position = stream.Position;

        var parts = text.Split('\n');
        // Le dernier fragment est une ligne incomplète (écriture en cours) ou vide.
        _pendingLine = parts[^1];
        for (var i = 0; i < parts.Length - 1; i++)
            lines.Add(parts[i]);

        return lines;
    }

    public void Reset()
    {
        _position = 0;
        _pendingLine = string.Empty;
        Parser = new LogParser();
    }
}
