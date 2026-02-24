using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
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
    private static ISptLogger<RaidDataRouter> _logger = null!;
    private static JsonUtil _jsonUtil = null!;

    public RaidDataRouter(
        JsonUtil jsonUtil,
        RaidTrackingService raidTrackingService,
        SaveServer saveServer,
        ISptLogger<RaidDataRouter> logger) : base(jsonUtil, GetRoutes())
    {
        _raidTracker = raidTrackingService;
        _saveServer = saveServer;
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

                    // Always passthrough SPT's response unchanged
                    return new ValueTask<string>(output ?? "");
                })
        ];
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
