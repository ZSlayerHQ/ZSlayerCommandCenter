using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Json;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class QuestDiscoveryService(
    DatabaseService databaseService,
    LocaleService localeService,
    SaveServer saveServer,
    ISptLogger<QuestDiscoveryService> logger)
{
    private static readonly Dictionary<string, string> MapNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Human-readable keys
        ["bigmap"] = "Customs",
        ["factory4"] = "Factory",
        ["factory4_day"] = "Factory",
        ["factory4_night"] = "Factory (Night)",
        ["woods"] = "Woods",
        ["shoreline"] = "Shoreline",
        ["interchange"] = "Interchange",
        ["lighthouse"] = "Lighthouse",
        ["rezervbase"] = "Reserve",
        ["reservebase"] = "Reserve",
        ["tarkovstreets"] = "Streets of Tarkov",
        ["sandbox"] = "Ground Zero",
        ["sandbox_high"] = "Ground Zero (21+)",
        ["laboratory"] = "The Lab",
        ["labyrinth"] = "Labyrinth",
        ["any"] = "Any Location",
        [""] = "Any Location",
        // MongoDB location IDs (quests use these)
        ["56f40101d2720b2a4d8b45d6"] = "Customs",
        ["55f2d3fd4bdc2d5f408b4567"] = "Factory",
        ["59fc81d786f774390775787e"] = "Factory (Night)",
        ["5704e3c2d2720bac5b8b4567"] = "Woods",
        ["5704e554d2720bac5b8b456e"] = "Shoreline",
        ["5714dbc024597771384a510d"] = "Interchange",
        ["5704e4dad2720bb55b8b4567"] = "Lighthouse",
        ["5b0fc42d86f7744a585f9105"] = "The Lab",
        ["5704e5fad2720bc05b8b4567"] = "Reserve",
        ["5714dc692459777137212e12"] = "Streets of Tarkov",
        ["653e6760052c01c1c805532f"] = "Ground Zero",
        ["65b8d6f5cdde2479cb2a3125"] = "Ground Zero (21+)",
    };

    /// <summary>Original quest data snapshots keyed by quest ID string.</summary>
    private readonly Dictionary<string, QuestSnapshot> _snapshots = new();

    /// <summary>Cached quest summaries built during discovery.</summary>
    private List<QuestSummaryDto> _summaries = [];

    private bool _initialized;

    public static string MapLocationName(string? location)
    {
        if (string.IsNullOrEmpty(location)) return "Any Location";
        return MapNames.TryGetValue(location, out var name) ? name : location;
    }

    // ═══════════════════════════════════════════════════════════════
    //  INITIALIZATION & SNAPSHOT
    // ═══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        if (_initialized) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var questTemplates = databaseService.GetQuests();
        var locale = localeService.GetLocaleDb("en");

        var count = 0;
        foreach (var (questMongoId, quest) in questTemplates)
        {
            var qid = questMongoId.ToString();
            SnapshotQuest(qid, quest, locale);
            count++;
        }

        // Build initial summaries
        RebuildSummaries();

        sw.Stop();
        _initialized = true;
        logger.Info($"[ZSlayerHQ] Quest discovery: {count} quests snapshotted in {sw.ElapsedMilliseconds}ms");
    }

    private void SnapshotQuest(string questId, SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest quest, Dictionary<string, string> locale)
    {
        var snapshot = new QuestSnapshot
        {
            TraderId = quest.TraderId.ToString()
        };

        // Snapshot conditions (AvailableForStart + AvailableForFinish)
        SnapshotConditions(snapshot, quest.Conditions?.AvailableForStart);
        SnapshotConditions(snapshot, quest.Conditions?.AvailableForFinish);
        SnapshotConditions(snapshot, quest.Conditions?.Fail);

        // Snapshot the original AvailableForStart list membership (for restoring removed prerequisites)
        if (quest.Conditions?.AvailableForStart != null)
            snapshot.OriginalStartConditions = new List<QuestCondition>(quest.Conditions.AvailableForStart);

        // Snapshot rewards
        if (quest.Rewards != null)
        {
            foreach (var (_, rewardList) in quest.Rewards)
            {
                if (rewardList == null) continue;
                foreach (var reward in rewardList)
                {
                    var rid = reward.Id.ToString();
                    if (!snapshot.Rewards.ContainsKey(rid))
                    {
                        snapshot.Rewards[rid] = new RewardSnapshot
                        {
                            RewardType = reward.Type?.ToString() ?? "",
                            Value = reward.Value
                        };
                    }
                }
            }
        }

        // Snapshot locale entries for this quest
        var localeKeys = new[]
        {
            $"{questId} name", $"{questId} description", $"{questId} note",
            $"{questId} failMessageText", $"{questId} successMessageText"
        };
        foreach (var key in localeKeys)
        {
            if (locale.TryGetValue(key, out var val))
                snapshot.LocaleEntries[key] = val;
        }

        // Snapshot condition locale entries
        foreach (var condId in snapshot.Conditions.Keys)
        {
            if (locale.TryGetValue(condId, out var condLocale))
                snapshot.LocaleEntries[condId] = condLocale;
        }

        _snapshots[questId] = snapshot;
    }

    private static void SnapshotConditions(QuestSnapshot snapshot, List<SPTarkov.Server.Core.Models.Eft.Common.Tables.QuestCondition>? conditions)
    {
        if (conditions == null) return;
        foreach (var cond in conditions)
        {
            var condId = cond.Id.ToString();
            if (!snapshot.Conditions.ContainsKey(condId))
            {
                snapshot.Conditions[condId] = new ConditionSnapshot
                {
                    ConditionType = cond.ConditionType ?? "",
                    Value = cond.Value,
                    OnlyFoundInRaid = cond.OnlyFoundInRaid
                };
            }
        }
    }

    public QuestSnapshot? GetSnapshot(string questId) =>
        _snapshots.TryGetValue(questId, out var snap) ? snap : null;

    public Dictionary<string, QuestSnapshot> GetAllSnapshots() => _snapshots;

    // ═══════════════════════════════════════════════════════════════
    //  BROWSE — Quest List
    // ═══════════════════════════════════════════════════════════════

    public void RebuildSummaries()
    {
        var questTemplates = databaseService.GetQuests();
        var locale = localeService.GetLocaleDb("en");

        var summaries = new List<QuestSummaryDto>(questTemplates.Count);

        foreach (var (questMongoId, quest) in questTemplates)
        {
            var qid = questMongoId.ToString();
            summaries.Add(BuildSummary(qid, quest, locale));
        }

        _summaries = summaries;
    }

    private QuestSummaryDto BuildSummary(
        string qid,
        SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest quest,
        Dictionary<string, string> locale)
    {
        // Resolve quest name from locale
        var questName = ResolveQuestName(qid, quest, locale);

        // Resolve trader name
        var traderId = quest.TraderId.ToString();
        var traderName = ResolveTraderName(traderId, locale);

        // Location
        var location = quest.Location ?? "";
        var locationName = MapLocationName(location);

        // Level from AvailableForStart conditions
        var levelRequired = GetLevelRequirement(quest);

        // Counts
        var objectiveCount = quest.Conditions?.AvailableForFinish?.Count ?? 0;
        var rewardCount = quest.Rewards != null && quest.Rewards.TryGetValue("Success", out var successRewards)
            ? successRewards?.Count ?? 0 : 0;
        var prerequisiteCount = quest.Conditions?.AvailableForStart?
            .Count(c => string.Equals(c.ConditionType, "Quest", StringComparison.OrdinalIgnoreCase)) ?? 0;

        return new QuestSummaryDto
        {
            QuestId = qid,
            QuestName = questName,
            TraderName = traderName,
            TraderId = traderId,
            Location = location,
            LocationName = locationName,
            LevelRequired = levelRequired,
            ObjectiveCount = objectiveCount,
            RewardCount = rewardCount,
            PrerequisiteCount = prerequisiteCount,
            Side = quest.Side ?? "",
            Type = quest.Type.ToString(),
            IsDisabled = false,
            HasOverrides = false
        };
    }

    /// <summary>
    /// Get quest summaries with optional filtering and sorting.
    /// Config is needed to decorate with isDisabled/hasOverrides flags.
    /// </summary>
    public QuestBrowserResponse GetQuests(
        QuestEditorConfig config,
        string? search, string? map, string? trader, string? type,
        string? sort, string? sortDir,
        int limit = 50, int offset = 0,
        string? sessionId = null)
    {
        // Build player quest status lookup for logged-in player
        var playerStatuses = BuildPlayerQuestStatusMap(sessionId);

        // Decorate summaries with config status + player status
        var decorated = _summaries.Select(s =>
        {
            var copy = s with
            {
                IsDisabled = config.DisabledQuests.Contains(s.QuestId),
                HasOverrides = config.QuestOverrides.ContainsKey(s.QuestId),
                PlayerStatus = playerStatuses.GetValueOrDefault(s.QuestId, "Locked")
            };
            return copy;
        });

        // Build map groups from unfiltered set
        var mapCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in _summaries)
        {
            var mapKey = string.IsNullOrEmpty(s.Location) ? "any" : s.Location.ToLowerInvariant();
            mapCounts[mapKey] = mapCounts.GetValueOrDefault(mapKey) + 1;
        }
        var maps = mapCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new QuestMapGroup { Location = kv.Key, LocationName = MapLocationName(kv.Key), Count = kv.Value })
            .ToList();

        // Apply filters
        var filtered = decorated.AsEnumerable();

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

        if (!string.IsNullOrEmpty(type) && !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(q => q.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

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
            "status" => ascending
                ? filtered.OrderBy(q => q.IsDisabled ? 0 : q.HasOverrides ? 1 : 2).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(q => q.IsDisabled ? 2 : q.HasOverrides ? 1 : 0).ThenBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase),
            _ => ascending
                ? filtered.OrderBy(q => q.TraderName, StringComparer.OrdinalIgnoreCase).ThenBy(q => q.LevelRequired)
                : filtered.OrderByDescending(q => q.TraderName, StringComparer.OrdinalIgnoreCase).ThenByDescending(q => q.LevelRequired),
        };

        var allResults = filtered.ToList();
        var paged = allResults.Skip(offset).Take(limit).ToList();

        return new QuestBrowserResponse
        {
            Total = allResults.Count,
            Quests = paged,
            Maps = maps
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  DETAIL — Single Quest
    // ═══════════════════════════════════════════════════════════════

    public QuestDetailResponse? GetQuestDetail(string questId, QuestEditorConfig config)
    {
        var questTemplates = databaseService.GetQuests();
        var mongoId = new MongoId(questId);

        if (!questTemplates.TryGetValue(mongoId, out var quest))
            return null;

        var locale = localeService.GetLocaleDb("en");
        var snapshot = GetSnapshot(questId);

        var questName = ResolveQuestName(questId, quest, locale);
        var traderId = quest.TraderId.ToString();
        var traderName = ResolveTraderName(traderId, locale);
        var levelRequired = GetLevelRequirement(quest);
        var description = "";
        if (locale.TryGetValue($"{questId} description", out var descLocale))
            description = descLocale;

        // Prerequisites
        var prerequisites = BuildPrerequisites(quest, locale);

        // Objectives
        var objectives = BuildObjectives(quest, locale, snapshot);

        // Rewards
        var successRewards = BuildRewards(quest, "Success", locale, snapshot);
        var failRewards = BuildRewards(quest, "Fail", locale, snapshot);

        var isDisabled = config.DisabledQuests.Contains(questId);
        var hasOverrides = config.QuestOverrides.ContainsKey(questId);

        return new QuestDetailResponse
        {
            QuestId = questId,
            QuestName = questName,
            Description = description,
            TraderName = traderName,
            TraderId = traderId,
            Location = quest.Location ?? "",
            LocationName = MapLocationName(quest.Location),
            LevelRequired = levelRequired,
            Side = quest.Side ?? "",
            Type = quest.Type.ToString(),
            IsDisabled = isDisabled,
            HasOverrides = hasOverrides,
            Prerequisites = prerequisites,
            Objectives = objectives,
            SuccessRewards = successRewards,
            FailRewards = failRewards
        };
    }

    private List<PrerequisiteInfo> BuildPrerequisites(
        SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest quest,
        Dictionary<string, string> locale)
    {
        var result = new List<PrerequisiteInfo>();
        var startConditions = quest.Conditions?.AvailableForStart;
        if (startConditions == null) return result;

        var questTemplates = databaseService.GetQuests();

        foreach (var cond in startConditions)
        {
            if (!string.Equals(cond.ConditionType, "Quest", StringComparison.OrdinalIgnoreCase))
                continue;

            var targetQuestIds = ToStringList(cond.Target);
            var statuses = cond.Status?.Select(s => s.ToString()).ToList() ?? [];

            foreach (var targetQid in targetQuestIds)
            {
                var prereqName = targetQid;
                if (locale.TryGetValue($"{targetQid} name", out var locName) && !string.IsNullOrEmpty(locName))
                    prereqName = locName;
                else if (questTemplates.TryGetValue(new MongoId(targetQid), out var prereqQuest) && !string.IsNullOrEmpty(prereqQuest.QuestName))
                    prereqName = prereqQuest.QuestName;

                result.Add(new PrerequisiteInfo
                {
                    ConditionId = cond.Id.ToString(),
                    QuestId = targetQid,
                    QuestName = prereqName,
                    RequiredStatuses = statuses
                });
            }
        }

        return result;
    }

    private List<ObjectiveInfo> BuildObjectives(
        SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest quest,
        Dictionary<string, string> locale,
        QuestSnapshot? snapshot)
    {
        var result = new List<ObjectiveInfo>();
        var finishConditions = quest.Conditions?.AvailableForFinish;
        if (finishConditions == null) return result;

        foreach (var cond in finishConditions)
        {
            var condId = cond.Id.ToString();
            locale.TryGetValue(condId, out var localeDesc);

            var currentValue = cond.Value ?? 0;
            var originalValue = snapshot?.Conditions.TryGetValue(condId, out var cs) == true
                ? cs.Value ?? 0 : currentValue;

            var targets = ToStringList(cond.Target);
            var targetNames = targets.Select((string t) =>
            {
                if (locale.TryGetValue($"{t} Name", out var n) && !string.IsNullOrEmpty(n)) return n;
                if (locale.TryGetValue($"{t} ShortName", out var sn) && !string.IsNullOrEmpty(sn)) return sn;
                return t;
            }).ToList();

            var description = !string.IsNullOrEmpty(localeDesc) ? localeDesc : $"{cond.ConditionType}: {(int)currentValue}";

            result.Add(new ObjectiveInfo
            {
                ConditionId = condId,
                ConditionType = cond.ConditionType ?? "",
                Description = description,
                Value = currentValue,
                OriginalValue = originalValue,
                OnlyFoundInRaid = cond.OnlyFoundInRaid,
                Target = targets,
                TargetNames = targetNames,
                HasOverride = Math.Abs(currentValue - originalValue) > 0.001
            });
        }

        return result;
    }

    private List<RewardInfo> BuildRewards(
        SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest quest,
        string rewardKey,
        Dictionary<string, string> locale,
        QuestSnapshot? snapshot)
    {
        var result = new List<RewardInfo>();
        if (quest.Rewards == null || !quest.Rewards.TryGetValue(rewardKey, out var rewards) || rewards == null)
            return result;

        foreach (var reward in rewards)
        {
            var rid = reward.Id.ToString();
            var rewardType = reward.Type?.ToString() ?? "";
            var currentValue = reward.Value ?? 0;
            var originalValue = snapshot?.Rewards.TryGetValue(rid, out var rs) == true
                ? rs.Value ?? 0 : currentValue;

            // Resolve target name
            var target = reward.Target ?? "";
            var targetName = target;
            if (reward.Type == RewardType.TraderStanding || reward.Type == RewardType.TraderUnlock)
                targetName = ResolveTraderName(target, locale);
            else if (reward.Type == RewardType.Skill && locale.TryGetValue($"{target} name", out var skillName))
                targetName = skillName;

            // Build item list
            var items = new List<RewardItemInfo>();
            if (reward.Items != null)
            {
                foreach (var item in reward.Items)
                {
                    var tpl = item.Template.ToString();
                    var itemName = tpl;
                    if (locale.TryGetValue($"{tpl} Name", out var iName) && !string.IsNullOrEmpty(iName))
                        itemName = iName;

                    items.Add(new RewardItemInfo
                    {
                        TemplateId = tpl,
                        Name = itemName,
                        Count = (int)(item.Upd?.StackObjectsCount ?? 1)
                    });
                }
            }

            result.Add(new RewardInfo
            {
                RewardId = rid,
                Type = rewardType,
                Value = currentValue,
                OriginalValue = originalValue,
                Target = target,
                TargetName = targetName,
                Items = items,
                HasOverride = Math.Abs(currentValue - originalValue) > 0.001
            });
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TREE — Prerequisite Graph
    // ═══════════════════════════════════════════════════════════════

    public QuestTreeResponse GetQuestTree(QuestEditorConfig config)
    {
        var questTemplates = databaseService.GetQuests();
        var locale = localeService.GetLocaleDb("en");

        var nodes = new List<QuestTreeNode>();
        var edges = new List<QuestTreeEdge>();

        foreach (var (questMongoId, quest) in questTemplates)
        {
            var qid = questMongoId.ToString();
            var questName = ResolveQuestName(qid, quest, locale);
            var traderId = quest.TraderId.ToString();

            nodes.Add(new QuestTreeNode
            {
                Id = qid,
                Name = questName,
                Trader = ResolveTraderName(traderId, locale),
                TraderId = traderId,
                Level = GetLevelRequirement(quest),
                IsDisabled = config.DisabledQuests.Contains(qid),
                HasOverrides = config.QuestOverrides.ContainsKey(qid)
            });

            // Build edges from Quest prerequisites
            var startConditions = quest.Conditions?.AvailableForStart;
            if (startConditions == null) continue;

            foreach (var cond in startConditions)
            {
                if (!string.Equals(cond.ConditionType, "Quest", StringComparison.OrdinalIgnoreCase))
                    continue;

                var targets = ToStringList(cond.Target);
                foreach (var targetQid in targets)
                {
                    edges.Add(new QuestTreeEdge { From = targetQid, To = qid });
                }
            }
        }

        return new QuestTreeResponse { Nodes = nodes, Edges = edges };
    }

    // ═══════════════════════════════════════════════════════════════
    //  TRADER & LOCATION LISTS (for filter dropdowns)
    // ═══════════════════════════════════════════════════════════════

    public List<QuestTraderInfo> GetTraderList()
    {
        var traderCounts = new Dictionary<string, (string Name, int Count)>();
        var locale = localeService.GetLocaleDb("en");

        foreach (var s in _summaries)
        {
            if (!traderCounts.ContainsKey(s.TraderId))
                traderCounts[s.TraderId] = (ResolveTraderName(s.TraderId, locale), 0);

            var (name, count) = traderCounts[s.TraderId];
            traderCounts[s.TraderId] = (name, count + 1);
        }

        return traderCounts
            .OrderBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new QuestTraderInfo { TraderId = kv.Key, TraderName = kv.Value.Name, QuestCount = kv.Value.Count })
            .ToList();
    }

    public List<QuestMapGroup> GetLocationList()
    {
        var mapCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in _summaries)
        {
            var mapKey = string.IsNullOrEmpty(s.Location) ? "any" : s.Location.ToLowerInvariant();
            mapCounts[mapKey] = mapCounts.GetValueOrDefault(mapKey) + 1;
        }

        return mapCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new QuestMapGroup { Location = kv.Key, LocationName = MapLocationName(kv.Key), Count = kv.Value })
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    //  PER-PLAYER QUEST STATUS
    // ═══════════════════════════════════════════════════════════════

    public List<QuestPlayerStatus> GetPlayerQuestStatuses(string questId)
    {
        var result = new List<QuestPlayerStatus>();
        var questMongoId = new MongoId(questId);
        var profiles = saveServer.GetProfiles();

        foreach (var (_, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc == null) continue;

            var playerName = pmc.Info?.Nickname ?? "Unknown";
            var questEntry = pmc.Quests.FirstOrDefault(q => q.QId == questMongoId);
            var status = questEntry?.Status.ToString() ?? "NotStarted";

            result.Add(new QuestPlayerStatus
            {
                SessionId = profile.ProfileInfo?.ProfileId?.ToString() ?? "",
                PlayerName = playerName,
                Status = status
            });
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════

    public string ResolveQuestName(string questId, SPTarkov.Server.Core.Models.Eft.Common.Tables.Quest quest, Dictionary<string, string> locale)
    {
        if (locale.TryGetValue($"{questId} name", out var localeName) && !string.IsNullOrEmpty(localeName))
            return localeName;
        if (!string.IsNullOrEmpty(quest.QuestName))
            return quest.QuestName;
        if (!string.IsNullOrEmpty(quest.Name))
            return quest.Name;
        return questId;
    }

    public string ResolveTraderName(string traderId, Dictionary<string, string> locale)
    {
        // Try locale first (handles modded traders like Artem that set name via locale)
        if (locale.TryGetValue($"{traderId} Nickname", out var localeNick) && !string.IsNullOrEmpty(localeNick))
            return localeNick;

        // Try trader Base.Nickname
        var allTraders = databaseService.GetTables().Traders;
        if (allTraders != null && allTraders.TryGetValue(traderId, out var traderObj))
        {
            var nick = traderObj.Base?.Nickname;
            if (!string.IsNullOrEmpty(nick))
                return nick;
        }

        return traderId;
    }

    public static int GetLevelRequirement(Quest quest)
    {
        var startConditions = quest.Conditions?.AvailableForStart;
        if (startConditions == null) return 0;

        foreach (var cond in startConditions)
        {
            if (string.Equals(cond.ConditionType, "Level", StringComparison.OrdinalIgnoreCase))
                return (int)(cond.Value ?? 0);
        }

        return 0;
    }

    /// <summary>Convert ListOrT&lt;string&gt; to a plain List&lt;string&gt;.</summary>
    public static List<string> ToStringList(ListOrT<string>? target)
    {
        if (target == null) return [];
        if (target.IsList && target.List != null) return target.List;
        if (target.IsItem && target.Item != null) return [target.Item];
        return [];
    }

    /// <summary>Build a questId → status string map for the given player session.</summary>
    private Dictionary<string, string> BuildPlayerQuestStatusMap(string? sessionId)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(sessionId)) return map;

        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return map;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Quests == null) return map;

        foreach (var q in pmc.Quests)
        {
            var qid = q.QId.ToString();
            map[qid] = q.Status.ToString();
        }

        return map;
    }
}
