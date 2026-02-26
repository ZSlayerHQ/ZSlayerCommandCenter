using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public partial class HeadlessLogService(ISptLogger<HeadlessLogService> logger)
{
    private string _logFilePath = "";
    private long _lastReadPosition;
    private readonly List<ConsoleEntry> _buffer = new();
    private const int MaxBuffer = 500;

    public bool IsConfigured => !string.IsNullOrEmpty(_logFilePath);

    /// <summary>
    /// Configure with an explicit log file path, or auto-detect BepInEx/LogOutput.log from modPath.
    /// </summary>
    public void Configure(string explicitPath, string modPath)
    {
        var path = explicitPath?.Trim() ?? "";

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            _logFilePath = path;
            logger.Info($"HeadlessLogService: using configured path — {_logFilePath}");
            return;
        }

        // Auto-detect: modPath = {game_root}/SPT/user/mods/ZSlayerCommandCenter → up 4 levels
        if (!string.IsNullOrEmpty(modPath))
        {
            var gameRoot = Path.GetFullPath(Path.Combine(modPath, "..", "..", "..", ".."));
            var candidate = Path.Combine(gameRoot, "BepInEx", "LogOutput.log");
            if (File.Exists(candidate))
            {
                _logFilePath = candidate;
                logger.Info($"HeadlessLogService: auto-detected BepInEx log — {_logFilePath}");
                return;
            }
        }
    }

    /// <summary>
    /// Set (or change) the log file to poll.
    /// </summary>
    public void SetLogFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        _logFilePath = path;
        logger.Info($"HeadlessLogService: watching — {_logFilePath}");
    }

    /// <summary>
    /// Reset read position to start of file and clear buffer.
    /// Call when the headless process (re)starts so we read the fresh log from the top.
    /// </summary>
    public void Reset()
    {
        _lastReadPosition = 0;
        lock (_buffer)
        {
            _buffer.Clear();
        }
    }

    public List<ConsoleEntry> GetEntriesSince(DateTime since)
    {
        PollNewEntries();
        lock (_buffer)
        {
            return _buffer.Where(e => e.Timestamp > since).ToList();
        }
    }

    public List<ConsoleEntry> GetHistory(int lines)
    {
        PollNewEntries();
        lock (_buffer)
        {
            return _buffer.TakeLast(lines).ToList();
        }
    }

    private void PollNewEntries()
    {
        if (!IsConfigured) return;
        if (!File.Exists(_logFilePath)) return;

        try
        {
            using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // BepInEx overwrites LogOutput.log on each game launch — if file is shorter
            // than our last position, the client restarted and we should read from the top.
            if (fs.Length < _lastReadPosition)
                _lastReadPosition = 0;

            if (fs.Length <= _lastReadPosition) return;

            fs.Seek(_lastReadPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            var newContent = reader.ReadToEnd();
            _lastReadPosition = fs.Position;

            var lines = newContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lock (_buffer)
            {
                foreach (var l in lines)
                {
                    var entry = ParseLine(l.TrimEnd('\r'));
                    if (entry != null)
                        _buffer.Add(entry);
                }

                if (_buffer.Count > MaxBuffer)
                    _buffer.RemoveRange(0, _buffer.Count - MaxBuffer);
            }
        }
        catch
        {
            // Graceful — don't crash on file access issues
        }
    }

    // BepInEx format: [Level   : Source] Message
    // Level is right-padded, Source is right-padded
    [GeneratedRegex(@"^\[(\w+)\s*:\s*([^\]]*?)\s*\]\s*(.*)$")]
    private static partial Regex BepInExPattern();

    private static ConsoleEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m = BepInExPattern().Match(line);
        if (m.Success)
        {
            var level = m.Groups[1].Value.ToLowerInvariant();
            // Normalize BepInEx levels
            if (level == "message") level = "info";
            if (level == "warn") level = "warning";
            if (level == "fatal") level = "error";

            return new ConsoleEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Source = m.Groups[2].Value.Trim(),
                Message = m.Groups[3].Value
            };
        }

        // Fallback for unstructured lines (stack traces, etc.)
        var inferredLevel = "info";
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("exception", StringComparison.OrdinalIgnoreCase))
            inferredLevel = "error";
        else if (line.Contains("warn", StringComparison.OrdinalIgnoreCase))
            inferredLevel = "warning";

        return new ConsoleEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = inferredLevel,
            Source = "",
            Message = line
        };
    }
}
