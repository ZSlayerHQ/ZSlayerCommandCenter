using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using ZSlayerCommandCenter.Models;
using ZSlayerCommandCenter.Services;

namespace ZSlayerCommandCenter.Routers;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class RaidDataRouter : StaticRouter
{
    private static RaidTrackingService _raidTracker = null!;
    private static SaveServer _saveServer = null!;
    private static ConfigService _configService = null!;
    private static ISptLogger<RaidDataRouter> _logger = null!;
    private static JsonUtil _jsonUtil = null!;

    public RaidDataRouter(
        JsonUtil jsonUtil,
        RaidTrackingService raidTrackingService,
        SaveServer saveServer,
        ConfigService configService,
        ISptLogger<RaidDataRouter> logger) : base(jsonUtil, GetRoutes())
    {
        _raidTracker = raidTrackingService;
        _saveServer = saveServer;
        _configService = configService;
        _logger = logger;
        _jsonUtil = jsonUtil;
    }

    private static List<RouteAction> GetRoutes()
    {
        return
        [
            new RouteAction<EndLocalRaidRequestData>("/client/match/local/end",
                (url, info, sessionId, output) =>
                {
                    try
                    {
                        ParseAndRecordRaid(sessionId.ToString(), info);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning($"ZSlayerCommandCenter: Failed to parse raid end data: {ex.Message}");
                    }

                    // Apply death tax after SPT has processed the raid
                    try
                    {
                        ApplyDeathTax(sessionId.ToString(), info);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning($"ZSlayerCommandCenter: Death tax error: {ex.Message}");
                    }

                    // Always passthrough SPT's response unchanged
                    return new ValueTask<string>(output ?? "");
                })
        ];
    }

    private static void ApplyDeathTax(string sessionId, EndLocalRaidRequestData info)
    {
        var cfg = _configService?.GetConfig()?.Fir;
        if (cfg == null || cfg.DeathTaxPercent <= 0) return;

        // Check if player died
        var resultStr = "Unknown";
        try
        {
            var json = _jsonUtil.Serialize(info);
            if (string.IsNullOrEmpty(json)) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("results", out var resultsEl))
                resultStr = TryGetString(resultsEl, "result") ?? TryGetString(resultsEl, "Result") ?? "Unknown";
            else
                resultStr = TryGetString(root, "result") ?? TryGetString(root, "exitStatus") ?? "Unknown";
        }
        catch { return; }

        // Only apply on death (KILLED, LEFT, MISSINGINACTION)
        var isDead = resultStr is "Killed" or "KILLED" or "Left" or "LEFT" or "MissingInAction" or "MISSINGINACTION";
        if (!isDead) return;

        // Check if this is a PMC raid (not scav)
        try
        {
            var json = _jsonUtil.Serialize(info);
            if (string.IsNullOrEmpty(json)) return;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("serverId", out var serverIdEl))
            {
                var serverId = serverIdEl.GetString() ?? "";
                if (!serverId.ToLowerInvariant().Contains("pmc")) return;
            }
        }
        catch { return; }

        // Get the saved profile (SPT has already saved post-death state)
        var profiles = _saveServer?.GetProfiles();
        if (profiles == null || !profiles.TryGetValue(sessionId, out var profile)) return;

        var pmcData = profile.CharacterData?.PmcData;
        if (pmcData?.Inventory?.Items == null) return;

        // Find stash root ID
        var stashId = pmcData.Inventory.Stash.ToString();
        if (string.IsNullOrEmpty(stashId)) return;

        // Find all ruble stacks in stash (walk parent chain to verify in stash)
        const string roublesTemplate = "5449016a4bdc2d6f028b456f";
        var rubleStacks = new List<Item>();

        foreach (var item in pmcData.Inventory.Items)
        {
            if (item.Template.ToString() != roublesTemplate) continue;

            // Walk parent chain to check if item is in stash
            var currentParent = item.ParentId?.ToString() ?? "";
            var inStash = false;
            var maxDepth = 20;

            while (!string.IsNullOrEmpty(currentParent) && maxDepth-- > 0)
            {
                if (currentParent == stashId)
                {
                    inStash = true;
                    break;
                }
                // Find the parent item
                var parentItem = pmcData.Inventory.Items.FirstOrDefault(i => i.Id.ToString() == currentParent);
                if (parentItem == null) break;
                currentParent = parentItem.ParentId?.ToString() ?? "";
            }

            if (inStash)
                rubleStacks.Add(item);
        }

        if (rubleStacks.Count == 0) return;

        // Calculate total rubles and tax
        var totalRoubles = rubleStacks.Sum(i => i.Upd?.StackObjectsCount ?? 1);
        var taxPercent = Math.Clamp(cfg.DeathTaxPercent, 0, 100);
        var taxAmount = Math.Round(totalRoubles * taxPercent / 100.0);
        if (taxAmount < 1) return;

        var remaining = taxAmount;
        var itemsToRemove = new List<Item>();

        // Sort by stack size ascending (consume smaller stacks first)
        foreach (var stack in rubleStacks.OrderBy(i => i.Upd?.StackObjectsCount ?? 1))
        {
            if (remaining <= 0) break;
            var stackCount = stack.Upd?.StackObjectsCount ?? 1;

            if (stackCount <= remaining)
            {
                // Consume entire stack
                remaining -= stackCount;
                itemsToRemove.Add(stack);
            }
            else
            {
                // Partial consumption
                if (stack.Upd != null)
                    stack.Upd.StackObjectsCount = stackCount - remaining;
                remaining = 0;
            }
        }

        // Remove fully consumed stacks
        foreach (var item in itemsToRemove)
            pmcData.Inventory.Items.Remove(item);

        // Save the modified profile
        try
        {
            _ = _saveServer.SaveProfileAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"ZSlayerCommandCenter: Failed to save profile after death tax: {ex.Message}");
        }

        var nickname = pmcData.Info?.Nickname ?? "Unknown";
        _logger?.Info($"[ZSlayerHQ] Death tax: {nickname} lost ₽{taxAmount:N0} ({taxPercent}% of ₽{totalRoubles:N0})");
    }

    private static void ParseAndRecordRaid(string sessionId, EndLocalRaidRequestData info)
    {
        var nickname = "Unknown";
        try
        {
            var profiles = _saveServer.GetProfiles();
            if (profiles.TryGetValue(sessionId, out var profile))
                nickname = profile.CharacterData?.PmcData?.Info?.Nickname ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger?.Debug($"ZSlayerCommandCenter: Failed profile lookup for raid record ({sessionId}): {ex.Message}");
        }

        // Serialize the request data to JSON so we can extract fields regardless of property naming
        var map = "Unknown";
        var result = "Unknown";
        var playTime = 0;

        try
        {
            var json = _jsonUtil.Serialize(info);
            if (!string.IsNullOrEmpty(json))
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                map = TryGetString(root, "locationId")
                   ?? TryGetString(root, "location")
                   ?? TryGetString(root, "LocationId")
                   ?? "Unknown";

                // EndRaidResult is typically nested under "results"
                if (root.TryGetProperty("results", out var resultsEl))
                {
                    result = TryGetString(resultsEl, "result")
                          ?? TryGetString(resultsEl, "Result")
                          ?? "Unknown";

                    if (resultsEl.TryGetProperty("playTime", out var ptEl) && ptEl.TryGetInt32(out var pt))
                        playTime = pt;
                    else if (resultsEl.TryGetProperty("PlayTime", out var pt2El) && pt2El.TryGetInt32(out var pt2))
                        playTime = pt2;
                }
                else
                {
                    result = TryGetString(root, "result")
                          ?? TryGetString(root, "exitStatus")
                          ?? "Unknown";

                    if (root.TryGetProperty("playTime", out var ptEl) && ptEl.TryGetInt32(out var pt))
                        playTime = pt;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning($"ZSlayerCommandCenter: Error extracting raid data from JSON: {ex.Message}");
        }

        _raidTracker.RecordRaid(new RaidEndRecord
        {
            SessionId = sessionId,
            Nickname = nickname,
            Map = map,
            Result = result,
            PlayTimeSeconds = playTime,
            Timestamp = DateTime.UtcNow
        });
    }

    private static string? TryGetString(JsonElement el, string propertyName)
    {
        if (el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}
