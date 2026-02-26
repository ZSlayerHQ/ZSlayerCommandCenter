using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class WatchdogManager(ISptLogger<WatchdogManager> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<string, ConnectedWatchdog> _watchdogs = new();
    private readonly Dictionary<string, string> _sessionToWatchdog = new();
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTime> _lastCommandTime = new();
    private static readonly TimeSpan CommandCooldown = TimeSpan.FromSeconds(5);
    private const string TokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public void HandleRegister(string sessionIdContext, WebSocket socket, WatchdogRegisterMessage msg)
    {
        using (_lock.EnterScope())
        {
            // If this watchdogId already exists (reconnect), clean up old session mapping
            if (_watchdogs.TryGetValue(msg.WatchdogId, out var existing))
            {
                _sessionToWatchdog.Remove(existing.SessionIdContext);
            }

            _watchdogs[msg.WatchdogId] = new ConnectedWatchdog
            {
                WatchdogId = msg.WatchdogId,
                Name = msg.Name,
                Hostname = msg.Hostname,
                Ip = msg.Ip,
                Manages = msg.Manages,
                Socket = socket,
                SessionIdContext = sessionIdContext,
                ConnectedAt = DateTime.UtcNow,
                LastStatusAt = DateTime.UtcNow
            };

            _sessionToWatchdog[sessionIdContext] = msg.WatchdogId;
        }

        logger.Info($"[ZSlayerHQ] Watchdog connected: {msg.Name} ({msg.Hostname}, {msg.Ip})");
        logger.Debug($"[ZSlayerHQ] Watchdog registered: {msg.WatchdogId} manages sptServer={msg.Manages.SptServer}, headlessClient={msg.Manages.HeadlessClient}");
    }

    public void HandleStatus(WatchdogStatusMessage msg)
    {
        using (_lock.EnterScope())
        {
            if (!_watchdogs.TryGetValue(msg.WatchdogId, out var wd))
            {
                logger.Warning($"[ZSlayerHQ] Received status from unknown Watchdog: {msg.WatchdogId}");
                return;
            }

            wd.SptServer = msg.SptServer;
            wd.HeadlessClient = msg.HeadlessClient;
            wd.System = msg.System;
            wd.LastStatusAt = DateTime.UtcNow;
        }

        var serverRunning = msg.SptServer?.Running == true;
        var headlessRunning = msg.HeadlessClient?.Running == true;
        logger.Debug($"[ZSlayerHQ] Watchdog status from {msg.WatchdogId}: server={serverRunning}, headless={headlessRunning}");
    }

    public void HandleCommandResult(WatchdogCommandResultMessage msg)
    {
        string name;
        using (_lock.EnterScope())
        {
            name = _watchdogs.TryGetValue(msg.WatchdogId, out var wd) ? wd.Name : msg.WatchdogId;
        }

        var status = msg.Success ? "success" : "fail";
        logger.Info($"[ZSlayerHQ] Command result from {name}: {msg.Target}.{msg.Action} -> {status}: {msg.Message}");
    }

    public void HandleDisconnect(string sessionIdContext)
    {
        string name;
        using (_lock.EnterScope())
        {
            if (!_sessionToWatchdog.TryGetValue(sessionIdContext, out var watchdogId))
                return;

            name = _watchdogs.TryGetValue(watchdogId, out var wd) ? wd.Name : watchdogId;
            _watchdogs.Remove(watchdogId);
            _sessionToWatchdog.Remove(sessionIdContext);
        }

        logger.Info($"[ZSlayerHQ] Watchdog disconnected: {name}");
    }

    public List<WatchdogStatusEntry> GetConnectedWatchdogs()
    {
        using (_lock.EnterScope())
        {
            return _watchdogs.Values.Select(wd => new WatchdogStatusEntry
            {
                WatchdogId = wd.WatchdogId,
                Name = wd.Name,
                Hostname = wd.Hostname,
                Ip = wd.Ip,
                Connected = wd.Socket.State == WebSocketState.Open,
                Manages = wd.Manages,
                SptServer = wd.Manages.SptServer ? wd.SptServer : null,
                HeadlessClient = wd.Manages.HeadlessClient ? wd.HeadlessClient : null,
                System = wd.System
            }).ToList();
        }
    }

    /// <summary>
    /// Send a command to a specific Watchdog via WebSocket.
    /// Returns (sent, message) for the HTTP response.
    /// </summary>
    public async Task<(bool Sent, string Message)> SendCommand(string watchdogId, string target, string action)
    {
        WebSocket socket;
        string name;

        using (_lock.EnterScope())
        {
            if (!_watchdogs.TryGetValue(watchdogId, out var wd))
                return (false, "Watchdog not connected");

            if (wd.Socket.State != WebSocketState.Open)
                return (false, $"Watchdog '{wd.Name}' socket is not open");

            socket = wd.Socket;
            name = wd.Name;
        }

        var cmd = new WatchdogCommandMessage { Target = target, Action = action };
        var json = JsonSerializer.Serialize(cmd, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            logger.Info($"[ZSlayerHQ] Command sent to {name}: {target}.{action}");
            return (true, $"Command sent to {name}");
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to send command to {name}: {ex.Message}");
            return (false, $"Failed to send command to {name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Find all connected Watchdogs that manage a given target.
    /// </summary>
    public List<ConnectedWatchdog> GetWatchdogsForTarget(string target)
    {
        using (_lock.EnterScope())
        {
            return _watchdogs.Values.Where(wd =>
            {
                return target switch
                {
                    "sptServer" => wd.Manages.SptServer,
                    "headlessClient" => wd.Manages.HeadlessClient,
                    _ => false
                };
            }).Where(wd => wd.Socket.State == WebSocketState.Open).ToList();
        }
    }

    /// <summary>
    /// Send a command to the first connected Watchdog for a target.
    /// Used by REST endpoints that don't specify a watchdogId.
    /// </summary>
    public async Task<(bool Sent, string Message)> SendCommandToTarget(string target, string action)
    {
        var candidates = GetWatchdogsForTarget(target);
        if (candidates.Count == 0)
        {
            logger.Warning($"[ZSlayerHQ] No Watchdog connected for target: {target}");
            return (false, $"No Watchdog connected for {target}");
        }

        // Rate limit check using the first candidate's ID
        var (allowed, rateLimitMsg) = CheckRateLimit(candidates[0].WatchdogId, target);
        if (!allowed)
        {
            logger.Warning($"[ZSlayerHQ] {rateLimitMsg}");
            return (false, rateLimitMsg);
        }

        // Send to the first available Watchdog
        return await SendCommand(candidates[0].WatchdogId, target, action);
    }

    /// <summary>
    /// Check if any Watchdog is connected and managing the headless client.
    /// Returns the headless status from the first matching Watchdog.
    /// </summary>
    public (bool Available, WatchdogProcessStatus? Status) GetHeadlessStatus()
    {
        var candidates = GetWatchdogsForTarget("headlessClient");
        if (candidates.Count == 0)
            return (false, null);

        return (true, candidates[0].HeadlessClient);
    }

    /// <summary>
    /// Generate a cryptographically random 32-char alphanumeric token.
    /// </summary>
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var chars = new char[32];
        for (var i = 0; i < 32; i++)
            chars[i] = TokenChars[bytes[i] % TokenChars.Length];
        return new string(chars);
    }

    /// <summary>
    /// Check if a command to a target is rate-limited (5s cooldown per watchdog+target).
    /// </summary>
    public (bool Allowed, string Message) CheckRateLimit(string watchdogId, string target)
    {
        var key = $"{watchdogId}:{target}";
        using (_lock.EnterScope())
        {
            if (_lastCommandTime.TryGetValue(key, out var lastTime))
            {
                var elapsed = DateTime.UtcNow - lastTime;
                if (elapsed < CommandCooldown)
                {
                    var remaining = (int)Math.Ceiling((CommandCooldown - elapsed).TotalSeconds);
                    return (false, $"Rate limited — wait {remaining}s before sending another command to {target}");
                }
            }

            _lastCommandTime[key] = DateTime.UtcNow;
        }

        return (true, "");
    }

    /// <summary>
    /// Disconnect all connected Watchdogs (e.g. after token regeneration).
    /// </summary>
    public async Task DisconnectAll(string reason)
    {
        List<(WebSocket Socket, string Name)> toClose;
        using (_lock.EnterScope())
        {
            toClose = _watchdogs.Values
                .Select(wd => (wd.Socket, wd.Name))
                .ToList();
            _watchdogs.Clear();
            _sessionToWatchdog.Clear();
            _lastCommandTime.Clear();
        }

        foreach (var (socket, name) in toClose)
        {
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(
                        (WebSocketCloseStatus)4001, reason, CancellationToken.None);
                }
            }
            catch { /* best effort */ }

            logger.Info($"[ZSlayerHQ] Watchdog disconnected (token change): {name}");
        }
    }
}
