using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class QuestBrowserService(
    DatabaseService databaseService,
    LocaleService localeService,
    SaveServer saveServer,
    ISptLogger<QuestBrowserService> logger)
{
    private static readonly Dictionary<string, string> MapNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bigmap"] = "Customs",
        ["factory4_day"] = "Factory",
        ["factory4_night"] = "Factory",
        ["woods"] = "Woods",
        ["shoreline"] = "Shoreline",
        ["interchange"] = "Interchange",
        ["lighthouse"] = "Lighthouse",
        ["reservebase"] = "Reserve",
        ["tarkovstreets"] = "Streets",
        ["sandbox"] = "Ground Zero",
        ["sandbox_high"] = "Ground Zero",
        ["laboratory"] = "Labs",
        ["labyrinth"] = "Labyrinth",
        ["any"] = "Any",
        [""] = "Any",
    };

    public static string MapLocationName(string location)
    {
        if (string.IsNullOrEmpty(location)) return "Any";
        return MapNames.TryGetValue(location, out var name) ? name : location;
    }

    public QuestBrowserDto GetQuests(string? search, string? map, string? trader, string? sort, string? sortDir)
    {
        var questTemplates = databaseService.GetQuests();
        var allTraders = databaseService.GetTables().Traders;
        var locale = localeService.GetLocaleDb("en");

        var summaries = new List<QuestSummaryDto>();
        var mapCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (questId, quest) in questTemplates)
        {
            var qid = questId.ToString();

            // Resolve quest name from locale
            var questName = qid;
            if (locale.TryGetValue($"{qid} name", out var localeName) && !string.IsNullOrEmpty(localeName))
                questName = localeName;
            else if (!string.IsNullOrEmpty(quest.QuestName))
                questName = quest.QuestName;

            // Resolve trader name
            var traderId = quest.TraderId.ToString();
            var traderName = traderId;
            if (allTraders.TryGetValue(traderId, out var traderObj))
                traderName = traderObj.Base?.Nickname ?? traderId;

            // Location
            var location = quest.Location ?? "";
            var locationName = MapLocationName(location);

            // Level from AvailableForStart conditions
            var levelRequired = 0;
            var startConditions = quest.Conditions?.AvailableForStart;
            if (startConditions != null)
            {
                foreach (var cond in startConditions)
                {
                    if (string.Equals(cond.ConditionType, "Level", StringComparison.OrdinalIgnoreCase))
                    {
                        levelRequired = (int)(cond.Value ?? 0);
                        break;
                    }
                }
            }

            // Objective count from AvailableForFinish
            var objectiveCount = quest.Conditions?.AvailableForFinish?.Count ?? 0;

            // Side and type
            var side = quest.Side ?? "";
            var type = quest.Type.ToString();

            var summary = new QuestSummaryDto
            {
                QuestId = qid,
                QuestName = questName,
                TraderName = traderName,
                TraderId = traderId,
                Location = location,
                LocationName = locationName,
                LevelRequired = levelRequired,
                ObjectiveCount = objectiveCount,
                Side = side,
                Type = type
            };

            summaries.Add(summary);

            // Accumulate map counts (for chips) — normalize "" and "any" to "any"
            var mapKey = string.IsNullOrEmpty(location) ? "any" : location.ToLowerInvariant();
            mapCounts[mapKey] = mapCounts.GetValueOrDefault(mapKey) + 1;
        }

        // Build map groups
        var maps = mapCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new QuestMapGroup
            {
                Location = kv.Key,
                LocationName = MapLocationName(kv.Key),
                Count = kv.Value
            })
            .ToList();

        // Apply filters
        var filtered = summaries.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(q =>
                q.QuestName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                q.QuestId.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(map) && !string.Equals(map, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(q =>
            {
                var loc = string.IsNullOrEmpty(q.Location) ? "any" : q.Location.ToLowerInvariant();
                return string.Equals(loc, map, StringComparison.OrdinalIgnoreCase);
            });
        }

        if (!string.IsNullOrEmpty(trader))
            filtered = filtered.Where(q => q.TraderId.Equals(trader, StringComparison.OrdinalIgnoreCase));

        // Apply sort
        var ascending = !string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        filtered = (sort?.ToLowerInvariant()) switch
        {
            "name" => ascending
                ? filtered.OrderBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(q => q.QuestName, StringComparer.OrdinalIgnoreCase),
            "level" => ascending
                ? filtered.OrderBy(q => q.LevelRequired).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(q => q.LevelRequired).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase),
            "objectives" => ascending
                ? filtered.OrderBy(q => q.ObjectiveCount).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(q => q.ObjectiveCount).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase),
            "map" => ascending
                ? filtered.OrderBy(q => q.LocationName, StringComparer.OrdinalIgnoreCase).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(q => q.LocationName, StringComparer.OrdinalIgnoreCase).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase),
            _ => ascending
                ? filtered.OrderBy(q => q.TraderName, StringComparer.OrdinalIgnoreCase).ThenBy(q => q.LevelRequired)
                : filtered.OrderByDescending(q => q.TraderName, StringComparer.OrdinalIgnoreCase).ThenByDescending(q => q.LevelRequired),
        };

        var result = filtered.ToList();

        return new QuestBrowserDto
        {
            Total = result.Count,
            Quests = result,
            Maps = maps
        };
    }

    public QuestDetailDto? GetQuestDetail(string questId)
    {
        var questTemplates = databaseService.GetQuests();
        var mongoId = new MongoId(questId);

        if (!questTemplates.TryGetValue(mongoId, out var quest))
            return null;

        var locale = localeService.GetLocaleDb("en");
        var allTraders = databaseService.GetTables().Traders;
        var qid = questId;

        // Name
        var questName = qid;
        if (locale.TryGetValue($"{qid} name", out var localeName) && !string.IsNullOrEmpty(localeName))
            questName = localeName;
        else if (!string.IsNullOrEmpty(quest.QuestName))
            questName = quest.QuestName;

        // Trader
        var traderId = quest.TraderId.ToString();
        var traderName = traderId;
        if (allTraders.TryGetValue(traderId, out var traderObj))
            traderName = traderObj.Base?.Nickname ?? traderId;

        // Location
        var location = quest.Location ?? "";
        var locationName = MapLocationName(location);

        // Level
        var levelRequired = 0;
        var startConditions = quest.Conditions?.AvailableForStart;
        if (startConditions != null)
        {
            foreach (var cond in startConditions)
            {
                if (string.Equals(cond.ConditionType, "Level", StringComparison.OrdinalIgnoreCase))
                {
                    levelRequired = (int)(cond.Value ?? 0);
                    break;
                }
            }
        }

        // Objectives
        var objectives = new List<QuestObjectiveDto>();
        var finishConditions = quest.Conditions?.AvailableForFinish;
        if (finishConditions != null)
        {
            foreach (var cond in finishConditions)
            {
                var condId = cond.Id.ToString();
                locale.TryGetValue(condId, out var localeDesc);
                var val = cond.Value ?? 0;
                var desc = !string.IsNullOrEmpty(localeDesc)
                    ? $"{localeDesc} (0/{(int)val})"
                    : $"{cond.ConditionType}: (0/{(int)val})";

                objectives.Add(new QuestObjectiveDto
                {
                    Id = condId,
                    Type = cond.ConditionType ?? "",
                    Description = desc,
                    Value = val
                });
            }
        }

        return new QuestDetailDto
        {
            QuestId = qid,
            QuestName = questName,
            TraderName = traderName,
            TraderId = traderId,
            Location = location,
            LocationName = locationName,
            LevelRequired = levelRequired,
            Side = quest.Side ?? "",
            Type = quest.Type.ToString(),
            Objectives = objectives
        };
    }

    public SetQuestStateResponse SetQuestState(string adminSessionId, string questId, string targetSessionId, string status)
    {
        try
        {
            var profiles = saveServer.GetProfiles();
            var targetMongoId = new MongoId(targetSessionId);

            if (!profiles.TryGetValue(targetMongoId, out var profile))
                return new SetQuestStateResponse { Success = false, Error = "Player not found" };

            var pmc = profile.CharacterData?.PmcData;
            if (pmc == null)
                return new SetQuestStateResponse { Success = false, Error = "Player PMC data not found" };

            // Parse status string to enum
            if (!Enum.TryParse<QuestStatusEnum>(status, ignoreCase: true, out var questStatus))
                return new SetQuestStateResponse { Success = false, Error = $"Invalid quest status: {status}" };

            var questMongoId = new MongoId(questId);

            // Find existing quest entry
            var existingQuest = pmc.Quests.FirstOrDefault(q => q.QId == questMongoId);

            if (existingQuest != null)
            {
                existingQuest.Status = questStatus;
            }
            else
            {
                // Create new quest entry
                pmc.Quests.Add(new QuestStatus
                {
                    QId = questMongoId,
                    Status = questStatus,
                    StartTime = 0,
                    CompletedConditions = [],
                    StatusTimers = new Dictionary<QuestStatusEnum, double>()
                });
            }

            _ = saveServer.SaveProfileAsync(targetSessionId);

            logger.Info($"ZSlayerCommandCenter: Admin {adminSessionId} set quest {questId} to {status} for player {targetSessionId}");

            return new SetQuestStateResponse
            {
                Success = true,
                Message = $"Quest status set to {status}"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error setting quest state: {ex.Message}");
            return new SetQuestStateResponse { Success = false, Error = ex.Message };
        }
    }
}
