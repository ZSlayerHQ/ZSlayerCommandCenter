using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class PlayerStatsService(
    SaveServer saveServer,
    ProfileActivityService profileActivityService)
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string DollarsTpl = "5696686a4bdc2da3298b456a";
    private const string EurosTpl = "569668774bdc2da2298b4568";

    // Approximate conversion rates to roubles
    private const int DollarsToRoubles = 143;
    private const int EurosToRoubles = 157;

    public PlayerOverviewDto GetPlayerOverview()
    {
        var profiles = saveServer.GetProfiles();
        var activeIds = profileActivityService.GetActiveProfileIdsWithinMinutes(5);
        var activeSet = new HashSet<string>(activeIds.Select(id => id.ToString()));

        var players = new List<PlayerSummaryDto>();

        foreach (var (sessionId, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null) continue;

            var roubles = CalculateRoubles(pmc.Inventory?.Items);

            players.Add(new PlayerSummaryDto
            {
                SessionId = sessionId.ToString(),
                Nickname = pmc.Info.Nickname ?? "Unknown",
                Level = pmc.Info.Level ?? 0,
                Side = pmc.Info.Side ?? "",
                Online = activeSet.Contains(sessionId.ToString()),
                Roubles = roubles
            });
        }

        // Sort: online first, then by level descending
        players.Sort((a, b) =>
        {
            if (a.Online != b.Online) return b.Online.CompareTo(a.Online);
            return b.Level.CompareTo(a.Level);
        });

        return new PlayerOverviewDto
        {
            TotalProfiles = players.Count,
            OnlineCount = activeSet.Count,
            Players = players
        };
    }

    public EconomyDto GetEconomy()
    {
        var overview = GetPlayerOverview();
        var totalRoubles = overview.Players.Sum(p => p.Roubles);
        var avgWealth = overview.Players.Count > 0 ? totalRoubles / overview.Players.Count : 0;

        var topPlayers = overview.Players
            .OrderByDescending(p => p.Roubles)
            .Take(5)
            .ToList();

        return new EconomyDto
        {
            TotalRoubles = totalRoubles,
            AverageWealth = avgWealth,
            TopPlayers = topPlayers
        };
    }

    private static long CalculateRoubles(IEnumerable<SPTarkov.Server.Core.Models.Eft.Common.Tables.Item>? items)
    {
        if (items == null) return 0;

        long total = 0;
        foreach (var item in items)
        {
            var count = (long)(item.Upd?.StackObjectsCount ?? 1);
            if (item.Template == RoublesTpl)
                total += count;
            else if (item.Template == DollarsTpl)
                total += count * (long)DollarsToRoubles;
            else if (item.Template == EurosTpl)
                total += count * (long)EurosToRoubles;
        }
        return total;
    }
}
