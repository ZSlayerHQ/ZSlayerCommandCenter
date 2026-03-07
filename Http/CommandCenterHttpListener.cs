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
    QuestDiscoveryService questDiscoveryService,
    QuestOverrideService questOverrideService,
    FleaPriceService fleaPriceService,
    OfferRegenerationService offerRegenerationService,
    HeadlessLogService headlessLogService,
    WatchdogManager watchdogManager,
    FikaConfigService fikaConfigService,
    TelemetryService telemetryService,
    TraderApplyService traderApplyService,
    TraderDiscoveryService traderDiscoveryService,
    PlayerBuildService playerBuildService,
    ProgressionControlService progressionControlService,
    SkillEditorService skillEditorService,
    DatabaseService databaseService,
    ConfigServer configServer,
    SaveServer saveServer,
    ProfileActivityService profileActivityService,
    ProfileBackupService backupService,
    WipeService wipeService,
    EventService eventService,
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

        // Redirect /zslayer/cc → /zslayer/cc/ so relative URLs resolve correctly
        if (rawPath.Equals(BasePath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 301;
            context.Response.Headers["Location"] = BasePath + "/";
            await context.Response.StartAsync();
            await context.Response.CompleteAsync();
            return;
        }

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
            // Must exclude known sub-routes handled by the switch-case below
            if (path.StartsWith("quests/") && path.Length > "quests/".Length)
            {
                var segments = path.Split('/');
                if (segments.Length >= 2)
                {
                    var firstSeg = segments[1];
                    // Skip known fixed routes — let them fall through to the switch-case
                    if (firstSeg is not ("config" or "apply" or "reset" or "status" or "tree" or "traders" or "locations" or "presets" or "bulk-state" or "reward-summary" or "objective-types"))
                    {
                        var questId = firstSeg;
                        var action = segments.Length >= 3 ? segments[2] : "";
                        await HandleQuestRoute(context, headerSessionId, questId, action, method);
                        return;
                    }
                }
            }

            // Handle telemetry routes (POST = no auth, GET = auth required)
            if (path.StartsWith("telemetry/"))
            {
                await HandleTelemetryRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle watchdog routes (proxy to watchdog exe)
            if (path.StartsWith("watchdog/"))
            {
                await HandleWatchdogRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle headless routes with prefix matching
            if (path.StartsWith("headless/"))
            {
                await HandleHeadlessRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle progression routes
            if (path.StartsWith("progression/") || path == "progression")
            {
                await HandleProgressionRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle skill editing routes
            if (path.StartsWith("skills/") || path == "skills")
            {
                await HandleSkillsRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle FIKA config routes
            if (path.StartsWith("fika/"))
            {
                await HandleFikaRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle backup/wipe routes
            if (path.StartsWith("backups/") || path == "backups" || path.StartsWith("wipe/"))
            {
                await HandleBackupRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle scheduler/event routes
            if (path.StartsWith("scheduler/") || path == "scheduler")
            {
                await HandleSchedulerRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle flea routes with prefix matching
            if (path.StartsWith("flea/"))
            {
                await HandleFleaRoute(context, headerSessionId, path, method);
                return;
            }

            // Handle trader routes with prefix matching
            if (path.StartsWith("traders/") || path == "traders")
            {
                await HandleTraderRoute(context, headerSessionId, path, method);
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

            // Public endpoints (no auth required)
            if (path == "profiles" && method == "GET")
            {
                await HandleProfiles(context);
                return;
            }

            if (path == "profile-icons" && method == "GET")
            {
                await HandleProfileIcons(context);
                return;
            }

            if (path == "profile-icon" && method == "POST")
            {
                await HandleProfileIconSet(context);
                return;
            }

            if (path == "server-vitals" && method == "GET")
            {
                await HandleServerVitals(context);
                return;
            }

            switch (path)
            {
                case "banner" when method == "GET":
                    await ServeStaticFile(context, "banner.svg");
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
                case "player-builds" when method == "GET":
                    await HandlePlayerBuilds(context, headerSessionId);
                    break;
                case "player-build/give" when method == "POST":
                    await HandlePlayerBuildGive(context, headerSessionId);
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
                case "dashboard/my-raids" when method == "GET":
                    await HandleDashboardMyRaids(context, headerSessionId);
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
                case "headless-console/sources" when method == "GET":
                {
                    if (!await ValidateAccess(context, headerSessionId)) break;
                    await WriteJson(context, 200, new { sources = headlessLogService.GetSources() });
                    break;
                }
                // Quest editor routes
                case "quests" when method == "GET":
                    await HandleQuestList(context, headerSessionId);
                    break;
                case "quests/config" when method == "GET":
                    await HandleQuestConfig(context, headerSessionId);
                    break;
                case "quests/config/global" when method == "POST":
                    await HandleQuestGlobalConfig(context, headerSessionId);
                    break;
                case "quests/apply" when method == "POST":
                    await HandleQuestApply(context, headerSessionId);
                    break;
                case "quests/reset" when method == "POST":
                    await HandleQuestReset(context, headerSessionId);
                    break;
                case "quests/status" when method == "GET":
                    await HandleQuestStatus(context, headerSessionId);
                    break;
                case "quests/tree" when method == "GET":
                    await HandleQuestTree(context, headerSessionId);
                    break;
                case "quests/traders" when method == "GET":
                    await HandleQuestTraders(context, headerSessionId);
                    break;
                case "quests/locations" when method == "GET":
                    await HandleQuestLocations(context, headerSessionId);
                    break;
                case "quests/objective-types" when method == "GET":
                    await HandleQuestObjectiveTypes(context, headerSessionId);
                    break;
                case "quests/reward-summary" when method == "GET":
                    await HandleQuestRewardSummary(context, headerSessionId);
                    break;
                case "quests/presets" when method == "GET":
                    await HandleQuestPresetList(context, headerSessionId);
                    break;
                case "quests/presets/save" when method == "POST":
                    await HandleQuestPresetSave(context, headerSessionId);
                    break;
                case "quests/presets/load" when method == "POST":
                    await HandleQuestPresetLoad(context, headerSessionId);
                    break;
                case "quests/presets/upload" when method == "POST":
                    await HandleQuestPresetUpload(context, headerSessionId);
                    break;
                case "quests/bulk-state" when method == "POST":
                    await HandleQuestBulkState(context, headerSessionId);
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
                {
                    // DELETE quests/presets/{name}
                    if (method == "DELETE" && path.StartsWith("quests/presets/"))
                    {
                        if (!await ValidateAccess(context, headerSessionId)) return;
                        var presetName = Uri.UnescapeDataString(path["quests/presets/".Length..]);
                        if (string.IsNullOrEmpty(presetName))
                        {
                            await WriteJson(context, 400, new { error = "Missing preset name" });
                            break;
                        }
                        var deleted = questOverrideService.DeleteQuestPreset(presetName);
                        if (deleted)
                            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Quests: deleted preset '{presetName}'");
                        await WriteJson(context, 200, new { success = deleted });
                        break;
                    }

                    // GET quests/presets/{name}/download
                    if (method == "GET" && path.StartsWith("quests/presets/") && path.EndsWith("/download"))
                    {
                        if (!await ValidateAccess(context, headerSessionId)) return;
                        var segment = path["quests/presets/".Length..^"/download".Length];
                        var presetName = Uri.UnescapeDataString(segment);
                        if (string.IsNullOrEmpty(presetName))
                        {
                            await WriteJson(context, 400, new { error = "Missing preset name" });
                            break;
                        }
                        var json = questOverrideService.DownloadQuestPreset(presetName);
                        if (json == null)
                        {
                            await WriteJson(context, 404, new { error = "Preset not found" });
                            break;
                        }
                        await WriteJson(context, 200, new { success = true, json });
                        break;
                    }

                    await WriteJson(context, 404, new { error = "Not found" });
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error handling {method} {path}: {ex.Message}\n{ex.StackTrace}");
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
                Reason = "Missing profile ID"
            });
            return;
        }

        var authorized = accessControlService.IsAuthorized(headerSessionId);

        // Check password if configured
        if (authorized)
        {
            var configPassword = configService.GetConfig().Access.Password;
            if (!string.IsNullOrEmpty(configPassword))
            {
                var headerPassword = context.Request.Headers["X-Password"].FirstOrDefault() ?? "";
                if (headerPassword != configPassword)
                {
                    await WriteJson(context, 200, new AuthResponse
                    {
                        Authorized = false,
                        Reason = "Invalid password"
                    });
                    return;
                }
            }
        }

        var profileName = authorized ? accessControlService.GetProfileName(headerSessionId) : "";

        await WriteJson(context, 200, new AuthResponse
        {
            Authorized = authorized,
            ProfileName = profileName,
            SessionId = headerSessionId,
            Reason = authorized ? null : "Access denied"
        });
    }

    private async Task HandleProfiles(HttpContext context)
    {
        var config = configService.GetConfig();
        var hasPassword = !string.IsNullOrEmpty(config.Access.Password);

        var profiles = saveServer.GetProfiles();
        var activeIds = profileActivityService.GetActiveProfileIdsWithinMinutes(5);
        var activeSet = new HashSet<string>(activeIds.Select(id => id.ToString()));
        var entries = new List<ProfileEntry>();

        foreach (var (sid, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null) continue;

            var sidStr = sid.ToString();

            // Extract raid stats from OverallCounters
            int totalRaids = 0, survived = 0;
            var counters = pmc.Stats?.Eft?.OverallCounters?.Items;
            if (counters != null)
            {
                foreach (var counter in counters)
                {
                    if (counter.Key == null || counter.Key.Count == 0) continue;
                    var k = counter.Key;
                    var val = (int)(counter.Value ?? 0);

                    if (k.Count > 1)
                    {
                        if (k.Contains("Sessions") && k.Contains("Pmc")) totalRaids = val;
                        else if (k.Contains("ExitStatus") && k.Contains("Survived")) survived = val;
                    }
                }
            }

            var survivalRate = totalRaids > 0 ? (int)Math.Round(100.0 * survived / totalRaids) : 0;

            entries.Add(new ProfileEntry
            {
                SessionId = sidStr,
                Nickname = pmc.Info.Nickname ?? "Unknown",
                Side = pmc.Info.Side ?? "",
                Level = pmc.Info.Level ?? 0,
                AvatarIcon = config.ProfileAvatars.GetValueOrDefault(sidStr),
                TotalRaids = totalRaids,
                SurvivalRate = survivalRate,
                IsOnline = activeSet.Contains(sidStr)
            });
        }

        await WriteJson(context, 200, new ProfileListResponse
        {
            Profiles = entries,
            HasPassword = hasPassword,
            ModVersion = ModMetadata.StaticVersion
        });
    }

    private async Task HandleProfileIcons(HttpContext context)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var dir = Path.Combine(modPath, "res", "Profile Icons");
        var icons = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.png").Select(Path.GetFileName).ToList()
            : new List<string?>();
        await WriteJson(context, 200, new { icons });
    }

    private async Task HandleProfileIconSet(HttpContext context)
    {
        var body = await ReadBody<ProfileIconSetRequest>(context);
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId) || string.IsNullOrWhiteSpace(body.Icon))
        {
            await WriteJson(context, 400, new { error = "sessionId and icon required" });
            return;
        }

        // Validate the icon file exists
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var iconPath = Path.Combine(modPath, "res", "Profile Icons", body.Icon);
        if (!File.Exists(iconPath))
        {
            await WriteJson(context, 400, new { error = "Icon not found" });
            return;
        }

        var config = configService.GetConfig();
        config.ProfileAvatars[body.SessionId] = body.Icon;
        configService.SaveConfig();

        await WriteJson(context, 200, new { success = true, icon = body.Icon });
    }

    private async Task HandleServerVitals(HttpContext context)
    {
        var overview = playerStatsService.GetPlayerOverview();
        var headlessId = configService.GetConfig().Headless.ProfileId;
        var onlinePlayers = overview.Players.Where(p => p.Online && !p.IsHeadless).ToList();
        var online = onlinePlayers.Count;
        var total = overview.Players.Count(p => !p.IsHeadless);

        // Active raid from telemetry
        var telemetry = telemetryService.GetCurrent();
        object? activeRaid = null;
        var inRaidIds = new HashSet<string>();
        string raidMap = "";

        if (telemetry.RaidActive && telemetry.RaidState != null)
        {
            var rs = telemetry.RaidState;
            raidMap = rs.Map ?? "";
            var elapsedSec = rs.RaidTimer - rs.RaidTimeLeft;
            activeRaid = new
            {
                map = rs.Map,
                playersInRaid = rs.Players?.PmcAlive ?? 0,
                timeElapsedSec = Math.Max(0, elapsedSec),
                status = rs.Status
            };

            // Collect profileIds of players currently in raid
            foreach (var p in telemetry.Players)
            {
                if (!string.IsNullOrEmpty(p.ProfileId))
                    inRaidIds.Add(p.ProfileId);
            }
        }

        // Build online player list with status
        var playerList = onlinePlayers.Select(p => new
        {
            nickname = p.Nickname,
            status = inRaidIds.Contains(p.SessionId) ? "In Raid" : "In Stash",
            map = inRaidIds.Contains(p.SessionId) ? raidMap : ""
        }).ToList();

        // Server uptime
        var status = serverStatsService.GetStatus();

        // Headless status from Watchdog
        var headlessWatchdogs = watchdogManager.GetWatchdogsForTarget("headlessClient");
        var headlessRunning = headlessWatchdogs.Any(w => w.HeadlessClient?.Running == true);
        var headlessUptime = headlessWatchdogs
            .FirstOrDefault(w => w.HeadlessClient?.Running == true)?.HeadlessClient?.Uptime ?? "";

        var headlessInstances = watchdogManager.GetHeadlessInstances();

        await WriteJson(context, 200, new
        {
            playersOnline = online,
            totalProfiles = total,
            activeRaid,
            onlinePlayers = playerList,
            serverUptime = status.Uptime,
            headlessRunning,
            headlessUptime,
            headlessInstances
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

    // ── Player Build Handlers ──

    private async Task HandlePlayerBuilds(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = playerBuildService.GetAllBuilds();
        await WriteJson(context, 200, result);
    }

    private async Task HandlePlayerBuildGive(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        var body = await ReadBody<PlayerBuildGiveRequest>(context);
        if (body is null || string.IsNullOrEmpty(body.BuildId) || string.IsNullOrEmpty(body.BuildType))
        {
            await WriteJson(context, 400, new PresetGiveResponse
            {
                Success = false,
                Error = "buildId and buildType required"
            });
            return;
        }

        var result = playerBuildService.GivePlayerBuild(headerSessionId, body.BuildId, body.BuildType);
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

        // Compute in-raid player IDs from telemetry (same pattern as HandleServerVitals)
        var inRaidIds = new HashSet<string>();
        var telemetry = telemetryService.GetCurrent();
        if (telemetry.RaidActive && telemetry.RaidState != null)
        {
            foreach (var p in telemetry.Players)
            {
                if (!string.IsNullOrEmpty(p.ProfileId))
                    inRaidIds.Add(p.ProfileId);
            }
        }

        var result = playerStatsService.GetPlayerOverview(inRaidIds);
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
        var result = playerStatsService.GetServerRaidStats();
        await WriteJson(context, 200, result);
    }

    private async Task HandleDashboardMyRaids(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = playerStatsService.GetPlayerRaidStats(headerSessionId);
        if (result == null)
        {
            await WriteJson(context, 404, new { error = "Profile not found" });
            return;
        }
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
        var search = query["search"].FirstOrDefault();
        var result = activityLogService.GetRecentActivity(limit, offset, typeFilter, search);
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
            catch (Exception ex)
            {
                logger.Debug($"ZSlayerCommandCenter: Failed URL mail send to session {sid}: {ex.Message}");
            }
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
        var sourceFilter = query["source"].FirstOrDefault();

        if (!string.IsNullOrEmpty(linesStr) && int.TryParse(linesStr, out var lines))
        {
            var entries = headlessLogService.GetHistory(Math.Clamp(lines, 1, 500), sourceFilter);
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

            var entries = headlessLogService.GetEntriesSince(since, sourceFilter);
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
            catch (Exception ex)
            {
                logger.Debug($"ZSlayerCommandCenter: Failed broadcast send to session {sid}: {ex.Message}");
            }
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

            case "stats" when method == "GET":
                var stats = playerManagementService.GetPlayerFullStats(targetSid);
                if (stats == null)
                    await WriteJson(context, 404, new { error = "Player not found" });
                else
                    await WriteJson(context, 200, stats);
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

            case "set-trader-loyalty" when method == "POST":
                var loyaltyBody = await ReadBody<SetTraderLoyaltyRequest>(context);
                if (loyaltyBody == null || string.IsNullOrEmpty(loyaltyBody.TraderId))
                {
                    await WriteJson(context, 400, new PlayerActionResponse { Success = false, Error = "Invalid request body" });
                    return;
                }
                var loyaltyResult = playerManagementService.SetTraderLoyalty(headerSessionId, targetSid, loyaltyBody.TraderId, loyaltyBody.Level);
                await WriteJson(context, 200, loyaltyResult);
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
        var type = query["type"].FirstOrDefault();
        var sort = query["sort"].FirstOrDefault();
        var sortDir = query["dir"].FirstOrDefault();
        var limitStr = query["limit"].FirstOrDefault();
        var offsetStr = query["offset"].FirstOrDefault();

        int limit = int.TryParse(limitStr, out var limVal) ? Math.Clamp(limVal, 1, 10000) : 50;
        int offset = int.TryParse(offsetStr, out var offVal) ? Math.Max(0, offVal) : 0;

        var config = configService.GetConfig().Quests;
        var result = questDiscoveryService.GetQuests(config, search, map, trader, type, sort, sortDir, limit, offset, headerSessionId);
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestRoute(HttpContext context, string headerSessionId, string questId, string action, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (action)
        {
            case "" when method == "GET":
            {
                var config = configService.GetConfig().Quests;
                var detail = questDiscoveryService.GetQuestDetail(questId, config);
                if (detail == null)
                    await WriteJson(context, 404, new { error = "Quest not found" });
                else
                    await WriteJson(context, 200, detail);
                break;
            }

            case "override" when method == "POST":
            {
                var body = await ReadBody<QuestOverrideRequest>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }

                var config = configService.GetConfig().Quests;
                var questOverride = config.QuestOverrides.GetValueOrDefault(questId) ?? new QuestOverrideConfig();

                // Merge incoming overrides
                if (body.TraderIdOverride != null) questOverride.TraderIdOverride = body.TraderIdOverride;
                if (body.LevelRequirementOverride.HasValue) questOverride.LevelRequirementOverride = body.LevelRequirementOverride;
                if (body.RemovedPrerequisites != null) questOverride.RemovedPrerequisites = body.RemovedPrerequisites;
                if (body.ConditionOverrides != null)
                {
                    foreach (var (k, v) in body.ConditionOverrides)
                        questOverride.ConditionOverrides[k] = v;
                }
                if (body.RewardOverrides != null)
                {
                    foreach (var (k, v) in body.RewardOverrides)
                        questOverride.RewardOverrides[k] = v;
                }
                if (body.RemovedRewards != null) questOverride.RemovedRewards = body.RemovedRewards;
                if (body.LocaleOverrides != null)
                {
                    foreach (var (k, v) in body.LocaleOverrides)
                        questOverride.LocaleOverrides[k] = v;
                }

                config.QuestOverrides[questId] = questOverride;
                configService.SaveConfig();
                await WriteJson(context, 200, new { success = true });
                break;
            }

            case "override" when method == "DELETE":
            {
                var config = configService.GetConfig().Quests;
                config.QuestOverrides.Remove(questId);
                configService.SaveConfig();
                await WriteJson(context, 200, new { success = true });
                break;
            }

            case "disable" when method == "POST":
            {
                var config = configService.GetConfig().Quests;
                if (!config.DisabledQuests.Contains(questId))
                    config.DisabledQuests.Add(questId);
                configService.SaveConfig();
                await WriteJson(context, 200, new { success = true });
                break;
            }

            case "enable" when method == "POST":
            {
                var config = configService.GetConfig().Quests;
                config.DisabledQuests.Remove(questId);
                configService.SaveConfig();
                await WriteJson(context, 200, new { success = true });
                break;
            }

            case "state" when method == "POST":
            {
                var body = await ReadBody<SetQuestStateRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.SessionId) || string.IsNullOrEmpty(body.Status))
                {
                    await WriteJson(context, 400, new { error = "SessionId and Status required" });
                    return;
                }
                var result = questOverrideService.SetPlayerQuestState(headerSessionId, questId, body.SessionId, body.Status);
                await WriteJson(context, 200, result);
                break;
            }

            case "players" when method == "GET":
            {
                var result = questDiscoveryService.GetPlayerQuestStatuses(questId);
                await WriteJson(context, 200, result);
                break;
            }

            default:
                await WriteJson(context, 404, new { error = "Not found" });
                break;
        }
    }

    private async Task HandleQuestConfig(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var config = configService.GetConfig().Quests;
        await WriteJson(context, 200, config);
    }

    private async Task HandleQuestGlobalConfig(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<QuestGlobalConfigRequest>(context);
        if (body == null)
        {
            await WriteJson(context, 400, new { error = "Invalid request body" });
            return;
        }

        var config = configService.GetConfig().Quests;
        if (body.GlobalXpMultiplier.HasValue) config.GlobalXpMultiplier = Math.Clamp(body.GlobalXpMultiplier.Value, 0.0, 100.0);
        if (body.GlobalStandingMultiplier.HasValue) config.GlobalStandingMultiplier = Math.Clamp(body.GlobalStandingMultiplier.Value, 0.0, 10.0);
        if (body.GlobalItemRewardMultiplier.HasValue) config.GlobalItemRewardMultiplier = Math.Clamp(body.GlobalItemRewardMultiplier.Value, 0.1, 10.0);
        if (body.GlobalKillCountMultiplier.HasValue) config.GlobalKillCountMultiplier = Math.Clamp(body.GlobalKillCountMultiplier.Value, 0.1, 10.0);
        if (body.GlobalHandoverCountMultiplier.HasValue) config.GlobalHandoverCountMultiplier = Math.Clamp(body.GlobalHandoverCountMultiplier.Value, 0.1, 10.0);
        if (body.RemoveFIRRequirements.HasValue) config.RemoveFIRRequirements = body.RemoveFIRRequirements.Value;
        if (body.GlobalLevelRequirementShift.HasValue) config.GlobalLevelRequirementShift = Math.Clamp(body.GlobalLevelRequirementShift.Value, -50, 0);

        configService.SaveConfig();
        await WriteJson(context, 200, new { success = true });
    }

    private async Task HandleQuestApply(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = questOverrideService.ApplyConfig();
        if (result.Success)
        {
            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                $"Quests: applied — {result.QuestsModified} quests, {result.ObjectivesModified} objectives, {result.RewardsModified} rewards in {result.ApplyTimeMs}ms");
        }
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestReset(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = questOverrideService.ResetAll();
        if (result.Success)
        {
            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                $"Quests: reset all — {result.QuestsModified} quests restored");
        }
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestStatus(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var playerSessionId = context.Request.Query["sessionId"].FirstOrDefault();
        var result = questOverrideService.GetStatus(playerSessionId);
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestTree(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var config = configService.GetConfig().Quests;
        var result = questDiscoveryService.GetQuestTree(config);
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestTraders(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = questDiscoveryService.GetTraderList();
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestLocations(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = questDiscoveryService.GetLocationList();
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestObjectiveTypes(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = questDiscoveryService.GetObjectiveTypeList();
        await WriteJson(context, 200, new { types = result });
    }

    private async Task HandleQuestRewardSummary(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var config = configService.GetConfig().Quests;
        var result = questDiscoveryService.GetRewardSummary(config);
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestPresetList(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var result = questOverrideService.ListQuestPresets();
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestPresetSave(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<QuestPresetSaveRequest>(context);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await WriteJson(context, 400, new { error = "Missing preset name" });
            return;
        }
        var result = questOverrideService.SaveQuestPreset(body.Name.Trim(), body.Description?.Trim() ?? "");
        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Quests: saved preset '{body.Name}'");
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestPresetLoad(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<QuestPresetLoadRequest>(context);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await WriteJson(context, 400, new { error = "Missing preset name" });
            return;
        }
        var result = questOverrideService.LoadAndApplyQuestPreset(body.Name.Trim());
        if (result.Success)
            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Quests: loaded preset '{body.Name}'");
        await WriteJson(context, 200, result);
    }

    private async Task HandleQuestPresetUpload(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<QuestPresetUploadRequest>(context);
        if (body == null || string.IsNullOrWhiteSpace(body.PresetJson))
        {
            await WriteJson(context, 400, new { error = "Missing preset JSON" });
            return;
        }
        try
        {
            var result = questOverrideService.UploadQuestPreset(body.Name?.Trim() ?? "", body.PresetJson);
            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Quests: imported preset '{result.Name}'");
            await WriteJson(context, 200, new { success = true, name = result.Name });
        }
        catch (Exception ex)
        {
            await WriteJson(context, 400, new { error = $"Invalid preset JSON: {ex.Message}" });
        }
    }

    private async Task HandleQuestBulkState(HttpContext context, string headerSessionId)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;
        var body = await ReadBody<BulkQuestStateRequest>(context);
        if (body == null || string.IsNullOrEmpty(body.SessionId) || string.IsNullOrEmpty(body.Status))
        {
            await WriteJson(context, 400, new { error = "SessionId and Status required" });
            return;
        }
        var result = questOverrideService.BulkSetQuestState(headerSessionId, body.SessionId, body.Status, body.QuestIds);
        await WriteJson(context, 200, result);
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

    // ── Telemetry Handlers ──

    private async Task HandleTelemetryRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        // POST endpoints — from headless plugin, no session auth required (localhost only)
        if (method == "POST")
        {
            switch (path)
            {
                case "telemetry/hello":
                {
                    var body = await ReadBody<TelemetryHelloPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdateHello(body);
                    if (!string.IsNullOrEmpty(body.SourceId))
                        watchdogManager.BroadcastHeadlessHello(body.SourceId, body.Hostname ?? "");
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/raid-state":
                {
                    var body = await ReadBody<RaidStatePayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdateRaidState(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/performance":
                {
                    var body = await ReadBody<PerformancePayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdatePerformance(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/kill":
                {
                    var body = await ReadBody<KillPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.AddKill(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/players":
                {
                    var body = await ReadBody<PlayerStatusPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdatePlayers(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/bots":
                {
                    var body = await ReadBody<BotCountPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdateBots(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/boss-spawn":
                {
                    var body = await ReadBody<BossSpawnPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.AddBossSpawn(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/extract":
                {
                    var body = await ReadBody<ExtractPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.AddExtract(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/raid-summary":
                {
                    var body = await ReadBody<RaidSummaryPayload>(context);
                    if (body == null)
                    {
                        logger.Warning("[HTTP] raid-summary POST received but body was null/invalid");
                        await WriteJson(context, 400, new { error = "Invalid body" });
                        return;
                    }
                    logger.Info($"[HTTP] raid-summary POST received — map: {body.Map}, players: {body.Players.Count}, kills: {body.TotalKills}, deaths: {body.TotalDeaths}");
                    telemetryService.FinishRaid(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/damage-stats":
                {
                    var body = await ReadBody<DamageStatsPayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdateDamageStats(body);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/positions":
                {
                    var body = await ReadBody<PositionPayload>(context);
                    if (body == null) { logger.Warning("[HTTP] telemetry/positions POST: body was null/invalid"); await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    telemetryService.UpdatePositions(body);
                    logger.Info($"[Telemetry] Positions updated: {body.Positions.Count} entities");
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                case "telemetry/map-refresh-rate":
                {
                    // POST from dashboard to change rate
                    var body = await ReadBody<MapRefreshRateRequest>(context);
                    if (body != null)
                        telemetryService.SetMapRefreshRate(body.IntervalSec);
                    await WriteJson(context, 200, new { ok = true, intervalSec = telemetryService.GetMapRefreshRate() });
                    break;
                }
                case "telemetry/console":
                {
                    var body = await ReadBody<TelemetryConsolePayload>(context);
                    if (body == null) { await WriteJson(context, 400, new { error = "Invalid body" }); return; }
                    var entries = body.Entries.Select(e => new ConsoleEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = e.Level,
                        Source = e.Source,
                        Message = e.Message,
                        SourceId = body.SourceId
                    }).ToList();
                    headlessLogService.AddStreamedEntries(body.SourceId, body.SourceId, entries);
                    await WriteJson(context, 200, new { ok = true });
                    break;
                }
                default:
                    await WriteJson(context, 404, new { error = "Not found" });
                    break;
            }
            return;
        }

        // GET endpoints — from dashboard, session auth required
        if (method == "GET")
        {
            if (!await ValidateAccess(context, headerSessionId)) return;

            var sourceFilter = context.Request.Query["source"].FirstOrDefault();

            switch (path)
            {
                case "telemetry/current":
                    await WriteJson(context, 200, telemetryService.GetCurrent(sourceFilter));
                    break;
                case "telemetry/kill-feed":
                {
                    var limitStr = context.Request.Query["limit"].FirstOrDefault();
                    var limit = int.TryParse(limitStr, out var lv) ? Math.Clamp(lv, 1, 100) : 50;
                    await WriteJson(context, 200, telemetryService.GetKillFeed(limit, sourceFilter));
                    break;
                }
                case "telemetry/raid-history":
                    await WriteJson(context, 200, telemetryService.GetRaidHistory(sourceFilter));
                    break;
                case "telemetry/lifetime-stats":
                    await WriteJson(context, 200, telemetryService.GetLifetimeStats());
                    break;
                case "telemetry/performance-history":
                    await WriteJson(context, 200, telemetryService.GetPerformanceHistory(sourceFilter));
                    break;
                case "telemetry/sources":
                    await WriteJson(context, 200, new TelemetrySourcesResponse { Sources = telemetryService.GetSources() });
                    break;
                case "telemetry/alerts":
                {
                    var sinceStr = context.Request.Query["since"].FirstOrDefault();
                    DateTime? since = null;
                    if (DateTime.TryParse(sinceStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sinceVal))
                        since = sinceVal;
                    var alertLimit = 50;
                    if (int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out var al))
                        alertLimit = Math.Clamp(al, 1, 200);
                    await WriteJson(context, 200, telemetryService.GetAlerts(since, alertLimit));
                    break;
                }
                case "telemetry/positions":
                    await WriteJson(context, 200, telemetryService.GetCurrent(sourceFilter).Positions);
                    break;
                case "telemetry/map-refresh-rate":
                    await WriteJson(context, 200, new { intervalSec = telemetryService.GetMapRefreshRate() });
                    break;
                default:
                {
                    // GET telemetry/raid-history/{id}
                    if (path.StartsWith("telemetry/raid-history/"))
                    {
                        var raidId = path.Substring("telemetry/raid-history/".Length);
                        if (string.IsNullOrEmpty(raidId))
                        {
                            await WriteJson(context, 400, new { error = "Missing raid ID" });
                            return;
                        }
                        var detail = telemetryService.GetRaidDetail(raidId);
                        if (detail == null)
                        {
                            await WriteJson(context, 404, new { error = "Raid not found" });
                            return;
                        }
                        await WriteJson(context, 200, detail);
                    }
                    else
                    {
                        await WriteJson(context, 404, new { error = "Not found" });
                    }
                    break;
                }
            }
            return;
        }

        await WriteJson(context, 405, new { error = "Method not allowed" });
    }

    // ── Watchdog Handlers ──

    private async Task HandleWatchdogRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        var endpoint = path["watchdog/".Length..];

        switch (endpoint)
        {
            case "status" when method == "GET":
            {
                var response = new WatchdogStatusResponse
                {
                    Watchdogs = watchdogManager.GetConnectedWatchdogs()
                };
                await WriteJson(context, 200, response);
                break;
            }
            case "token" when method == "GET":
            {
                var token = configService.GetConfig().Watchdog.WatchdogToken;
                await WriteJson(context, 200, new { token });
                break;
            }
            case "token" when method == "POST":
            {
                var newToken = WatchdogManager.GenerateToken();
                configService.GetConfig().Watchdog.WatchdogToken = newToken;
                configService.SaveConfig();

                // Write updated token file
                try
                {
                    var tokenPath = Path.Combine(configService.ModPath, "watchdog-token.txt");
                    File.WriteAllText(tokenPath, newToken);
                }
                catch { /* best effort */ }

                // Disconnect all watchdogs so they must reconnect with new token
                await watchdogManager.DisconnectAll("Token regenerated");

                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Watchdog: token regenerated");
                var masked = newToken.Length > 5 ? newToken[..^5] + "*****" : new string('*', newToken.Length);
                logger.Info($"[ZSlayerHQ] Watchdog token regenerated. New token: {masked}");
                await WriteJson(context, 200, new { token = newToken, message = "Token regenerated — all Watchdogs disconnected. Update their config and reconnect." });
                break;
            }
            case "start" when method == "POST":
            {
                var wdId = context.Request.Query["watchdogId"].FirstOrDefault();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Watchdog: start sptServer");
                var (sent, message) = await watchdogManager.SendCommandToTargetOrId("sptServer", "start", wdId);
                var status = sent ? 200 : (message.Contains("Rate limited") ? 429 : 503);
                await WriteJson(context, status, new { success = sent, message });
                break;
            }
            case "stop" when method == "POST":
            {
                var wdId = context.Request.Query["watchdogId"].FirstOrDefault();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Watchdog: stop sptServer");
                var (sent, message) = await watchdogManager.SendCommandToTargetOrId("sptServer", "stop", wdId);
                var status = sent ? 200 : (message.Contains("Rate limited") ? 429 : 503);
                await WriteJson(context, status, new { success = sent, message });
                break;
            }
            case "restart" when method == "POST":
            {
                var wdId = context.Request.Query["watchdogId"].FirstOrDefault();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Watchdog: restart sptServer");
                var (sent, message) = await watchdogManager.SendCommandToTargetOrId("sptServer", "restart", wdId);
                var status = sent ? 200 : (message.Contains("Rate limited") ? 429 : 503);
                await WriteJson(context, status, new { success = sent, message });
                break;
            }
            default:
                await WriteJson(context, 404, new { error = "Not found" });
                break;
        }
    }

    // ── Headless Handlers ──

    private async Task HandleHeadlessRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (path)
        {
            case "headless/status" when method == "GET":
            {
                var (available, wdStatus) = watchdogManager.GetHeadlessStatus();
                var config = configService.GetConfig().Headless;
                var dto = new HeadlessStatusDto
                {
                    Available = available,
                    Running = wdStatus?.Running ?? false,
                    Pid = wdStatus?.Pid,
                    Uptime = wdStatus?.Uptime ?? "",
                    UptimeSeconds = 0,
                    RestartCount = wdStatus?.Crashes ?? 0,
                    LastCrashReason = available ? null : "No Watchdog connected",
                    AutoStart = wdStatus?.AutoStart ?? config.AutoStart,
                    AutoStartDelaySec = config.AutoStartDelaySec,
                    AutoRestart = wdStatus?.AutoRestart ?? config.AutoRestart,
                    ProfileId = wdStatus?.Profile ?? config.ProfileId,
                    ProfileName = "",
                    ExePath = ""
                };
                await WriteJson(context, 200, dto);
                break;
            }
            case "headless/start" when method == "POST":
            {
                var wdId = context.Request.Query["watchdogId"].FirstOrDefault();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: started");
                var (sent, message) = await watchdogManager.SendCommandToTargetOrId("headlessClient", "start", wdId);
                var status = sent ? 200 : (message.Contains("Rate limited") ? 429 : 503);
                await WriteJson(context, status, new { success = sent, message });
                break;
            }
            case "headless/stop" when method == "POST":
            {
                var wdId = context.Request.Query["watchdogId"].FirstOrDefault();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: stopped");
                var (sent, message) = await watchdogManager.SendCommandToTargetOrId("headlessClient", "stop", wdId);
                var status = sent ? 200 : (message.Contains("Rate limited") ? 429 : 503);
                await WriteJson(context, status, new { success = sent, message });
                break;
            }
            case "headless/restart" when method == "POST":
            {
                var wdId = context.Request.Query["watchdogId"].FirstOrDefault();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Headless: restarted");
                var (sent, message) = await watchdogManager.SendCommandToTargetOrId("headlessClient", "restart", wdId);
                var status = sent ? 200 : (message.Contains("Rate limited") ? 429 : 503);
                await WriteJson(context, status, new { success = sent, message });
                break;
            }
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
                await WriteJson(context, 200, new { success = true });
                break;
            }
            default:
                await WriteJson(context, 404, new { error = "Not found" });
                break;
        }
    }

    // ── FIKA Config Handlers ──

    private async Task HandleFikaRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (path)
        {
            case "fika/config" when method == "GET":
                await WriteJson(context, 200, fikaConfigService.GetFikaSettings());
                break;
            case "fika/config" when method == "POST":
            {
                var body = await ReadBody<FikaConfigDto>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var result = fikaConfigService.UpdateFikaSettings(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "FIKA: config updated");
                await WriteJson(context, 200, result);
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
                var fleaCfg = fleaPriceService.GetConfig();
                await WriteJson(context, 200, new
                {
                    configTaxMultiplier = fleaCfg.FleaTaxMultiplier,
                    globalsCommunityItemTax = globals.Configuration.RagFair.CommunityItemTax,
                    globalsCommunityRequirementTax = globals.Configuration.RagFair.CommunityRequirementTax,
                    ragfairConfigOfferListingTaxMultiplier = configServer.GetConfig<RagfairConfig>().OfferListingTaxMultiplier
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

    private async Task HandleTraderRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (path)
        {
            // ── Config endpoints ──
            case "traders/config" when method == "GET":
            {
                var config = traderApplyService.GetConfig();
                await WriteJson(context, 200, config);
                break;
            }
            case "traders/config" when method == "POST":
            {
                var body = await ReadBody<TraderControlConfig>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var result = traderApplyService.UpdateFullConfig(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Traders: full config updated");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/config/global" when method == "POST":
            {
                var body = await ReadBody<TraderGlobalUpdateRequest>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }
                var result = traderApplyService.UpdateGlobalConfig(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Traders: global buy={body.GlobalBuyMultiplier:F2} sell={body.GlobalSellMultiplier:F2} stock={body.GlobalStockMultiplier:F2}");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/config/trader" when method == "POST":
            {
                var body = await ReadBody<TraderOverrideUpdateRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.TraderId))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body or missing traderId" });
                    return;
                }
                var result = traderApplyService.UpdateTraderOverride(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Traders: updated override for {body.TraderId}");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/config/trader/item" when method == "POST":
            {
                var body = await ReadBody<TraderItemOverrideRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.TraderId) || string.IsNullOrEmpty(body.TemplateId))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body or missing traderId/templateId" });
                    return;
                }
                var result = traderApplyService.SetItemOverride(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Traders: item override '{body.Name}' on {body.TraderId}");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/config/trader/add-item" when method == "POST":
            {
                var body = await ReadBody<TraderAddItemRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.TraderId) || string.IsNullOrEmpty(body.TemplateId))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body or missing traderId/templateId" });
                    return;
                }
                var result = traderApplyService.AddItemToTrader(body);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Traders: added item '{body.Name}' to {body.TraderId}");
                await WriteJson(context, 200, result);
                break;
            }

            // ── Discovery & info endpoints ──
            case "traders/list" when method == "GET":
            {
                var config = traderApplyService.GetConfig();
                var traders = traderDiscoveryService.GetDiscoveredTraders(config);
                await WriteJson(context, 200, new { traders });
                break;
            }
            case "traders/status" when method == "GET":
            {
                var status = traderApplyService.GetStatus();
                await WriteJson(context, 200, status);
                break;
            }

            // ── Action endpoints ──
            case "traders/apply" when method == "POST":
            {
                var result = traderApplyService.ApplyConfig();
                if (result.Success)
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Traders: applied — {result.ItemsModified} items across {result.TradersAffected} traders in {result.ApplyTimeMs}ms");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/reset" when method == "POST":
            {
                var result = traderApplyService.ResetAll();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, "Traders: reset all to defaults");
                await WriteJson(context, 200, result);
                break;
            }

            // ── Display override endpoints ──
            case "traders/display" when method == "POST":
            {
                var body = await ReadBody<TraderDisplayUpdateRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.TraderId))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body or missing traderId" });
                    return;
                }
                var config = configService.GetConfig().Traders;
                if (!config.TraderDisplayOverrides.TryGetValue(body.TraderId, out var displayOv))
                {
                    displayOv = new TraderDisplayOverride();
                    config.TraderDisplayOverrides[body.TraderId] = displayOv;
                }
                displayOv = displayOv with
                {
                    DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? null : body.DisplayName.Trim(),
                    CustomDescription = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim()
                };
                config.TraderDisplayOverrides[body.TraderId] = displayOv;
                // Clean up empty overrides
                if (displayOv.DisplayName == null && displayOv.CustomDescription == null && displayOv.CustomAvatar == null)
                    config.TraderDisplayOverrides.Remove(body.TraderId);
                configService.SaveConfig();
                traderDiscoveryService.ApplyInGameDisplayOverrides(config);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Traders: display name for {body.TraderId} set to '{body.DisplayName ?? "(default)"}'");
                await WriteJson(context, 200, new { success = true });
                break;
            }
            case "traders/display/avatar" when method == "POST":
            {
                var body = await ReadBody<TraderAvatarUploadRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.TraderId) || string.IsNullOrEmpty(body.ImageBase64))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    return;
                }

                // Parse data URL or raw base64
                var base64 = body.ImageBase64;
                string ext;
                if (base64.StartsWith("data:"))
                {
                    var commaIdx = base64.IndexOf(',');
                    if (commaIdx < 0)
                    {
                        await WriteJson(context, 400, new { error = "Invalid data URL format" });
                        return;
                    }
                    var mime = base64[5..base64.IndexOf(';')].ToLowerInvariant();
                    ext = mime switch
                    {
                        "image/png" => ".png",
                        "image/jpeg" => ".jpg",
                        "image/webp" => ".webp",
                        _ => ""
                    };
                    if (ext == "")
                    {
                        await WriteJson(context, 400, new { error = "Unsupported image type. Use PNG, JPG, or WebP." });
                        return;
                    }
                    base64 = base64[(commaIdx + 1)..];
                }
                else
                {
                    // Default to PNG for raw base64
                    ext = ".png";
                }

                byte[] imageBytes;
                try { imageBytes = Convert.FromBase64String(base64); }
                catch
                {
                    await WriteJson(context, 400, new { error = "Invalid base64 data" });
                    return;
                }

                if (imageBytes.Length > 2 * 1024 * 1024)
                {
                    await WriteJson(context, 400, new { error = "Image too large (max 2MB)" });
                    return;
                }

                // Generate safe filename
                var idPrefix = body.TraderId.Length >= 8 ? body.TraderId[..8] : body.TraderId;
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var filename = $"{idPrefix}_{timestamp}{ext}";

                var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                var iconDir = Path.Combine(modPath, "res", "Trader Icons");
                Directory.CreateDirectory(iconDir);

                // Delete old custom avatar file if exists
                var config = configService.GetConfig().Traders;
                if (config.TraderDisplayOverrides.TryGetValue(body.TraderId, out var existingOv) &&
                    !string.IsNullOrWhiteSpace(existingOv.CustomAvatar))
                {
                    var oldPath = Path.Combine(iconDir, existingOv.CustomAvatar);
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }

                // Write new file
                await File.WriteAllBytesAsync(Path.Combine(iconDir, filename), imageBytes);

                // Update config
                if (!config.TraderDisplayOverrides.TryGetValue(body.TraderId, out var displayOvAvatar))
                {
                    displayOvAvatar = new TraderDisplayOverride();
                }
                config.TraderDisplayOverrides[body.TraderId] = displayOvAvatar with { CustomAvatar = filename };
                configService.SaveConfig();
                traderDiscoveryService.ApplyInGameDisplayOverrides(config);

                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Traders: custom avatar uploaded for {body.TraderId}");
                await WriteJson(context, 200, new { success = true, filename });
                break;
            }

            // ── Preset endpoints ──
            case "traders/presets" when method == "GET":
            {
                var result = traderApplyService.ListTraderPresets();
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/presets/save" when method == "POST":
            {
                var body = await ReadBody<TraderPresetSaveRequest>(context);
                if (body == null || string.IsNullOrWhiteSpace(body.Name))
                {
                    await WriteJson(context, 400, new { error = "Missing preset name" });
                    return;
                }
                var result = traderApplyService.SaveTraderPreset(body.Name.Trim(), body.Description?.Trim() ?? "");
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Traders: saved preset '{body.Name}'");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/presets/load" when method == "POST":
            {
                var body = await ReadBody<TraderPresetLoadRequest>(context);
                if (body == null || string.IsNullOrWhiteSpace(body.Name))
                {
                    await WriteJson(context, 400, new { error = "Missing preset name" });
                    return;
                }
                var result = traderApplyService.LoadAndApplyTraderPreset(body.Name.Trim());
                if (result.Success)
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Traders: loaded preset '{body.Name}'");
                await WriteJson(context, 200, result);
                break;
            }
            case "traders/presets/upload" when method == "POST":
            {
                var body = await ReadBody<TraderPresetUploadRequest>(context);
                if (body == null || string.IsNullOrWhiteSpace(body.PresetJson))
                {
                    await WriteJson(context, 400, new { error = "Missing preset JSON" });
                    return;
                }
                try
                {
                    var result = traderApplyService.UploadTraderPreset(body.Name?.Trim() ?? "", body.PresetJson);
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Traders: imported preset '{result.Name}'");
                    await WriteJson(context, 200, result);
                }
                catch (Exception ex)
                {
                    await WriteJson(context, 400, new { error = "Invalid preset JSON: " + ex.Message });
                }
                break;
            }

            default:
            {
                // ── Parameterized routes ──

                // DELETE traders/presets/{name}
                if (method == "DELETE" && path.StartsWith("traders/presets/"))
                {
                    var presetName = Uri.UnescapeDataString(path["traders/presets/".Length..]);
                    if (string.IsNullOrEmpty(presetName))
                    {
                        await WriteJson(context, 400, new { error = "Missing preset name" });
                        return;
                    }
                    var deleted = traderApplyService.DeleteTraderPreset(presetName);
                    if (deleted)
                        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId, $"Traders: deleted preset '{presetName}'");
                    await WriteJson(context, 200, new { success = deleted });
                    break;
                }

                // GET traders/presets/{name}/download
                if (method == "GET" && path.StartsWith("traders/presets/") && path.EndsWith("/download"))
                {
                    var segment = path["traders/presets/".Length..^"/download".Length];
                    var presetName = Uri.UnescapeDataString(segment);
                    if (string.IsNullOrEmpty(presetName))
                    {
                        await WriteJson(context, 400, new { error = "Missing preset name" });
                        return;
                    }
                    var json = traderApplyService.DownloadTraderPreset(presetName);
                    if (json == null)
                    {
                        await WriteJson(context, 404, new { error = "Preset not found" });
                        return;
                    }
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{presetName}.json\"";
                    await context.Response.WriteAsync(json);
                    break;
                }

                // DELETE traders/display/avatar/{traderId}
                if (method == "DELETE" && path.StartsWith("traders/display/avatar/"))
                {
                    var traderId = path["traders/display/avatar/".Length..];
                    if (string.IsNullOrEmpty(traderId))
                    {
                        await WriteJson(context, 400, new { error = "Missing traderId" });
                        return;
                    }
                    var config = configService.GetConfig().Traders;
                    if (config.TraderDisplayOverrides.TryGetValue(traderId, out var displayOv) &&
                        !string.IsNullOrWhiteSpace(displayOv.CustomAvatar))
                    {
                        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                        var filePath = Path.Combine(modPath, "res", "Trader Icons", displayOv.CustomAvatar);
                        if (File.Exists(filePath)) File.Delete(filePath);

                        config.TraderDisplayOverrides[traderId] = displayOv with { CustomAvatar = null };
                        // Clean up empty overrides
                        var updated = config.TraderDisplayOverrides[traderId];
                        if (updated.DisplayName == null && updated.CustomDescription == null && updated.CustomAvatar == null)
                            config.TraderDisplayOverrides.Remove(traderId);
                        configService.SaveConfig();
                    }
                    traderDiscoveryService.ApplyInGameDisplayOverrides(configService.GetConfig().Traders);
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Traders: custom avatar removed for {traderId}");
                    await WriteJson(context, 200, new { success = true });
                    break;
                }

                // DELETE traders/config/trader/item/{traderId}/{templateId}
                if (method == "DELETE" && path.StartsWith("traders/config/trader/item/"))
                {
                    var remainder = path.Substring("traders/config/trader/item/".Length);
                    var slashIdx = remainder.IndexOf('/');
                    if (slashIdx <= 0)
                    {
                        await WriteJson(context, 400, new { error = "Missing traderId/templateId" });
                        return;
                    }
                    var traderId = remainder[..slashIdx];
                    var templateId = remainder[(slashIdx + 1)..];
                    if (string.IsNullOrEmpty(traderId) || string.IsNullOrEmpty(templateId))
                    {
                        await WriteJson(context, 400, new { error = "Missing traderId or templateId" });
                        return;
                    }
                    var result = traderApplyService.RemoveItemOverride(traderId, templateId);
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Traders: removed item override {templateId} from {traderId}");
                    await WriteJson(context, 200, result);
                    break;
                }

                // DELETE traders/config/trader/added-item/{traderId}/{index}
                if (method == "DELETE" && path.StartsWith("traders/config/trader/added-item/"))
                {
                    var remainder = path.Substring("traders/config/trader/added-item/".Length);
                    var slashIdx = remainder.IndexOf('/');
                    if (slashIdx <= 0)
                    {
                        await WriteJson(context, 400, new { error = "Missing traderId/index" });
                        return;
                    }
                    var traderId = remainder[..slashIdx];
                    var indexStr = remainder[(slashIdx + 1)..];
                    if (string.IsNullOrEmpty(traderId) || !int.TryParse(indexStr, out var index))
                    {
                        await WriteJson(context, 400, new { error = "Missing traderId or invalid index" });
                        return;
                    }
                    var result = traderApplyService.RemoveAddedItem(traderId, index);
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Traders: removed added item index {index} from {traderId}");
                    await WriteJson(context, 200, result);
                    break;
                }

                // POST traders/reset/{traderId}
                if (method == "POST" && path.StartsWith("traders/reset/"))
                {
                    var traderId = path.Substring("traders/reset/".Length);
                    if (string.IsNullOrEmpty(traderId))
                    {
                        await WriteJson(context, 400, new { error = "Missing traderId" });
                        return;
                    }
                    var result = traderApplyService.ResetTrader(traderId);
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Traders: reset {traderId}");
                    await WriteJson(context, 200, result);
                    break;
                }

                // GET traders/{traderId}/items?search=&loyaltyLevel=&limit=&offset=
                if (method == "GET" && path.StartsWith("traders/") && path.Contains("/items"))
                {
                    var afterTraders = path.Substring("traders/".Length);
                    var itemsIdx = afterTraders.IndexOf("/items");
                    if (itemsIdx <= 0)
                    {
                        await WriteJson(context, 404, new { error = "Not found" });
                        return;
                    }
                    var traderId = afterTraders[..itemsIdx];
                    var subPath = afterTraders[(itemsIdx + "/items".Length)..];

                    // GET traders/{traderId}/items/{templateId} — single item detail
                    if (subPath.StartsWith("/") && subPath.Length > 1 && !subPath.Contains('?'))
                    {
                        var templateId = subPath[1..];
                        var itemList = traderApplyService.GetTraderItems(traderId, null, null, 1000, 0);
                        var item = itemList.Items.FirstOrDefault(i => i.TemplateId == templateId);
                        if (item == null)
                        {
                            await WriteJson(context, 404, new { error = "Item not found" });
                            return;
                        }
                        await WriteJson(context, 200, item);
                        break;
                    }

                    // GET traders/{traderId}/items?search=&loyaltyLevel=&limit=&offset=
                    var search = context.Request.Query["search"].FirstOrDefault();
                    var llStr = context.Request.Query["loyaltyLevel"].FirstOrDefault();
                    var limitStr = context.Request.Query["limit"].FirstOrDefault();
                    var offsetStr = context.Request.Query["offset"].FirstOrDefault();

                    int? ll = int.TryParse(llStr, out var llVal) ? llVal : null;
                    var limit = int.TryParse(limitStr, out var limVal) ? Math.Clamp(limVal, 1, 200) : 50;
                    var offset = int.TryParse(offsetStr, out var offVal) ? Math.Max(0, offVal) : 0;

                    var result = traderApplyService.GetTraderItems(traderId, search, ll, limit, offset);
                    await WriteJson(context, 200, result);
                    break;
                }

                await WriteJson(context, 404, new { error = "Not found" });
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SKILLS ROUTES
    // ═══════════════════════════════════════════════════════════════

    private async Task HandleSkillsRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        var subPath = path == "skills" ? "" : path["skills/".Length..];

        switch (subPath)
        {
            case "list" when method == "GET":
            {
                var result = skillEditorService.GetSkillList();
                await WriteJson(context, 200, result);
                break;
            }
            case "settings" when method == "GET":
            {
                var result = skillEditorService.GetAllBonusConfigs();
                await WriteJson(context, 200, result);
                break;
            }
            case "settings" when method == "POST":
            {
                var body = await ReadBody<SkillBonusUpdateRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.SkillName))
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    break;
                }
                var success = skillEditorService.UpdateBonusValues(body);
                if (success)
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Skills: updated bonus fields for {body.SkillName}");
                await WriteJson(context, 200, new { success, skillName = body.SkillName });
                break;
            }
            case "settings/reset" when method == "POST":
            {
                var body = await ReadBody<Dictionary<string, string>>(context);
                var skillName = body?.GetValueOrDefault("skillName");
                var restored = skillEditorService.ResetBonusValues(skillName);
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    skillName != null ? $"Skills: reset bonus fields for {skillName}" : "Skills: reset all bonus fields");
                await WriteJson(context, 200, new { success = true, restored });
                break;
            }
            case "defaults" when method == "GET":
            {
                var defaults = skillEditorService.GetDefaults();
                await WriteJson(context, 200, new { defaults });
                break;
            }
            case "rates" when method == "GET":
            {
                var config = configService.GetConfig().Progression.Skills;
                await WriteJson(context, 200, new
                {
                    globalSkillSpeedMultiplier = config.GlobalSkillSpeedMultiplier,
                    skillFatigueMultiplier = config.SkillFatigueMultiplier,
                    perSkillMultipliers = config.PerSkillMultipliers
                });
                break;
            }
            case "rates" when method == "POST":
            {
                var body = await ReadBody<SkillsConfig>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    break;
                }
                var config = configService.GetConfig();
                config.Progression.Skills = body;
                configService.SaveConfig();
                progressionControlService.ClearActivePreset();
                var result = progressionControlService.ApplyConfig();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Skills: updated rate multipliers");
                await WriteJson(context, 200, result);
                break;
            }
            case "icons/list" when method == "GET":
            {
                var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                var iconDir = Path.Combine(modPath, "res", "skill-icons");
                var config = configService.GetConfig();
                var icons = new Dictionary<string, string>();

                // Scan for default game icons
                if (Directory.Exists(iconDir))
                {
                    foreach (var file in Directory.GetFiles(iconDir, "*.*")
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)))
                    {
                        var filename = Path.GetFileNameWithoutExtension(file);
                        // Skip custom_ prefixed files — they're only used via config overrides
                        if (filename.StartsWith("custom_")) continue;
                        icons[filename] = $"skill-icons/{Path.GetFileName(file)}";
                    }
                }

                // Apply custom overrides from config
                foreach (var (skillName, customFile) in config.SkillIcons)
                {
                    var customPath = Path.Combine(iconDir, customFile);
                    if (File.Exists(customPath))
                        icons[skillName] = $"skill-icons/{customFile}";
                }

                await WriteJson(context, 200, new { icons });
                break;
            }
            case "icons/upload" when method == "POST":
            {
                var body = await ReadBody<SkillIconUploadRequest>(context);
                if (body == null || string.IsNullOrEmpty(body.SkillName) || string.IsNullOrEmpty(body.ImageBase64))
                {
                    await WriteJson(context, 400, new { error = "skillName and imageBase64 required" });
                    break;
                }

                // Parse data URL or raw base64
                var base64 = body.ImageBase64;
                string ext;
                if (base64.StartsWith("data:"))
                {
                    var commaIdx = base64.IndexOf(',');
                    if (commaIdx < 0)
                    {
                        await WriteJson(context, 400, new { error = "Invalid data URL format" });
                        break;
                    }
                    var mime = base64[5..base64.IndexOf(';')].ToLowerInvariant();
                    ext = mime switch
                    {
                        "image/png" => ".png",
                        "image/jpeg" => ".jpg",
                        "image/webp" => ".webp",
                        _ => ""
                    };
                    if (ext == "")
                    {
                        await WriteJson(context, 400, new { error = "Unsupported image type. Use PNG, JPG, or WebP." });
                        break;
                    }
                    base64 = base64[(commaIdx + 1)..];
                }
                else
                {
                    ext = ".png";
                }

                byte[] imageBytes;
                try { imageBytes = Convert.FromBase64String(base64); }
                catch
                {
                    await WriteJson(context, 400, new { error = "Invalid base64 data" });
                    break;
                }

                if (imageBytes.Length > 2 * 1024 * 1024)
                {
                    await WriteJson(context, 400, new { error = "Image too large (max 2MB)" });
                    break;
                }

                var modPathUpload = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                var iconDirUpload = Path.Combine(modPathUpload, "res", "skill-icons");
                Directory.CreateDirectory(iconDirUpload);

                // Delete old custom icon if exists
                var config2 = configService.GetConfig();
                if (config2.SkillIcons.TryGetValue(body.SkillName, out var oldFile))
                {
                    var oldPath = Path.Combine(iconDirUpload, oldFile);
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }

                // Sanitize skill name to prevent path traversal
                var safeName = SanitizeFileName(body.SkillName);
                if (string.IsNullOrEmpty(safeName))
                {
                    await WriteJson(context, 400, new { error = "Invalid skill name" });
                    break;
                }

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var filename = $"custom_{safeName}_{timestamp}{ext}";
                await File.WriteAllBytesAsync(Path.Combine(iconDirUpload, filename), imageBytes);

                config2.SkillIcons[body.SkillName] = filename;
                configService.SaveConfig();

                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Skills: custom icon uploaded for {body.SkillName}");
                await WriteJson(context, 200, new { success = true, filename, url = $"skill-icons/{filename}" });
                break;
            }
            default:
            {
                // DELETE skills/icons/{skillName}
                if (method == "DELETE" && subPath.StartsWith("icons/"))
                {
                    var skillName = SanitizeFileName(subPath["icons/".Length..]);
                    if (string.IsNullOrEmpty(skillName))
                    {
                        await WriteJson(context, 400, new { error = "Missing or invalid skill name" });
                        break;
                    }

                    var config3 = configService.GetConfig();
                    if (config3.SkillIcons.TryGetValue(skillName, out var customFile))
                    {
                        var modPath3 = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                        var filePath = Path.Combine(modPath3, "res", "skill-icons", customFile);
                        if (File.Exists(filePath)) File.Delete(filePath);
                        config3.SkillIcons.Remove(skillName);
                        configService.SaveConfig();
                    }

                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Skills: custom icon removed for {skillName}");
                    await WriteJson(context, 200, new { success = true });
                    break;
                }

                // GET/POST skills/player/{sid}
                if (subPath.StartsWith("player/"))
                {
                    var sid = subPath["player/".Length..];
                    if (string.IsNullOrEmpty(sid))
                    {
                        await WriteJson(context, 400, new { error = "Missing session ID" });
                        break;
                    }

                    if (method == "GET")
                    {
                        var skills = skillEditorService.GetPlayerSkills(sid);
                        if (skills == null)
                        {
                            await WriteJson(context, 404, new { error = "Player not found" });
                            break;
                        }
                        await WriteJson(context, 200, skills);
                        break;
                    }

                    if (method == "POST")
                    {
                        // Check for bulk action query param
                        var action = context.Request.Query["action"].FirstOrDefault();
                        if (action == "max")
                        {
                            var count = skillEditorService.SetAllPlayerSkills(sid, 51);
                            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                                $"Skills: maxed all skills for player {sid}");
                            await WriteJson(context, 200, new { success = true, skillsUpdated = count });
                            break;
                        }
                        if (action == "reset")
                        {
                            var count = skillEditorService.SetAllPlayerSkills(sid, 0);
                            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                                $"Skills: reset all skills for player {sid}");
                            await WriteJson(context, 200, new { success = true, skillsUpdated = count });
                            break;
                        }
                        if (action == "setall")
                        {
                            var body = await ReadBody<PlayerSkillBulkRequest>(context);
                            if (body == null)
                            {
                                await WriteJson(context, 400, new { error = "Invalid request body" });
                                break;
                            }
                            var count = skillEditorService.SetAllPlayerSkills(sid, body.Level);
                            activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                                $"Skills: set all skills to level {body.Level} for player {sid}");
                            await WriteJson(context, 200, new { success = true, skillsUpdated = count });
                            break;
                        }

                        // Default: individual skill updates
                        var updateBody = await ReadBody<PlayerSkillUpdateRequest>(context);
                        if (updateBody == null)
                        {
                            await WriteJson(context, 400, new { error = "Invalid request body" });
                            break;
                        }
                        var success = skillEditorService.SetPlayerSkills(sid, updateBody);
                        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                            $"Skills: updated {updateBody.Skills.Count} skill levels for player {sid}");
                        await WriteJson(context, 200, new { success });
                        break;
                    }
                }

                await WriteJson(context, 404, new { error = "Not found" });
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PROGRESSION ROUTES
    // ═══════════════════════════════════════════════════════════════

    private async Task HandleProgressionRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        var subPath = path == "progression" ? "" : path["progression/".Length..];

        switch (subPath)
        {
            case "" when method == "GET":
            {
                var config = configService.GetConfig().Progression;
                await WriteJson(context, 200, config);
                break;
            }
            case "" when method == "POST":
            {
                var body = await ReadBody<ProgressionConfig>(context);
                if (body == null)
                {
                    await WriteJson(context, 400, new { error = "Invalid request body" });
                    break;
                }
                var config = configService.GetConfig();
                config.Progression = body;
                configService.SaveConfig();
                progressionControlService.ClearActivePreset();
                var result = progressionControlService.ApplyConfig();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    $"Progression: updated config — {result.SettingsModified} settings applied");
                await WriteJson(context, 200, result);
                break;
            }
            case "apply" when method == "POST":
            {
                var result = progressionControlService.ApplyConfig();
                await WriteJson(context, 200, result);
                break;
            }
            case "reset" when method == "POST":
            {
                var result = progressionControlService.ResetToDefaults();
                activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                    "Progression: reset all settings to defaults");
                await WriteJson(context, 200, result);
                break;
            }
            case "status" when method == "GET":
            {
                var status = progressionControlService.GetStatus();
                await WriteJson(context, 200, status);
                break;
            }
            case "presets" when method == "GET":
            {
                var presets = progressionControlService.GetPresets();
                await WriteJson(context, 200, new { presets });
                break;
            }
            case "presets/save" when method == "POST":
            {
                var body = await ReadBody<Dictionary<string, string>>(context);
                var name = body?.GetValueOrDefault("name")?.Trim();
                var desc = body?.GetValueOrDefault("description")?.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    await WriteJson(context, 400, new { error = "Missing preset name" });
                    break;
                }
                var saved = progressionControlService.SaveCustomPreset(name, desc);
                if (saved)
                    activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                        $"Progression: saved custom preset '{name}'");
                await WriteJson(context, saved ? 200 : 400, new
                {
                    success = saved,
                    error = saved ? null : "Cannot overwrite built-in preset"
                });
                break;
            }
            default:
            {
                // POST progression/presets/{name} — apply preset
                if (method == "POST" && subPath.StartsWith("presets/"))
                {
                    var presetName = Uri.UnescapeDataString(subPath["presets/".Length..]);
                    if (string.IsNullOrEmpty(presetName))
                    {
                        await WriteJson(context, 400, new { error = "Missing preset name" });
                        break;
                    }
                    var result = progressionControlService.ApplyPreset(presetName);
                    if (result.Success)
                    {
                        configService.SaveConfig();
                        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                            $"Progression: applied '{presetName}' preset — {result.SettingsModified} settings");
                    }
                    await WriteJson(context, result.Success ? 200 : 400, result);
                    break;
                }

                // DELETE progression/presets/{name} — delete custom preset
                if (method == "DELETE" && subPath.StartsWith("presets/"))
                {
                    var presetName = Uri.UnescapeDataString(subPath["presets/".Length..]);
                    var deleted = progressionControlService.DeleteCustomPreset(presetName);
                    if (deleted)
                        activityLogService.LogAction(ActionType.ConfigChange, headerSessionId,
                            $"Progression: deleted custom preset '{presetName}'");
                    await WriteJson(context, deleted ? 200 : 404, new { success = deleted });
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
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".json"] = "application/json",
        [".woff2"] = "font/woff2",
        [".woff"] = "font/woff",
    };

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        // Strip path separators and traversal
        var safe = name.Replace("/", "").Replace("\\", "").Replace("..", "");
        // Only allow alphanumeric, underscore, hyphen
        safe = new string(safe.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        return safe;
    }

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
        // Cache images/fonts for 1hr, skill-icons no-cache (custom upload swaps), HTML/JS/CSS revalidate
        context.Response.Headers["Cache-Control"] = ext is ".html" or ".js" or ".css"
            ? "no-cache"
            : relativePath.StartsWith("skill-icons/")
                ? "no-cache"
                : "public, max-age=3600";
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

        // Check password if configured
        var configPassword = configService.GetConfig().Access.Password;
        if (!string.IsNullOrEmpty(configPassword))
        {
            var headerPassword = context.Request.Headers["X-Password"].FirstOrDefault() ?? "";
            if (headerPassword != configPassword)
            {
                await WriteJson(context, 403, new { error = "Invalid password" });
                return false;
            }
        }

        return true;
    }

    private static void SetCorsHeaders(HttpContext context)
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, DELETE, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Session-Id, X-Password";
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

    // ═══════════════════════════════════════════════════════
    // BACKUP & RESTORE ROUTES
    // ═══════════════════════════════════════════════════════

    private async Task HandleBackupRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        if (!await ValidateAccess(context, headerSessionId)) return;

        switch (path)
        {
            case "backups" when method == "GET":
                await WriteJson(context, 200, backupService.ListBackups());
                break;

            case "backups/create" when method == "POST":
            {
                var req = await ReadBody<BackupCreateRequest>(context);
                var type = req?.Type ?? "profile";
                var notes = req?.Notes ?? "";
                var entries = new List<BackupEntry>();

                if (type is "profile" or "both")
                    entries.Add(backupService.CreateProfileBackup(notes));
                if (type is "config" or "both")
                    entries.Add(backupService.CreateConfigBackup(notes));

                activityLogService.LogAction(ActionType.ServerStart, headerSessionId, $"Manual backup created ({type})");
                await WriteJson(context, 200, new { success = true, entries });
                break;
            }

            case "backups/config" when method == "GET":
                await WriteJson(context, 200, backupService.GetCcBackupConfig());
                break;

            case "backups/config" when method == "POST":
            {
                var cfg = await ReadBody<CcBackupConfig>(context);
                if (cfg == null) { await WriteJson(context, 400, new { error = "Invalid config" }); break; }
                backupService.SaveCcBackupConfig(cfg);
                await WriteJson(context, 200, new { success = true });
                break;
            }

            case "wipe/full" when method == "POST":
            {
                var req = await ReadBody<WipeRequest>(context);
                if (req == null) { await WriteJson(context, 400, new { error = "Invalid request" }); break; }
                var (success, message) = wipeService.FullWipe(req.ConfirmText);
                activityLogService.LogAction(ActionType.ServerStart, headerSessionId, $"Full wipe: {message}");
                await WriteJson(context, success ? 200 : 400, new { success, message });
                break;
            }

            case "wipe/selective" when method == "POST":
            {
                var req = await ReadBody<WipeRequest>(context);
                if (req == null) { await WriteJson(context, 400, new { error = "Invalid request" }); break; }
                var (success, message) = wipeService.SelectiveWipe(req.Categories, req.ConfirmText);
                activityLogService.LogAction(ActionType.ServerStart, headerSessionId, $"Selective wipe: {message}");
                await WriteJson(context, success ? 200 : 400, new { success, message });
                break;
            }

            default:
            {
                // Parameterized routes: backups/{id}/...
                if (path.StartsWith("backups/") && path.Contains('/'))
                {
                    var segments = path.Split('/');
                    if (segments.Length >= 2)
                    {
                        var id = segments[1];

                        if (segments.Length == 2 && method == "DELETE")
                        {
                            var deleted = backupService.DeleteBackup(id);
                            await WriteJson(context, deleted ? 200 : 404, new { success = deleted });
                            break;
                        }

                        if (segments.Length == 3 && segments[2] == "diff" && method == "GET")
                        {
                            var diff = backupService.GetBackupDiff(id);
                            if (diff == null) { await WriteJson(context, 404, new { error = "Backup not found" }); break; }
                            await WriteJson(context, 200, diff);
                            break;
                        }

                        if (segments.Length == 3 && segments[2] == "restore" && method == "POST")
                        {
                            var isConfig = id.StartsWith("config-");
                            var (success, message) = isConfig
                                ? backupService.RestoreConfigBackup(id)
                                : backupService.RestoreProfileBackup(id);
                            if (success)
                                activityLogService.LogAction(ActionType.ServerStart, headerSessionId, $"Backup restored: {id}");
                            await WriteJson(context, success ? 200 : 400, new { success, message });
                            break;
                        }
                    }
                }

                await WriteJson(context, 404, new { error = "Unknown backup route" });
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SCHEDULER & EVENTS ROUTES
    // ═══════════════════════════════════════════════════════════════

    private async Task HandleSchedulerRoute(HttpContext context, string headerSessionId, string path, string method)
    {
        switch (path)
        {
            // GET /scheduler — full overview (events, tasks, active, templates)
            case "scheduler" when method == "GET":
                await WriteJson(context, 200, eventService.GetOverview());
                break;

            // GET /scheduler/active — currently active events only
            case "scheduler/active" when method == "GET":
                await WriteJson(context, 200, new { events = eventService.GetActiveEvents() });
                break;

            // POST /scheduler/event — create a new event
            case "scheduler/event" when method == "POST":
            {
                var body = await ReadBody<CreateEventRequest>(context);
                if (body == null) { await WriteJson(context, 400, new { error = "Invalid request body" }); break; }
                try
                {
                    var evt = eventService.CreateEvent(body, headerSessionId);
                    await WriteJson(context, 200, evt);
                }
                catch (ArgumentException ex)
                {
                    await WriteJson(context, 400, new { error = ex.Message });
                }
                break;
            }

            // POST /scheduler/task — create a new scheduled task
            case "scheduler/task" when method == "POST":
            {
                var body = await ReadBody<CreateTaskRequest>(context);
                if (body == null) { await WriteJson(context, 400, new { error = "Invalid request body" }); break; }
                try
                {
                    var task = eventService.CreateTask(body);
                    activityLogService.LogAction(ActionType.ScheduledTask, headerSessionId,
                        $"Created scheduled task: '{task.Name}' ({task.Type})");
                    await WriteJson(context, 200, task);
                }
                catch (ArgumentException ex)
                {
                    await WriteJson(context, 400, new { error = ex.Message });
                }
                break;
            }

            // POST /scheduler/cron/validate — validate a cron expression
            case "scheduler/cron/validate" when method == "POST":
            {
                var body = await ReadBody<CronValidateRequest>(context);
                if (body == null) { await WriteJson(context, 400, new { error = "Invalid request body" }); break; }
                var valid = CronParser.IsValid(body.Expression ?? "");
                string? description = null;
                DateTime? nextRun = null;
                if (valid)
                {
                    description = CronParser.Describe(body.Expression!);
                    nextRun = CronParser.GetNextOccurrence(body.Expression!, DateTime.UtcNow);
                }
                await WriteJson(context, 200, new { valid, description, nextRun });
                break;
            }

            default:
            {
                // Parameterized event routes: scheduler/event/{id}/{action}
                if (path.StartsWith("scheduler/event/"))
                {
                    var parts = path["scheduler/event/".Length..].Split('/');
                    var eventId = parts[0];

                    if (parts.Length == 1 && method == "GET")
                    {
                        var evt = eventService.GetEvent(eventId);
                        if (evt == null) { await WriteJson(context, 404, new { error = "Event not found" }); break; }
                        await WriteJson(context, 200, evt);
                        break;
                    }

                    if (parts.Length == 1 && method == "DELETE")
                    {
                        var deleted = eventService.DeleteEvent(eventId);
                        await WriteJson(context, deleted ? 200 : 404, new { success = deleted });
                        break;
                    }

                    if (parts.Length == 2)
                    {
                        var action = parts[1];
                        switch (action)
                        {
                            case "activate" when method == "POST":
                            {
                                var evt = eventService.ActivateEvent(eventId, headerSessionId);
                                if (evt == null) { await WriteJson(context, 404, new { error = "Event not found" }); break; }
                                await WriteJson(context, 200, evt);
                                break;
                            }
                            case "deactivate" when method == "POST":
                            {
                                var evt = eventService.DeactivateEvent(eventId, headerSessionId);
                                if (evt == null) { await WriteJson(context, 404, new { error = "Event not found" }); break; }
                                await WriteJson(context, 200, evt);
                                break;
                            }
                            case "cancel" when method == "POST":
                            {
                                var evt = eventService.CancelEvent(eventId, headerSessionId);
                                if (evt == null) { await WriteJson(context, 404, new { error = "Event not found" }); break; }
                                await WriteJson(context, 200, evt);
                                break;
                            }
                            default:
                                await WriteJson(context, 404, new { error = "Unknown event action" });
                                break;
                        }
                        break;
                    }
                }

                // Parameterized task routes: scheduler/task/{id}/{action}
                if (path.StartsWith("scheduler/task/"))
                {
                    var parts = path["scheduler/task/".Length..].Split('/');
                    var taskId = parts[0];

                    if (parts.Length == 1 && method == "DELETE")
                    {
                        var deleted = eventService.DeleteTask(taskId);
                        await WriteJson(context, deleted ? 200 : 404, new { success = deleted });
                        break;
                    }

                    if (parts.Length == 2 && parts[1] == "toggle" && method == "POST")
                    {
                        var body = await ReadBody<TaskToggleRequest>(context);
                        var task = eventService.ToggleTask(taskId, body?.Enabled ?? true);
                        if (task == null) { await WriteJson(context, 404, new { error = "Task not found" }); break; }
                        await WriteJson(context, 200, task);
                        break;
                    }
                }

                await WriteJson(context, 404, new { error = "Unknown scheduler route" });
                break;
            }
        }
    }
}
