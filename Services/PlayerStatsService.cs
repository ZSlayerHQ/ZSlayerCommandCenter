using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class PlayerStatsService(
    SaveServer saveServer,
    ProfileActivityService profileActivityService,
    ConfigService configService)
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
        var headlessId = configService.GetConfig().Headless.ProfileId;

        var players = new List<PlayerSummaryDto>();

        foreach (var (sessionId, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null) continue;

            var sid = sessionId.ToString();
            var isHeadless = !string.IsNullOrEmpty(headlessId) && sid == headlessId;
            var roubles = CalculateRoubles(pmc.Inventory?.Items);

            players.Add(new PlayerSummaryDto
            {
                SessionId = sid,
                Nickname = pmc.Info.Nickname ?? "Unknown",
                Level = pmc.Info.Level ?? 0,
                Side = pmc.Info.Side ?? "",
                Online = activeSet.Contains(sid),
                Roubles = roubles,
                IsHeadless = isHeadless
            });
        }

        // Sort: online first, then by level descending
        players.Sort((a, b) =>
        {
            if (a.Online != b.Online) return b.Online.CompareTo(a.Online);
            return b.Level.CompareTo(a.Level);
        });

        var realPlayers = players.Where(p => !p.IsHeadless).ToList();

        return new PlayerOverviewDto
        {
            TotalProfiles = realPlayers.Count,
            OnlineCount = realPlayers.Count(p => p.Online),
            Players = players
        };
    }

    public EconomyDto GetEconomy()
    {
        var overview = GetPlayerOverview();
        var realPlayers = overview.Players.Where(p => !p.IsHeadless).ToList();
        var totalRoubles = realPlayers.Sum(p => p.Roubles);
        var avgWealth = realPlayers.Count > 0 ? totalRoubles / realPlayers.Count : 0;

        var topPlayers = realPlayers
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

    public ProfileRaidStatsDto GetServerRaidStats()
    {
        var profiles = saveServer.GetProfiles();
        var headlessId = configService.GetConfig().Headless.ProfileId;

        int totalRaids = 0, survived = 0, deaths = 0;
        int pmcKills = 0, scavKills = 0, bossKills = 0, headshots = 0;

        foreach (var (sessionId, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null) continue;

            var sid = sessionId.ToString();
            if (!string.IsNullOrEmpty(headlessId) && sid == headlessId) continue;

            var stats = ExtractProfileRaidStats(pmc);
            totalRaids += stats.TotalRaids;
            survived += stats.Survived;
            deaths += stats.Deaths;
            pmcKills += stats.PmcKills;
            scavKills += stats.ScavKills;
            bossKills += stats.BossKills;
            headshots += stats.Headshots;
        }

        var totalKills = pmcKills + scavKills + bossKills;
        return new ProfileRaidStatsDto
        {
            TotalRaids = totalRaids,
            Survived = survived,
            Deaths = deaths,
            SurvivalRate = totalRaids > 0 ? Math.Round((double)survived / totalRaids * 100, 1) : 0,
            TotalKills = totalKills,
            PmcKills = pmcKills,
            ScavKills = scavKills,
            BossKills = bossKills,
            Headshots = headshots,
            KdRatio = deaths > 0 ? Math.Round((double)totalKills / deaths, 2) : totalKills
        };
    }

    public ProfileRaidStatsDto? GetPlayerRaidStats(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return null;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Info == null) return null;

        return ExtractProfileRaidStats(pmc);
    }

    private static ProfileRaidStatsDto ExtractProfileRaidStats(SPTarkov.Server.Core.Models.Eft.Common.PmcData pmc)
    {
        int totalRaids = 0, survived = 0, deaths = 0;
        int pmcKills = 0, scavKills = 0, bossKills = 0, headshots = 0;

        var counters = pmc.Stats?.Eft?.OverallCounters?.Items;
        if (counters != null)
        {
            foreach (var counter in counters)
            {
                if (counter.Key == null || counter.Key.Count < 2) continue;
                var k = counter.Key;
                var val = (int)counter.Value;

                if (k.Contains("Sessions") && k.Contains("Pmc")) totalRaids = val;
                else if (k.Contains("ExitStatus") && k.Contains("Survived")) survived = val;
                else if (k.Contains("ExitStatus") && k.Contains("Killed")) deaths = val;
                else if (k.Contains("Kills") && k.Contains("Pmc")) pmcKills = val;
                else if (k.Contains("Kills") && k.Contains("Savage")) scavKills = val;
                else if (k.Contains("Kills") && (k.Contains("Boss") || k.Contains("KilledBoss"))) bossKills = val;
                else if (k.Contains("HeadShots")) headshots = val;
            }
        }

        var totalKills = pmcKills + scavKills + bossKills;
        return new ProfileRaidStatsDto
        {
            TotalRaids = totalRaids,
            Survived = survived,
            Deaths = deaths,
            SurvivalRate = totalRaids > 0 ? Math.Round((double)survived / totalRaids * 100, 1) : 0,
            TotalKills = totalKills,
            PmcKills = pmcKills,
            ScavKills = scavKills,
            BossKills = bossKills,
            Headshots = headshots,
            KdRatio = deaths > 0 ? Math.Round((double)totalKills / deaths, 2) : totalKills
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
