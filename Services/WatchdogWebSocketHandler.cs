using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers.Ws;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class WatchdogWebSocketHandler(
    WatchdogManager watchdogManager,
    ConfigService configService,
    ISptLogger<WatchdogWebSocketHandler> logger) : IWebSocketConnectionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Track WebSocket → sessionIdContext mapping (OnMessage doesn't receive sessionIdContext)
    private readonly Dictionary<WebSocket, string> _socketToSession = new();
    private readonly Lock _mapLock = new();
    private bool _warnedOpenMode;

    public string GetHookUrl() => "/ws/watchdog";

    public string GetSocketId() => "ZSlayer Watchdog WebSocket";

    public async Task OnConnection(WebSocket ws, HttpContext context, string sessionIdContext)
    {
        var token = configService.GetConfig().Watchdog.WatchdogToken;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!string.IsNullOrEmpty(token))
        {
            var clientToken = context.Request.Query["token"].ToString();
            if (clientToken != token)
            {
                logger.Warning($"[ZSlayerHQ] Watchdog connection rejected — invalid token from {remoteIp}");
                await ws.CloseAsync(
                    (WebSocketCloseStatus)4001,
                    "Invalid or missing auth token",
                    CancellationToken.None);
                return;
            }
        }
        else if (!_warnedOpenMode)
        {
            logger.Warning("[ZSlayerHQ] Watchdog WebSocket running in OPEN mode — no auth token configured. Set watchdogToken in config.");
            _warnedOpenMode = true;
        }

        using (_mapLock.EnterScope())
        {
            _socketToSession[ws] = sessionIdContext;
        }

        logger.Info($"[ZSlayerHQ] Watchdog WebSocket authenticated and connected from {remoteIp} (ref: {sessionIdContext})");
    }

    public Task OnMessage(byte[] rawData, WebSocketMessageType messageType, WebSocket ws, HttpContext context)
    {
        string json;
        try
        {
            json = Encoding.UTF8.GetString(rawData);
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to decode Watchdog message: {ex.Message}");
            return Task.CompletedTask;
        }

        // Look up sessionIdContext for this WebSocket
        string sessionIdContext;
        using (_mapLock.EnterScope())
        {
            if (!_socketToSession.TryGetValue(ws, out sessionIdContext!))
                sessionIdContext = "unknown";
        }

        try
        {
            var baseMsg = JsonSerializer.Deserialize<WatchdogMessage>(json, JsonOptions);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.Type))
            {
                logger.Warning("[ZSlayerHQ] Watchdog message missing 'type' field");
                return Task.CompletedTask;
            }

            switch (baseMsg.Type)
            {
                case "register":
                {
                    var msg = JsonSerializer.Deserialize<WatchdogRegisterMessage>(json, JsonOptions);
                    if (msg == null || string.IsNullOrEmpty(msg.WatchdogId))
                    {
                        logger.Warning("[ZSlayerHQ] Invalid register message — missing watchdogId");
                        return Task.CompletedTask;
                    }
                    watchdogManager.HandleRegister(sessionIdContext, ws, msg);
                    break;
                }
                case "status":
                {
                    var msg = JsonSerializer.Deserialize<WatchdogStatusMessage>(json, JsonOptions);
                    if (msg == null || string.IsNullOrEmpty(msg.WatchdogId))
                    {
                        logger.Warning("[ZSlayerHQ] Invalid status message — missing watchdogId");
                        return Task.CompletedTask;
                    }
                    watchdogManager.HandleStatus(msg);
                    break;
                }
                case "commandResult":
                {
                    var msg = JsonSerializer.Deserialize<WatchdogCommandResultMessage>(json, JsonOptions);
                    if (msg == null || string.IsNullOrEmpty(msg.WatchdogId))
                    {
                        logger.Warning("[ZSlayerHQ] Invalid commandResult message — missing watchdogId");
                        return Task.CompletedTask;
                    }
                    watchdogManager.HandleCommandResult(msg);
                    break;
                }
                default:
                    logger.Warning($"[ZSlayerHQ] Unknown Watchdog message type: {baseMsg.Type}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to parse Watchdog message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task OnClose(WebSocket ws, HttpContext context, string sessionIdContext)
    {
        using (_mapLock.EnterScope())
        {
            _socketToSession.Remove(ws);
        }

        watchdogManager.HandleDisconnect(sessionIdContext);
        return Task.CompletedTask;
    }
}
