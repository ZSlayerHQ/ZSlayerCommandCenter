using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public partial class HeadlessLogService(ISptLogger<HeadlessLogService> logger)
{
    private readonly object _lock = new();
    private readonly Dictionary<string, SourceBuffer> _sources = new();
    private const int MaxBuffer = 500;
    private static readonly TimeSpan StaleTimeout = TimeSpan.FromMinutes(30);

    // File-based polling state (for "local" source)
    private string _logFilePath = "";
    private long _lastReadPosition;

    public bool IsConfigured
    {
        get
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_logFilePath)) return true;
                return _sources.Count > 0;
            }
        }
    }

    public void Configure(string explicitPath, string modPath)
    {
        var path = explicitPath?.Trim() ?? "";

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            _logFilePath = path;
            logger.Info($"HeadlessLogService: using configured path — {_logFilePath}");
            return;
        }

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

    public void SetLogFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        _logFilePath = path;
        logger.Info($"HeadlessLogService: watching — {_logFilePath}");
    }

    public void Reset()
    {
        _lastReadPosition = 0;
        lock (_lock)
        {
            if (_sources.TryGetValue("local", out var local))
                local.Entries.Clear();
        }
    }

    public void AddStreamedEntries(string sourceId, string hostname, List<ConsoleEntry> entries)
    {
        if (string.IsNullOrEmpty(sourceId) || entries.Count == 0) return;

        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var buf))
            {
                buf = new SourceBuffer
                {
                    SourceId = sourceId,
                    DisplayName = hostname,
                    Hostname = hostname,
                    IsStreaming = true,
                    FirstSeen = DateTime.UtcNow
                };
                _sources[sourceId] = buf;
                logger.Info($"HeadlessLogService: new streaming source — {sourceId} ({hostname})");
            }

            buf.LastSeen = DateTime.UtcNow;
            buf.Hostname = hostname;
            if (!string.IsNullOrEmpty(hostname))
                buf.DisplayName = hostname;

            foreach (var entry in entries)
            {
                entry.SourceId = sourceId;
                buf.Entries.Add(entry);
            }

            if (buf.Entries.Count > MaxBuffer)
                buf.Entries.RemoveRange(0, buf.Entries.Count - MaxBuffer);
        }
    }

    public List<ConsoleEntry> GetEntriesSince(DateTime since, string? sourceId = null)
    {
        PollNewEntries();
        lock (_lock)
        {
            CleanupStaleSources();

            if (!string.IsNullOrEmpty(sourceId))
            {
                if (_sources.TryGetValue(sourceId, out var buf))
                    return buf.Entries.Where(e => e.Timestamp > since).ToList();
                return [];
            }

            return _sources.Values
                .SelectMany(s => s.Entries)
                .Where(e => e.Timestamp > since)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
    }

    public List<ConsoleEntry> GetHistory(int lines, string? sourceId = null)
    {
        PollNewEntries();
        lock (_lock)
        {
            CleanupStaleSources();

            if (!string.IsNullOrEmpty(sourceId))
            {
                if (_sources.TryGetValue(sourceId, out var buf))
                    return buf.Entries.TakeLast(lines).ToList();
                return [];
            }

            return _sources.Values
                .SelectMany(s => s.Entries)
                .OrderBy(e => e.Timestamp)
                .TakeLast(lines)
                .ToList();
        }
    }

    public List<ConsoleSourceDto> GetSources()
    {
        PollNewEntries();
        lock (_lock)
        {
            CleanupStaleSources();
            return _sources.Values.Select(s => new ConsoleSourceDto
            {
                SourceId = s.SourceId,
                DisplayName = s.DisplayName,
                Hostname = s.Hostname,
                LastSeen = s.LastSeen,
                EntryCount = s.Entries.Count,
                IsStreaming = s.IsStreaming
            }).ToList();
        }
    }

    private void CleanupStaleSources()
    {
        var now = DateTime.UtcNow;
        var stale = _sources
            .Where(kv => kv.Value.IsStreaming && (now - kv.Value.LastSeen) > StaleTimeout)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in stale)
        {
            _sources.Remove(key);
            logger.Info($"HeadlessLogService: removed stale streaming source — {key}");
        }
    }

    private void PollNewEntries()
    {
        if (string.IsNullOrEmpty(_logFilePath)) return;
        if (!File.Exists(_logFilePath)) return;

        try
        {
            using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (fs.Length < _lastReadPosition)
                _lastReadPosition = 0;

            if (fs.Length <= _lastReadPosition) return;

            fs.Seek(_lastReadPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            var newContent = reader.ReadToEnd();
            _lastReadPosition = fs.Position;

            var lines = newContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lock (_lock)
            {
                if (!_sources.TryGetValue("local", out var local))
                {
                    local = new SourceBuffer
                    {
                        SourceId = "local",
                        DisplayName = "Local Headless",
                        Hostname = Environment.MachineName,
                        IsStreaming = false,
                        FirstSeen = DateTime.UtcNow
                    };
                    _sources["local"] = local;
                }

                foreach (var l in lines)
                {
                    var entry = ParseLine(l.TrimEnd('\r'));
                    if (entry != null)
                    {
                        entry.SourceId = "local";
                        local.Entries.Add(entry);
                    }
                }

                local.LastSeen = DateTime.UtcNow;

                if (local.Entries.Count > MaxBuffer)
                    local.Entries.RemoveRange(0, local.Entries.Count - MaxBuffer);
            }
        }
        catch
        {
            // Graceful — don't crash on file access issues
        }
    }

    // BepInEx format: [Level   : Source] Message
    [GeneratedRegex(@"^\[(\w+)\s*:\s*([^\]]*?)\s*\]\s*(.*)$")]
    private static partial Regex BepInExPattern();

    private static ConsoleEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m = BepInExPattern().Match(line);
        if (m.Success)
        {
            var level = m.Groups[1].Value.ToLowerInvariant();
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

    private class SourceBuffer
    {
        public string SourceId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Hostname { get; set; } = "";
        public bool IsStreaming { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public List<ConsoleEntry> Entries { get; } = new();
    }
}
