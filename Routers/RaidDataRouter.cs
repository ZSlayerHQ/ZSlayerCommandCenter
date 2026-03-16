using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Enums;
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
    private static ItemHelper _itemHelper = null!;
    private static ISptLogger<RaidDataRouter> _logger = null!;
    private static JsonUtil _jsonUtil = null!;

    public RaidDataRouter(
        JsonUtil jsonUtil,
        RaidTrackingService raidTrackingService,
        SaveServer saveServer,
        ConfigService configService,
        ItemHelper itemHelper,
        ISptLogger<RaidDataRouter> logger) : base(jsonUtil, GetRoutes())
    {
        _raidTracker = raidTrackingService;
        _saveServer = saveServer;
        _configService = configService;
        _itemHelper = itemHelper;
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

                    // Apply death penalties after SPT has processed the raid
                    try
                    {
                        ApplyDeathPenalties(sessionId.ToString(), info);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning($"ZSlayerCommandCenter: Death penalty error: {ex.Message}");
                    }

                    // Always passthrough SPT's response unchanged
                    return new ValueTask<string>(output ?? "");
                })
        ];
    }

    /// <summary>Check if this raid ended in a PMC death. Returns (isDead, isPmc) tuple.</summary>
    private static (bool isDead, bool isPmc) CheckPmcDeath(EndLocalRaidRequestData info)
    {
        var resultStr = "Unknown";
        var isPmc = false;

        try
        {
            var json = _jsonUtil.Serialize(info);
            if (string.IsNullOrEmpty(json)) return (false, false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("results", out var resultsEl))
                resultStr = TryGetString(resultsEl, "result") ?? TryGetString(resultsEl, "Result") ?? "Unknown";
            else
                resultStr = TryGetString(root, "result") ?? TryGetString(root, "exitStatus") ?? "Unknown";

            if (root.TryGetProperty("serverId", out var serverIdEl))
            {
                var serverId = serverIdEl.GetString() ?? "";
                isPmc = serverId.ToLowerInvariant().Contains("pmc");
            }
        }
        catch { return (false, false); }

        var isDead = resultStr is "Killed" or "KILLED" or "Left" or "LEFT" or "MissingInAction" or "MISSINGINACTION";
        return (isDead, isPmc);
    }

    private static void ApplyDeathPenalties(string sessionId, EndLocalRaidRequestData info)
    {
        var cfg = _configService?.GetConfig()?.Fir;
        if (cfg == null) return;

        var hasDeathTax = cfg.DeathTaxPercent > 0;
        var hasSecureWipe = cfg.SecureContainerWipeOnDeath;
        if (!hasDeathTax && !hasSecureWipe) return;

        var (isDead, isPmc) = CheckPmcDeath(info);
        if (!isDead || !isPmc) return;

        var profiles = _saveServer?.GetProfiles();
        if (profiles == null || !profiles.TryGetValue(sessionId, out var profile)) return;

        var pmcData = profile.CharacterData?.PmcData;
        if (pmcData?.Inventory?.Items == null) return;

        var nickname = pmcData.Info?.Nickname ?? "Unknown";
        var profileChanged = false;

        // ── Death Tax ──
        if (hasDeathTax)
            profileChanged |= ApplyDeathTax(pmcData, cfg, nickname);

        // ── Secure Container Wipe ──
        if (hasSecureWipe)
            profileChanged |= ApplySecureContainerWipe(pmcData, nickname);

        if (profileChanged)
        {
            try { _ = _saveServer.SaveProfileAsync(sessionId); }
            catch (Exception ex)
            {
                _logger?.Warning($"ZSlayerCommandCenter: Failed to save profile after death penalties: {ex.Message}");
            }
        }
    }

    private static bool ApplyDeathTax(PmcData pmcData, FirConfig cfg, string nickname)
    {
        var stashId = pmcData.Inventory.Stash.ToString();
        if (string.IsNullOrEmpty(stashId)) return false;

        const string roublesTemplate = "5449016a4bdc2d6f028b456f";
        var rubleStacks = new List<Item>();

        foreach (var item in pmcData.Inventory.Items)
        {
            if (item.Template.ToString() != roublesTemplate) continue;

            var currentParent = item.ParentId?.ToString() ?? "";
            var inStash = false;
            var maxDepth = 20;

            while (!string.IsNullOrEmpty(currentParent) && maxDepth-- > 0)
            {
                if (currentParent == stashId) { inStash = true; break; }
                var parentItem = pmcData.Inventory.Items.FirstOrDefault(i => i.Id.ToString() == currentParent);
                if (parentItem == null) break;
                currentParent = parentItem.ParentId?.ToString() ?? "";
            }

            if (inStash) rubleStacks.Add(item);
        }

        if (rubleStacks.Count == 0) return false;

        var totalRoubles = rubleStacks.Sum(i => i.Upd?.StackObjectsCount ?? 1);
        var taxPercent = Math.Clamp(cfg.DeathTaxPercent, 0, 100);
        var taxAmount = Math.Round(totalRoubles * taxPercent / 100.0);
        if (taxAmount < 1) return false;

        var remaining = taxAmount;
        var itemsToRemove = new List<Item>();

        foreach (var stack in rubleStacks.OrderBy(i => i.Upd?.StackObjectsCount ?? 1))
        {
            if (remaining <= 0) break;
            var stackCount = stack.Upd?.StackObjectsCount ?? 1;

            if (stackCount <= remaining)
            {
                remaining -= stackCount;
                itemsToRemove.Add(stack);
            }
            else
            {
                if (stack.Upd != null) stack.Upd.StackObjectsCount = stackCount - remaining;
                remaining = 0;
            }
        }

        foreach (var item in itemsToRemove)
            pmcData.Inventory.Items.Remove(item);

        _logger?.Info($"[ZSlayerHQ] Death tax: {nickname} lost ₽{taxAmount:N0} ({taxPercent}% of ₽{totalRoubles:N0})");
        return true;
    }

    private static bool ApplySecureContainerWipe(PmcData pmcData, string nickname)
    {
        // Find the secure container item (slotId == "SecuredContainer")
        var secureContainer = pmcData.Inventory.Items.FirstOrDefault(i => i.SlotId == "SecuredContainer");
        if (secureContainer == null) return false;

        var containerId = secureContainer.Id.ToString();

        // Build a set of all item IDs that descend from the secure container (BFS)
        var containerItemIds = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(containerId);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            foreach (var child in pmcData.Inventory.Items)
            {
                var childId = child.Id.ToString();
                if (child.ParentId?.ToString() == parentId && childId != containerId)
                {
                    containerItemIds.Add(childId);
                    queue.Enqueue(childId);
                }
            }
        }

        if (containerItemIds.Count == 0) return false;

        // Identify which items are keys or cases (protected from wipe)
        var protectedIds = new HashSet<string>();
        foreach (var item in pmcData.Inventory.Items)
        {
            var itemId = item.Id.ToString();
            if (!containerItemIds.Contains(itemId)) continue;

            var tpl = item.Template;
            // Protect keys (all types) and cases/containers
            if (_itemHelper.IsOfBaseclass(tpl, BaseClasses.KEY)
                || _itemHelper.IsOfBaseclass(tpl, BaseClasses.SIMPLE_CONTAINER)
                || _itemHelper.IsOfBaseclass(tpl, BaseClasses.MOB_CONTAINER)
                || _itemHelper.IsOfBaseclass(tpl, BaseClasses.LOCKABLE_CONTAINER))
            {
                protectedIds.Add(itemId);
            }
        }

        // Also protect items INSIDE protected cases (BFS from protected items)
        var protectedQueue = new Queue<string>(protectedIds);
        while (protectedQueue.Count > 0)
        {
            var parentId = protectedQueue.Dequeue();
            foreach (var child in pmcData.Inventory.Items)
            {
                var childId = child.Id.ToString();
                if (child.ParentId?.ToString() == parentId && containerItemIds.Contains(childId) && protectedIds.Add(childId))
                    protectedQueue.Enqueue(childId);
            }
        }

        // Remove all non-protected items from the secure container
        var removed = pmcData.Inventory.Items.RemoveAll(item =>
        {
            var itemId = item.Id.ToString();
            return containerItemIds.Contains(itemId) && !protectedIds.Contains(itemId);
        });

        if (removed > 0)
            _logger?.Info($"[ZSlayerHQ] Secure container wipe: {nickname} lost {removed} items (keys & cases preserved)");

        return removed > 0;
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
