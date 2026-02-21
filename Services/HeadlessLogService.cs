using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class HeadlessLogService(ISptLogger<HeadlessLogService> logger)
{
    private string _basePath = "";
    private string _currentLogDir = "";
    private long _lastReadPosition;
    private DateTime _lastDirScan = DateTime.MinValue;
    private string? _cachedLatestDir;
    private readonly List<ConsoleEntry> _buffer = new();
    private const int MaxBuffer = 500;
    private static readonly TimeSpan DirScanInterval = TimeSpan.FromSeconds(10);

    public bool IsConfigured => !string.IsNullOrEmpty(_basePath);
    public string BasePath => _basePath;

    public void Configure(string basePath, string modPath)
    {
        _basePath = basePath?.Trim() ?? "";

        // Auto-detect: check {game root}/Logs/ (up 4) and {SPT root}/Logs/ (up 3)
        if (!IsConfigured && !string.IsNullOrEmpty(modPath))
        {
            // modPath = {game_root}/SPT/user/mods/ZSlayerCommandCenter
            var sptRoot = Path.GetFullPath(Path.Combine(modPath, "..", "..", ".."));
            var gameRoot = Path.GetFullPath(Path.Combine(sptRoot, ".."));

            foreach (var candidate in new[] { Path.Combine(gameRoot, "Logs"), Path.Combine(sptRoot, "Logs") })
            {
                if (Directory.Exists(candidate) && Directory.GetDirectories(candidate, "log_*").Length > 0)
                {
                    _basePath = candidate;
                    return;
                }
            }
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

    private string? FindLatestLogDir()
    {
        if (!Directory.Exists(_basePath)) return null;

        // Cache directory scan — only rescan every 10s
        if (DateTime.UtcNow - _lastDirScan < DirScanInterval && _cachedLatestDir != null)
            return _cachedLatestDir;

        _cachedLatestDir = Directory.GetDirectories(_basePath, "log_*")
            .OrderByDescending(Path.GetFileName)
            .FirstOrDefault();
        _lastDirScan = DateTime.UtcNow;
        return _cachedLatestDir;
    }

    private void PollNewEntries()
    {
        if (!IsConfigured) return;

        var latestDir = FindLatestLogDir();
        if (latestDir == null) return;

        // If log directory changed (new client launch), reset position
        if (latestDir != _currentLogDir)
        {
            _currentLogDir = latestDir;
            _lastReadPosition = 0;
        }

        var logFile = Path.Combine(latestDir, "application.log");
        if (!File.Exists(logFile)) return;

        try
        {
            using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= _lastReadPosition) return;

            fs.Seek(_lastReadPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            var newContent = reader.ReadToEnd();
            _lastReadPosition = fs.Position;

            var lines = newContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lock (_buffer)
            {
                foreach (var line in lines)
                {
                    var entry = ParseLine(line.TrimEnd('\r'));
                    if (entry != null)
                        _buffer.Add(entry);
                }

                // Trim once after batch insert instead of per-entry O(n) RemoveAt(0)
                if (_buffer.Count > MaxBuffer)
                    _buffer.RemoveRange(0, _buffer.Count - MaxBuffer);
            }
        }
        catch
        {
            // Graceful — don't crash on file access issues
        }
    }

    private static ConsoleEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        // Format: 2026-02-21 05:11:41.079 +00:00|0.16.9.0.40087|Info|source|message
        var parts = line.Split('|', 5);
        if (parts.Length >= 5)
        {
            var level = parts[2].Trim().ToLowerInvariant();
            if (level == "warn") level = "warning";

            DateTime.TryParse(parts[0].Trim(), out var ts);

            return new ConsoleEntry
            {
                Timestamp = ts == default ? DateTime.UtcNow : ts,
                Level = level,
                Source = parts[3].Trim(),
                Message = parts[4].Trim()
            };
        }

        // Fallback for lines that don't match the expected format
        return new ConsoleEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "info",
            Source = "",
            Message = line
        };
    }
}
