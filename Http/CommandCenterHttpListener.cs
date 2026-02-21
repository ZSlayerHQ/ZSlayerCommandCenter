using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Servers.Http;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;
using ZSlayerCommandCenter.Services;

namespace ZSlayerCommandCenter.Http;

[Injectable(TypePriority = 0)]
public class CommandCenterHttpListener(
    ItemSearchService itemSearchService,
    ItemGiveService itemGiveService,
    AccessControlService accessControlService,
    ConfigService configService,
    ServerStatsService serverStatsService,
    PlayerStatsService playerStatsService,
    RaidTrackingService raidTrackingService,
    ActivityLogService activityLogService,
    ConsoleBufferService consoleBufferService,
    MailSendService mailSendService,
    PlayerManagementService playerManagementService,
    PlayerMailService playerMailService,
    QuestBrowserService questBrowserService,
    FleaPriceService fleaPriceService,
    OfferRegenerationService offerRegenerationService,
    HeadlessLogService headlessLogService,
    HeadlessProcessService headlessProcessService,
    DatabaseService databaseService,
    ConfigServer configServer,
    SaveServer saveServer,
    ModHelper modHelper,
    ISptLogger<CommandCenterHttpListener> logger) : IHttpListener
{
    private const string BasePath = "/zslayer/cc";
    private const string LegacyBasePath = "/zslayer/itemgui";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private byte[]? _cachedHtml;

    public bool CanHandle(MongoId sessionId, HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        return path.Equals(BasePath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(BasePath + "/", StringComparison.OrdinalIgnoreCase)
            || path.Equals(LegacyBasePath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(LegacyBasePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task Handle(MongoId sessionId, HttpContext context)
    {
        SetCorsHeaders(context);

        // Handle CORS preflight
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = 200;
            await context.Response.StartAsync();
            await context.Response.CompleteAsync();
            return;
        }

        var rawPath = context.Request.Path.Value ?? "";

        // Legacy redirect: /zslayer/itemgui/* → /zslayer/cc/*
        if (rawPath.Equals(LegacyBasePath, StringComparison.OrdinalIgnoreCase)
            || rawPath.StartsWith(LegacyBasePath + "/", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = rawPath.Length > LegacyBasePath.Length
                ? rawPath.Substring(LegacyBasePath.Length)
                : "/";
            if (string.IsNullOrEmpty(suffix)) suffix = "/";

            context.Response.StatusCode = 301;
            context.Response.Headers["Location"] = BasePath + suffix;
            await context.Response.StartAsync();
            await context.Response.CompleteAsync();
            return;
        }

        var path = rawPath.Length > BasePath.Length
            ? rawPath.Substring(BasePath.Length).TrimStart('/').TrimEnd('/')
            : "";
        var method = context.Request.Method;

        try
        {
            // Serve the web UI for the root path
            if (path == "" && method == "GET")
            {
                await ServeHtml(context);
                return;
            }

            // Extract session ID from header
            var headerSessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault() ?? "";

            // Serve static files from res/ directory
            if (!string.IsNullOrEmpty(path) && method == "GET" && IsStaticFile(path))
            {
                await ServeStaticFile(context, path);
                return;
            }

            // Handle parameterized quest routes: quests/{questId}/action
            if (path.StartsWith("quests/") && path.Length > "quests/".Length)
            {
                var segments = path.Split('/');
                if (segments.Length >= 2)
                {
                    var questId = segments[1];
                    var action = segments.Length >= 3 ? segments[2] : "";
                    await HandleQuestRoute(context, headerSessionId, questId, action, method);
                    return;
                }
            }

            // Handle headless routes with prefix matching
            if (path.StartsWith("headless/"))
            {
                await HandleHeadlessRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle flea routes with prefix matching
            if (path.StartsWith("flea/"))
            {
                await HandleFleaRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle parameterized player routes: player/{sid}/action
            if (path.StartsWith("player/") && !path.StartsWith("player/broadcast") && !path.StartsWith("player/give-all"))
            {
                var segments = path.Split('/');
                if (segments.Length >= 2)
                {
                    var targetSid = segments[1];
                    var action = segments.Length >= 3 ? segments[2] : "";
                    await HandlePlayerRoute(context, headerSessionId, targetSid, action, method);
                    return;
                }
            }

            switch (path)
            {
                case "banner" when method == "GET":
                    await ServeStaticFile(context, "banner.png");
                    break;
                case "auth" when method == "GET":
                    await HandleAuth(context, headerSessionId);
                    break;
                case "items" when method == "GET":
                    await HandleItems(context, headerSessionId);
                    break;
                case "categories" when method == "GET":
                    await HandleCategories(context, headerSessionId);
                    break;
                case "give" when method == "POST":
                    await HandleGive(context, headerSessionId);
                    break;
                case "presets" when method == "GET":
                    await HandlePresets(context, headerSessionId);
                    break;
                case "preset" when method == "POST":
                    await HandlePresetGive(context, headerSessionId);
                    break;
                // Dashboard routes
                case "dashboard/status" when method == "GET":
                    await HandleDashboardStatus(context, headerSessionId);
                    break;
                case "dashboard/players" when method == "GET":
                    await HandleDashboardPlayers(context, headerSessionId);
                    break;
                case "dashboard/economy" when method == "GET":
                    await HandleDashboardEconomy(context, headerSessionId);
                    break;
                case "dashboard/raids" when method == "GET":
                    await HandleDashboardRaids(context, headerSessionId);
                    break;
                case "dashboard/config" when method == "GET":
                    await HandleDashboardConfig(context, headerSessionId);
                    break;
                case "dashboard/broadcast" when method == "POST":
                    await HandleBroadcast(context, headerSessionId);
                    break;
                case "dashboard/send-urls" when method == "POST":
                    await HandleSendUrls(context, headerSessionId);
                    break;
                case "dashboard/activity" when method == "GET":
                    await HandleDashboardActivity(context, headerSessionId);
                    break;
                case "console" when method == "GET":
                case "console/history" when method == "GET":
                    await HandleConsole(context, headerSessionId);
                    break;
                case "headless-console" when method == "GET":
                    await HandleHeadlessConsole(context, headerSessionId);
                    break;
                // Quest browser routes
                case "quests" when method == "GET":
                    await HandleQuestList(context, headerSessionId);
                    break;
                // Player management routes
                case "players" when method == "GET":
                    await HandlePlayerRoster(context, headerSessionId);
                    break;
                case "players/bans" when method == "GET":
                    await HandleGetBanList(context, headerSessionId);
                    break;
                case "players/ban" when method == "POST":
                    await HandleBanPlayer(context, headerSessionId);
                    break;
                case "players/unban" when method == "POST":
                    await HandleUnbanPlayer(context, headerSessionId);
                    break;
                case "player/broadcast" when method == "POST":
                    await HandlePlayerBroadcastMail(context, headerSessionId);
                    break;
                case "player/give-all" when method == "POST":
                    await HandlePlayerGiveAll(context, headerSessionId);
                    break;
                default:
                    await WriteJson(context, 404, new { error = "Not found" });
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error handling {method} {path}: {ex.Message}");
            await WriteJson(context, 500, new { error = "Internal server error" });
        }
    }

    private async Task HandleAuth(HttpContext context, string headerSessionId)
    {
        if (string.IsNullOrEmpty(headerSessionId))
        {
            await WriteJson(context, 400, new AuthResponse
            {
                Authorized = false,
                Reason = "Missing X-Session-Id header"
            });
            return;
        }

        var authorized = accessControlService.IsAuthorized(headerSessionId);
        var profileName = authorized ? accessControlService.GetProfileName(headerSessionId) : "";

        await WriteJson(context, 200, new AuthResponse
        {
            Authorized = authorized,
            ProfileName = profileName,
            SessionId = headerSessionId,
            Reason = authorized ? null : "Access denied"
        });
    }

    private async Task HandleItems(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId))
            return;

        var query = context.Request.Query;
        var search = query["search"].FirstOrDefault();
        var category = query["category"].FirstOrDefault();
        var limit = int.TryParse(query["limit"].FirstOrDefault(), out var l) ? Math.Clamp(l, 1, 500) : 50;
        var offset = int.TryParse(query["offset"].FirstOrDefault(), out var o) ? Math.Max(o, 0) : 0;
        var sortField = query["sort"].FirstOrDefault();
        var sortDir = query["dir"].FirstOrDefault();

        var result = itemSearchService.SearchItems(search, category, limit, offset, sortField, sortDir);
        await WriteJson(context, 200, result);
    }

    private async Task HandleCategories(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId))
            return;

        var result = itemSearchService.GetCategories();
        await WriteJson(context, 200, result);
    }

    private async Task HandleGive(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId))
            return;

        var body = await ReadBody<GiveRequest>(context);
        if (body is null || body.Items.Count == 0)
        {
            await WriteJson(context, 400, new GiveResponse
            {
                Success = false,
                Error = "Invalid request body or empty items list"
            });
            return;
        }

        var result = itemGiveService.GiveItems(headerSessionId, body.Items);
        await WriteJson(context, 200, result);
    }

    private async Task HandlePresets(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId))
            return;

        var config = configService.GetConfig();
        var presets = config.Items.Presets.Select(p => new PresetInfo
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Items = p.Items
        }).ToList();

        await WriteJson(context, 200, new PresetListResponse { Presets = presets });
    }

    private async Task HandlePresetGive(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId))
            return;

        var body = await ReadBody<PresetGiveRequest>(context);
        if (body is null || string.IsNullOrEmpty(body.PresetId))
        {
            await WriteJson(context, 400, new PresetGiveResponse
            {
                Success = false,
                Error = "Invalid request body or missing presetId"
            });
            return;
        }

        var result = itemGiveService.GivePreset(headerSessionId, body.PresetId);
        await WriteJson(context, 200, result);
    }

    // ── Dashboard Handlers ──

    private async Task HandleDashboardStatus(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = serverStatsService.GetStatus();
        await WriteJson(context, 200, result);
    }

    private async Task HandleDashboardPlayers(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = playerStatsService.GetPlayerOverview();
        await WriteJson(context, 200, result);
    }

    private async Task HandleDashboardEconomy(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = playerStatsService.GetEconomy();
        await WriteJson(context, 200, result);
    }

    private async Task HandleDashboardRaids(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = raidTrackingService.GetRaidStats();
        await WriteJson(context, 200, result);
    }

    private async Task HandleDashboardConfig(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var config = configService.GetConfig().Dashboard;
        await WriteJson(context, 200, new DashboardConfigDto
        {
            RefreshIntervalSeconds = config.RefreshIntervalSeconds,
            ConsolePollingMs = config.ConsolePollingMs,
            HeadlessLogPath = config.HeadlessLogPath
        });
    }

    private async Task HandleDashboardActivity(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var query = context.Request.Query;
        var limit = int.TryParse(query["limit"].FirstOrDefault(), out var l) ? Math.Clamp(l, 1, 200) : 50;
        var offset = int.TryParse(query["offset"].FirstOrDefault(), out var o) ? Math.Max(o, 0) : 0;
        var typeFilter = query["type"].FirstOrDefault();
        var result = activityLogService.GetRecentActivity(limit, offset, typeFilter);
        await WriteJson(context, 200, result);
    }

    private async Task HandleConsole(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var query = context.Request.Query;
        var sinceStr = query["since"].FirstOrDefault();
        var linesStr = query["lines"].FirstOrDefault();

        if (!string.IsNullOrEmpty(linesStr) && int.TryParse(linesStr, out var lines))
        {
            var entries = consoleBufferService.GetHistory(Math.Clamp(lines, 1, 500));
            await WriteJson(context, 200, new ConsoleResponse
            {
                Entries = entries,
                Since = entries.Count > 0 ? entries[0].Timestamp : DateTime.UtcNow
            });
        }
        else
        {
            var since = DateTime.UtcNow.AddMinutes(-5); // default: last 5 minutes
            if (!string.IsNullOrEmpty(sinceStr) && DateTime.TryParse(sinceStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                since = parsed;
            }

            var entries = consoleBufferService.GetEntriesSince(since);
            await WriteJson(context, 200, new ConsoleResponse
            {
                Entries = entries,
                Since = since
            });
        }
    }

    private async Task HandleSendUrls(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        var urls = CommandCenterMod.ServerUrls;
        if (string.IsNullOrEmpty(urls))
        {
            await WriteJson(context, 500, new BroadcastResponse { Success = false, Error = "Server URLs not available" });
            return;
        }

        var profiles = saveServer.GetProfiles();
        var sent = 0;
        foreach (var (sid, _) in profiles)
        {
            try
            {
                mailSendService.SendSystemMessageToPlayer(sid.ToString(), urls, null);
                sent++;
            }
            catch { /* skip failed sends */ }
        }

        activityLogService.LogAction(ActionType.Broadcast, headerSessionId, "Sent Command Center URLs to all players");

        await WriteJson(context, 200, new BroadcastResponse { Success = true, RecipientCount = sent });
    }

    private async Task HandleHeadlessConsole(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        if (!headlessLogService.IsConfigured)
        {
            await WriteJson(context, 200, new ConsoleResponse
            {
                Entries = [],
                Since = DateTime.UtcNow,
                Configured = false
            });
            return;
        }

        var query = context.Request.Query;
        var sinceStr = query["since"].FirstOrDefault();
        var linesStr = query["lines"].FirstOrDefault();

        if (!string.IsNullOrEmpty(linesStr) && int.TryParse(linesStr, out var lines))
        {
            var entries = headlessLogService.GetHistory(Math.Clamp(lines, 1, 500));
            await WriteJson(context, 200, new ConsoleResponse
            {
                Entries = entries,
                Since = entries.Count > 0 ? entries[0].Timestamp : DateTime.UtcNow,
                Configured = true
            });
        }
        else
        {
            var since = DateTime.UtcNow.AddMinutes(-5);
            if (!string.IsNullOrEmpty(sinceStr) && DateTime.TryParse(sinceStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                since = parsed;
            }

            var entries = headlessLogService.GetEntriesSince(since);
            await WriteJson(context, 200, new ConsoleResponse
            {
                Entries = entries,
                Since = since,
                Configured = true
            });
        }
    }

    private async Task HandleBroadcast(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        var body = await ReadBody<BroadcastRequest>(context);
        if (body is null || string.IsNullOrWhiteSpace(body.Message))
        {
            await WriteJson(context, 400, new BroadcastResponse
            {
                Success = false,
                Error = "Message is required"
            });
            return;
        }

        var profiles = saveServer.GetProfiles();
        var sent = 0;
        foreach (var (sid, _) in profiles)
        {
            try
            {
                mailSendService.SendSystemMessageToPlayer(sid.ToString(), $"[Broadcast] {body.Message}", null);
                sent++;
            }
            catch { /* skip failed sends */ }
        }

        activityLogService.LogAction(ActionType.Broadcast, headerSessionId, body.Message);

        await WriteJson(context, 200, new BroadcastResponse
        {
            Success = true,
            RecipientCount = sent
        });
    }

    // ── Player Management Handlers ──

    private async Task HandlePlayerRoster(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var query = context.Request.Query;
        var search = query["search"].FirstOrDefault();
        var faction = query["faction"].FirstOrDefault();
        var sortField = query["sort"].FirstOrDefault();
        var sortDir = query["dir"].FirstOrDefault();
        var result = playerManagementService.GetRoster(search, faction, sortField, sortDir);
        await WriteJson(context, 200, result);
    }

    private async Task HandlePlayerRoute(HttpContext context, string headerSessionId, string targetSid, string action, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (action)
        {
            case "" when method == "GET":
                var profile = playerManagementService.GetProfile(targetSid);
                if (profile == null)
                    await WriteJson(context, 404, new { error = "Player not found" });
                else
                    await WriteJson(context, 200, profile);
                break;

            case "stash" when method == "GET":
                var q = context.Request.Query;
                var search = q["search"].FirstOrDefault();
                var limit = int.TryParse(q["limit"].FirstOrDefault(), out var lv) ? Math.Clamp(lv, 1, 200) : 50;
                var offset = int.TryParse(q["offset"].FirstOrDefault(), out var ov) ? Math.Max(ov, 0) : 0;
                var stash = playerManagementService.GetStash(targetSid, search, limit, offset);
                await WriteJson(context, 200, stash);
                break;

            case "mail" when method == "POST":
                var mailBody = await ReadBody<PlayerMailRequest>(context);
                if (mailBody == null)
                {
                    await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Invalid request body" });
                    return;
                }
                var mailResult = playerMailService.SendMail(headerSessionId, targetSid, mailBody);
                await WriteJson(context, 200, mailResult);
                break;

            case "give" when method == "POST":
                var giveBody = await ReadBody<PlayerGiveRequest>(context);
                if (giveBody == null)
                {
                    await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Invalid request body" });
                    return;
                }
                var giveResult = playerMailService.GiveToPlayer(headerSessionId, targetSid, giveBody.Items);
                await WriteJson(context, 200, giveResult);
                break;

            case "reset" when method == "POST":
                var resetBody = await ReadBody<PlayerResetRequest>(context);
                if (resetBody == null || resetBody.Categories.Count == 0)
                {
                    await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Categories required" });
                    return;
                }
                var resetResult = playerManagementService.ResetPlayer(headerSessionId, targetSid, resetBody.Categories);
                await WriteJson(context, 200, resetResult);
                break;

            case "modify" when method == "POST":
                var modBody = await ReadBody<PlayerModifyRequest>(context);
                if (modBody == null)
                {
                    await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Invalid request body" });
                    return;
                }
                var modResult = playerManagementService.ModifyPlayer(headerSessionId, targetSid, modBody);
                await WriteJson(context, 200, modResult);
                break;

            default:
                await WriteJson(context, 404, new { error = "Not found" });
                break;
        }
    }

    private async Task HandlePlayerBroadcastMail(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<BroadcastMailRequest>(context);
        if (body == null)
        {
            await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Invalid request body" });
            return;
        }
        var result = playerMailService.BroadcastMail(headerSessionId, body);
        await WriteJson(context, 200, result);
    }

    private async Task HandlePlayerGiveAll(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<PlayerGiveAllRequest>(context);
        if (body == null)
        {
            await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Invalid request body" });
            return;
        }
        var result = playerMailService.GiveToAll(headerSessionId, body.Items);
        await WriteJson(context, 200, result);
    }

    // ── Quest Browser Handlers ──

    private async Task HandleQuestList(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var query = context.Request.Query;
        var search = query["search"].FirstOrDefault();
        var map = query["map"].FirstOrDefault();
        var trader = query["trader"].FirstOrDefault();
        var sort = query["sort"].FirstOrDefault();
        var sortDir = query["dir"].FirstOrDefault();
        var result = questBrowserService.GetQuests(search, map, trader, sort, sortDir);
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestRoute(HttpContext context, string headerSessionId, string questId, string action, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (action)
        {
            case "" when method == "GET":
                var detail = questBrowserService.GetQuestDetail(questId);
                if (detail == null)
                    await WriteJson(context, 404, new { error = "Quest not found" });
                else
                    await WriteJson(context, 200, detail);
                break;

            case "state" when method == "POST":
                var body = await ReadBody<SetQuestStateRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.SessionId) || string.IsNullOrEmpty(body.Status))
                {
                    await WriteJson(context, 400, new SetQuestStateResponse { Success = false, Error = "SessionId and Status required" });
                    return;
                }
                var result = questBrowserService.SetQuestState(headerSessionId, questId, body.SessionId, body.Status);
                await WriteJson(context, 200, result);
                break;

            default:
                await WriteJson(context, 404, new { error = "Not found" });
                break;
        }
    }

    private async Task HandleGetBanList(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = accessControlService.GetBanList();
        await WriteJson(context, 200, result);
    }

    private async Task HandleBanPlayer(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<BanRequest>(context);
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId))
        {
            await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Session ID required" });
            return;
        }
        accessControlService.BanPlayer(body.SessionId, body.Reason);
        var name = accessControlService.GetProfileName(body.SessionId);
        activityLogService.LogAction(ActionType.PlayerBan, headerSessionId, $"Banned {name}: {body.Reason}");
        await WriteJson(context, 200, new PlayerActionResponse { Success = true, Message = $"Banned {name}" });
    }

    private async Task HandleUnbanPlayer(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<UnbanRequest>(context);
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId))
        {
            await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Session ID required" });
            return;
        }
        var name = accessControlService.GetProfileName(body.SessionId);
        accessControlService.UnbanPlayer(body.SessionId);
        activityLogService.LogAction(ActionType.PlayerUnban, headerSessionId, $"Unbanned {name}");
        await WriteJson(context, 200, new PlayerActionResponse { Success = true, Message = $"Unbanned {name}" });
    }

    // ── Headless Process Handlers ──

    private async Task HandleHeadlessRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (path)
        {
            case "headless/status" when method == "GET":
                await WriteJson(context, 200, headlessProcessService.GetStatus());
                break;
            case "headless/start" when method == "POST":
                var startResult = headlessProcessService.Start();
                if (startResult.Running)
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: started");
                await WriteJson(context, 200, startResult);
                break;
            case "headless/stop" when method == "POST":
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: stopped");
                await WriteJson(context, 200, headlessProcessService.Stop());
                break;
            case "headless/restart" when method == "POST":
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: restarted");
                await WriteJson(context, 200, headlessProcessService.Restart());
                break;
            case "headless/config" when method == "POST":
            {
                var body = await ReadBody<HeadlessConfigUpdateRequest>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var config = configService.GetConfig().Headless;
                if (body.AutoStart.HasValue) config.AutoStart = body.AutoStart.Value;
                if (body.AutoStartDelaySec.HasValue) config.AutoStartDelaySec = Math.Clamp(body.AutoStartDelaySec.Value, 1, 300);
                if (body.AutoRestart.HasValue) config.AutoRestart = body.AutoRestart.Value;
                if (body.ProfileId != null) config.ProfileId = body.ProfileId.Trim();
                configService.SaveConfig();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: config updated");
                await WriteJson(context, 200, headlessProcessService.GetStatus());
                break;
            }
            default:
                await WriteJson(context, 404, new { error = "Not found" });
                break;
        }
    }

    // ── Flea Market Handlers ──

    private async Task HandleFleaRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (path)
        {
            case "flea/config" when method == "GET":
            {
                var config = fleaPriceService.GetConfig();
                await WriteJson(context, 200, config);
                break;
            }
            case "flea/config" when method == "POST":
            {
                var body = await ReadBody<FleaConfig>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var result = fleaPriceService.UpdateFullConfig(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Flea: full config updated");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/config/global" when method == "POST":
            {
                var body = await ReadBody<FleaGlobalUpdateRequest>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var result = fleaPriceService.UpdateGlobalMultiplier(body.BuyMultiplier);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Flea: global buy={body.BuyMultiplier:F2}");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/config/market" when method == "POST":
            {
                var body = await ReadBody<FleaMarketSettingsRequest>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var result = fleaPriceService.UpdateMarketSettings(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Flea: market settings updated");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/config/category" when method == "POST":
            {
                var body = await ReadBody<FleaCategoryUpdateRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.Category))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body or missing category" });
                    return;
                }
                var result = fleaPriceService.UpdateCategoryMultiplier(body.Category, body.BuyMultiplier);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Flea: category '{body.Category}' buy={body.BuyMultiplier:F2}");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/config/item" when method == "POST":
            {
                var body = await ReadBody<FleaItemOverrideRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.TemplateId))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body or missing templateId" });
                    return;
                }
                var result = fleaPriceService.SetItemOverride(body.TemplateId, body.Name, body.BuyMultiplier);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Flea: item override '{body.Name}' ({body.TemplateId})");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/regenerate" when method == "POST":
            {
                var result = offerRegenerationService.RegenerateOffers();
                if (result.Success)
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Flea: regenerated {result.OfferCount} offers in {result.DurationMs}ms");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/categories" when method == "GET":
            {
                var result = fleaPriceService.GetCategories();
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/status" when method == "GET":
            {
                var result = offerRegenerationService.GetStatus();
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/debug" when method == "GET":
            {
                var result = offerRegenerationService.GetDebugInfo();
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/debug/tax" when method == "GET":
            {
                var globals = databaseService.GetGlobals();
                var ragfairConfig = configServer.GetConfig<RagfairConfig>();
                var fleaCfg = fleaPriceService.GetConfig();
                await WriteJson(context, 200, new
                {
                    configTaxMultiplier = fleaCfg.FleaTaxMultiplier,
                    globalsCommunityItemTax = globals.Configuration.RagFair.CommunityItemTax,
                    globalsCommunityRequirementTax = globals.Configuration.RagFair.CommunityRequirementTax,
                    ragfairConfigOfferListingTaxMultiplier = ragfairConfig.OfferListingTaxMultiplier
                });
                break;
            }
            case "flea/presets" when method == "GET":
            {
                var result = fleaPriceService.ListPresets();
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/presets" when method == "POST":
            {
                var body = await ReadBody<FleaSavePresetRequest>(context);
                if (body == null || string.IsNullOrWhiteSpace(body.Name))
                {
                    await WriteJson(context, 400, new { error = "Missing preset name" });
                    return;
                }
                var result = fleaPriceService.SavePreset(body.Name.Trim(), body.Description?.Trim() ?? "");
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Flea: saved preset '{body.Name}'");
                await WriteJson(context, 200, result);
                break;
            }
            case "flea/presets/load" when method == "POST":
            {
                var body = await ReadBody<FleaLoadPresetRequest>(context);
                if (body == null || string.IsNullOrWhiteSpace(body.Name))
                {
                    await WriteJson(context, 400, new { error = "Missing preset name" });
                    return;
                }
                var preset = fleaPriceService.LoadPreset(body.Name.Trim());
                if (preset == null)
                {
                    await WriteJson(context, 404, new { error = "Preset not found" });
                    return;
                }
                await WriteJson(context, 200, preset);
                break;
            }
            case "flea/presets/import" when method == "POST":
            {
                var body = await ReadBody<FleaPreset>(context);
                if (body == null || string.IsNullOrWhiteSpace(body.Name))
                {
                    await WriteJson(context, 400, new { error = "Invalid preset data" });
                    return;
                }
                var result = fleaPriceService.ImportPreset(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Flea: imported preset '{body.Name}'");
                await WriteJson(context, 200, result);
                break;
            }
            default:
            {
                // Handle parameterized flea routes — presets delete
                if (method == "DELETE" && path.StartsWith("flea/presets/"))
                {
                    var presetName = Uri.UnescapeDataString(path.Substring("flea/presets/".Length));
                    if (string.IsNullOrEmpty(presetName))
                    {
                        await WriteJson(context, 400, new { error = "Missing preset name" });
                        return;
                    }
                    var deleted = fleaPriceService.DeletePreset(presetName);
                    if (deleted)
                        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Flea: deleted preset '{presetName}'");
                    await WriteJson(context, 200, new { success = deleted });
                    break;
                }

                // Handle parameterized flea routes
                // DELETE flea/config/item/{templateId}
                if (method == "DELETE" && path.StartsWith("flea/config/item/"))
                {
                    var templateId = path.Substring("flea/config/item/".Length);
                    if (string.IsNullOrEmpty(templateId))
                    {
                        await WriteJson(context, 400, new { error = "Missing templateId" });
                        return;
                    }
                    var removed = fleaPriceService.RemoveItemOverride(templateId);
                    if (removed)
                        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                            $"Flea: removed item override {templateId}");
                    await WriteJson(context, 200, new { success = removed });
                    break;
                }

                // GET flea/preview?templateId={id}
                if (method == "GET" && path == "flea/preview")
                {
                    var templateId = context.Request.Query["templateId"].FirstOrDefault();
                    if (string.IsNullOrEmpty(templateId))
                    {
                        await WriteJson(context, 400, new { error = "Missing templateId query parameter" });
                        return;
                    }
                    var preview = fleaPriceService.GetPreview(templateId);
                    if (preview == null)
                    {
                        await WriteJson(context, 404, new { error = "Item not found" });
                        return;
                    }
                    await WriteJson(context, 200, preview);
                    break;
                }

                // GET flea/items/search?q={query}
                if (method == "GET" && path == "flea/items/search")
                {
                    var q = context.Request.Query["q"].FirstOrDefault() ?? "";
                    var result = fleaPriceService.SearchItems(q);
                    await WriteJson(context, 200, result);
                    break;
                }

                await WriteJson(context, 404, new { error = "Not found" });
                break;
            }
        }
    }

    private async Task ServeHtml(HttpContext context)
    {
        if (_cachedHtml is null)
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var htmlPath = Path.Combine(modPath, "res", "commandcenter.html");

            if (!File.Exists(htmlPath))
            {
                await WriteJson(context, 404, new { error = "UI file not found" });
                return;
            }

            _cachedHtml = await File.ReadAllBytesAsync(htmlPath);
            logger.Info($"ZSlayerCommandCenter: Loaded UI from {htmlPath} ({_cachedHtml.Length} bytes)");
        }

        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.Body.WriteAsync(_cachedHtml);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".json"] = "application/json",
        [".woff2"] = "font/woff2",
        [".woff"] = "font/woff",
    };

    private static bool IsStaticFile(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && MimeTypes.ContainsKey(ext);
    }

    private async Task ServeStaticFile(HttpContext context, string relativePath)
    {
        // Prevent directory traversal
        if (relativePath.Contains("..") || relativePath.Contains('\\'))
        {
            await WriteJson(context, 400, new { error = "Invalid path" });
            return;
        }

        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var filePath = Path.Combine(modPath, "res", relativePath);

        if (!File.Exists(filePath))
        {
            await WriteJson(context, 404, new { error = "File not found" });
            return;
        }

        var ext = Path.GetExtension(filePath);
        var contentType = MimeTypes.GetValueOrDefault(ext, "application/octet-stream");

        context.Response.StatusCode = 200;
        context.Response.ContentType = contentType;
        context.Response.Headers["Cache-Control"] = "public, max-age=3600";
        var bytes = await File.ReadAllBytesAsync(filePath);
        await context.Response.Body.WriteAsync(bytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private async Task<bool> ValidateAccess(HttpContext context, string headerSessionId)
    {
        if (string.IsNullOrEmpty(headerSessionId))
        {
            await WriteJson(context, 401, new { error = "Missing X-Session-Id header" });
            return false;
        }

        if (!accessControlService.IsAuthorized(headerSessionId))
        {
            await WriteJson(context, 403, new { error = "Access denied" });
            return false;
        }

        return true;
    }

    private static void SetCorsHeaders(HttpContext context)
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, DELETE, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Session-Id";
    }

    private static async Task WriteJson<T>(HttpContext context, int statusCode, T data)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        await context.Response.Body.WriteAsync(json);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private static async Task<T?> ReadBody<T>(HttpContext context) where T : class
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
