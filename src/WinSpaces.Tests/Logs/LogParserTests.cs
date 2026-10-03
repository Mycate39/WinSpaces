using System.IO;
using WinSpaces.Modules.Logs;
using Xunit;

namespace WinSpaces.Tests.Logs;

public class LogParserTests
{
    [Fact]
    public void ParseAll_ReadsTimestampLevelAndMessage()
    {
        var entries = LogParser.ParseAll(new[]
        {
            "[2026-10-03 11:46:02] [INFO] Démarrage",
            "[2026-10-03 11:46:03] [WARN] Attention",
            "[2026-10-03 11:46:04] [ERROR] Boom",
            "[2026-10-03 11:46:05] [DEBUG] Détail"
        });

        Assert.Equal(4, entries.Count);
        Assert.Equal(new DateTime(2026, 10, 3, 11, 46, 2), entries[0].Timestamp);
        Assert.Equal(LogLevelKind.Info, entries[0].Kind);
        Assert.Equal("Démarrage", entries[0].Message);
        Assert.Equal(LogLevelKind.Warning, entries[1].Kind);
        Assert.Equal(LogLevelKind.Error, entries[2].Kind);
        Assert.Equal(LogLevelKind.Debug, entries[3].Kind);
    }

    [Fact]
    public void ParseAll_AppendsStackTraceLinesToPreviousEntry()
    {
        var entries = LogParser.ParseAll(new[]
        {
            "[2026-10-03 11:46:04] [ERROR] System.InvalidOperationException: Boom",
            "   at WinSpaces.Program.Main()",
            "   at Foo.Bar()",
            "[2026-10-03 11:46:05] [INFO] Suite"
        });

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].HasDetails);
        Assert.Equal("System.InvalidOperationException: Boom", entries[0].Summary);
        Assert.Contains("at Foo.Bar()", entries[0].Message);
        Assert.False(entries[1].HasDetails);
    }

    [Fact]
    public void Feed_KeepsTextWithoutHeaderAndIgnoresBlankLines()
    {
        var entries = LogParser.ParseAll(new[] { "texte libre", "", "[2026-10-03 11:46:05] [INFO] Suite\r" });

        Assert.Equal(2, entries.Count);
        Assert.Null(entries[0].Timestamp);
        Assert.Equal("Suite", entries[1].Message);
    }

    [Fact]
    public void LogFileTail_ReadsOnlyAppendedLinesAndDetectsTruncation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winspaces-test-{Guid.NewGuid():N}.log");
        try
        {
            File.WriteAllText(path, "[2026-10-03 11:46:02] [INFO] A\n[2026-10-03 11:46:03] [INFO] B\n");
            var tail = new LogFileTail(path);

            Assert.Equal(2, tail.ReadNewLines(out bool reset1).Count);
            Assert.False(reset1);

            // Ligne incomplète : conservée jusqu'à son retour à la ligne.
            File.AppendAllText(path, "[2026-10-03 11:46:04] [INFO] C");
            Assert.Empty(tail.ReadNewLines(out _));
            File.AppendAllText(path, "\n");
            Assert.Equal(new[] { "[2026-10-03 11:46:04] [INFO] C" }, tail.ReadNewLines(out _));

            // Effacement : le fichier raccourcit → réinitialisation signalée.
            File.WriteAllText(path, "[2026-10-03 11:47:00] [INFO] D\n");
            var lines = tail.ReadNewLines(out bool reset2);
            Assert.True(reset2);
            Assert.Single(lines);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
