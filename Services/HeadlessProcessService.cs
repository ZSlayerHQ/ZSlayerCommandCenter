using System.Diagnostics;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class HeadlessProcessService(
    ConfigService configService,
    ISptLogger<HeadlessProcessService> logger)
{
    private Process? _process;
    private DateTime? _startedAt;
    private int _restartCount;
    private string? _lastCrashReason;
    private string _exePath = "";
    private string _workingDir = "";
    private bool _available;
    private bool _stopping;
    private CancellationTokenSource? _autoStartCts;

    public bool IsRunning => _process != null && !_process.HasExited;

    public void Configure(string modPath)
    {
        var config = configService.GetConfig().Headless;

        // Resolve EXE path
        if (!string.IsNullOrEmpty(config.ExePath) && File.Exists(config.ExePath))
        {
            _exePath = config.ExePath;
        }
        else if (!string.IsNullOrEmpty(modPath))
        {
            // modPath = {game_root}/SPT/user/mods/ZSlayerCommandCenter → up 4 levels
            var gameRoot = Path.GetFullPath(Path.Combine(modPath, "..", "..", "..", ".."));
            var candidate = Path.Combine(gameRoot, "EscapeFromTarkov.exe");
            if (File.Exists(candidate))
                _exePath = candidate;
        }

        _available = !string.IsNullOrEmpty(_exePath) && File.Exists(_exePath);
        _workingDir = _available ? Path.GetDirectoryName(_exePath)! : "";

        if (_available)
            logger.Info($"HeadlessProcessService: found EXE at '{_exePath}'");

        // Auto-read profileId from HeadlessConfig.json if not set in our config
        if (_available && string.IsNullOrEmpty(config.ProfileId))
        {
            var headlessConfigPath = Path.Combine(_workingDir, "HeadlessConfig.json");
            if (File.Exists(headlessConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(headlessConfigPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ProfileId", out var pid))
                    {
                        config.ProfileId = pid.GetString() ?? "";
                        if (!string.IsNullOrEmpty(config.ProfileId))
                        {
                            logger.Info($"HeadlessProcessService: auto-read profileId from HeadlessConfig.json");
                            configService.SaveConfig();
                        }
                    }
                }
                catch { /* ignore parse errors */ }
            }
        }
    }

    public void StartAutoStartTimer()
    {
        var config = configService.GetConfig().Headless;
        if (!config.AutoStart || !_available || string.IsNullOrEmpty(config.ProfileId))
            return;

        var delay = Math.Clamp(config.AutoStartDelaySec, 1, 300);
        logger.Info($"HeadlessProcessService: auto-start in {delay}s");

        _autoStartCts = new CancellationTokenSource();
        var token = _autoStartCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), token);
                if (!token.IsCancellationRequested && !IsRunning)
                    Start();
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    public HeadlessStatusDto Start()
    {
        if (!_available)
            return GetStatus("EscapeFromTarkov.exe not found");

        var config = configService.GetConfig().Headless;
        if (string.IsNullOrEmpty(config.ProfileId))
            return GetStatus("Profile ID not configured");

        if (IsRunning)
            return GetStatus();

        _stopping = false;

        var args = $"-token={config.ProfileId} " +
                   $"-config={{'BackendUrl':'https://127.0.0.1:6969','Version':'live'}} " +
                   "-nographics -batchmode --enable-console true";

        try
        {
            var psi = new ProcessStartInfo(_exePath, args)
            {
                WorkingDirectory = _workingDir,
                UseShellExecute = false
            };

            _process = Process.Start(psi);
            if (_process != null)
            {
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;
                _startedAt = DateTime.UtcNow;
                logger.Success($"HeadlessProcessService: started (PID {_process.Id})");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"HeadlessProcessService: failed to start — {ex.Message}");
            return GetStatus($"Failed to start: {ex.Message}");
        }

        return GetStatus();
    }

    public HeadlessStatusDto Stop()
    {
        _stopping = true;
        _autoStartCts?.Cancel();

        if (_process != null && !_process.HasExited)
        {
            try
            {
                logger.Info($"HeadlessProcessService: stopping (PID {_process.Id})");
                _process.Kill(true);
                _process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                logger.Warning($"HeadlessProcessService: error stopping — {ex.Message}");
            }
        }

        _process = null;
        _startedAt = null;
        return GetStatus();
    }

    public HeadlessStatusDto Restart()
    {
        Stop();
        return Start();
    }

    public HeadlessStatusDto GetStatus(string? error = null)
    {
        var config = configService.GetConfig().Headless;
        var running = IsRunning;
        var uptimeSeconds = running && _startedAt.HasValue
            ? (long)(DateTime.UtcNow - _startedAt.Value).TotalSeconds
            : 0;

        return new HeadlessStatusDto
        {
            Available = _available,
            Running = running,
            Pid = running ? _process?.Id : null,
            Uptime = running ? FormatUptime(uptimeSeconds) : "",
            UptimeSeconds = uptimeSeconds,
            RestartCount = _restartCount,
            LastCrashReason = error ?? _lastCrashReason,
            AutoStart = config.AutoStart,
            AutoStartDelaySec = config.AutoStartDelaySec,
            AutoRestart = config.AutoRestart,
            ProfileId = config.ProfileId,
            ExePath = _exePath
        };
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_stopping) return;

        var exitCode = _process?.ExitCode;
        _lastCrashReason = $"Process exited with code {exitCode}";
        _restartCount++;
        _process = null;
        _startedAt = null;

        logger.Warning($"HeadlessProcessService: process exited (code {exitCode}), restart #{_restartCount}");

        var config = configService.GetConfig().Headless;
        if (config.AutoRestart && _available && !string.IsNullOrEmpty(config.ProfileId))
        {
            logger.Info("HeadlessProcessService: auto-restarting in 5s...");
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (!IsRunning && !_stopping)
                    Start();
            });
        }
    }

    private static string FormatUptime(long seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
}
