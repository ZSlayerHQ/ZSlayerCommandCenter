using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public partial class ConsoleBufferService
{
    /// <summary>Static accessor for use from the TextWriter interceptor (no DI available there).</summary>
    public static ConsoleBufferService? Instance { get; private set; }

    private readonly ConcurrentQueue<ConsoleEntry> _buffer = new();
    private int _maxSize = 500;

    public ConsoleBufferService()
    {
        Instance = this;
    }

    public void Configure(int maxSize)
    {
        _maxSize = maxSize;
    }

    public void Add(ConsoleEntry entry)
    {
        _buffer.Enqueue(entry);
        while (_buffer.Count > _maxSize)
            _buffer.TryDequeue(out _);
    }

    public List<ConsoleEntry> GetEntriesSince(DateTime since)
    {
        return _buffer.Where(e => e.Timestamp > since).ToList();
    }

    public List<ConsoleEntry> GetHistory(int lines)
    {
        return _buffer.TakeLast(lines).ToList();
    }

    /// <summary>
    /// Install a TextWriter wrapper around Console.Out to intercept all server log output.
    /// Call once at startup.
    /// </summary>
    public void InstallConsoleInterceptor()
    {
        var original = Console.Out;
        Console.SetOut(new ConsoleInterceptWriter(original, this));
    }

    /// <summary>
    /// TextWriter that wraps the original Console.Out, forwarding all writes
    /// while also parsing and buffering log entries.
    /// </summary>
    private sealed partial class ConsoleInterceptWriter(TextWriter original, ConsoleBufferService buffer) : TextWriter
    {
        public override System.Text.Encoding Encoding => original.Encoding;

        // SPT log lines often look like:
        // [INFO] [SourceName] message text
        // or just plain text lines
        [GeneratedRegex(@"^\[(\w+)\]\s*\[([^\]]*)\]\s*(.*)$")]
        private static partial Regex LogLinePattern();

        public override void WriteLine(string? value)
        {
            original.WriteLine(value);
            if (!string.IsNullOrEmpty(value))
                ParseAndBuffer(value);
        }

        public override void Write(string? value)
        {
            original.Write(value);
            // Only buffer complete lines via WriteLine
        }

        public override void Write(char value)
        {
            original.Write(value);
        }

        public override void Flush()
        {
            original.Flush();
        }

        private void ParseAndBuffer(string line)
        {
            // Strip ANSI escape codes
            var clean = StripAnsi(line);
            if (string.IsNullOrWhiteSpace(clean)) return;

            var match = LogLinePattern().Match(clean);
            if (match.Success)
            {
                buffer.Add(new ConsoleEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = match.Groups[1].Value.ToLowerInvariant(),
                    Source = match.Groups[2].Value,
                    Message = match.Groups[3].Value
                });
            }
            else
            {
                // Infer level from content
                var level = "info";
                if (clean.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains("exception", StringComparison.OrdinalIgnoreCase))
                    level = "error";
                else if (clean.Contains("warn", StringComparison.OrdinalIgnoreCase))
                    level = "warning";

                buffer.Add(new ConsoleEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level,
                    Source = "",
                    Message = clean
                });
            }
        }

        private static string StripAnsi(string input)
        {
            // Remove ANSI escape sequences: ESC[ ... m
            return AnsiPattern().Replace(input, "");
        }

        [GeneratedRegex(@"\x1B\[[0-9;]*m")]
        private static partial Regex AnsiPattern();
    }
}
