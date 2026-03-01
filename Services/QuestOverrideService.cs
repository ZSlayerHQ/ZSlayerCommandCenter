using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class QuestOverrideService(
    DatabaseService databaseService,
    LocaleService sptLocaleService,
    QuestDiscoveryService discoveryService,
    QuestLocaleService localeService,
    ConfigService configService,
    SaveServer saveServer,
    ISptLogger<QuestOverrideService> logger)
{
    private readonly object _lock = new();

    // ═══════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        // Discovery must run first (snapshots all quests)
        discoveryService.Initialize();

        // Apply config on startup if any overrides exist
        var config = configService.GetConfig().Quests;
        if (HasAnyOverrides(config))
        {
            var result = ApplyConfig();
            logger.Info($"[ZSlayerHQ] Quests: Applied startup overrides — {result.QuestsModified} quests, {result.ObjectivesModified} objectives, {result.RewardsModified} rewards in {result.ApplyTimeMs}ms");
        }
        else
        {
            logger.Info("[ZSlayerHQ] Quests: No overrides configured — using default quest data");
        }
    }

    private static bool HasAnyOverrides(QuestEditorConfig config)
    {
        return config.QuestOverrides.Count > 0
            || config.DisabledQuests.Count > 0
            || Math.Abs(config.GlobalXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.GlobalStandingMultiplier - 1.0) > 0.001
            || Math.Abs(config.GlobalItemRewardMultiplier - 1.0) > 0.001
            || Math.Abs(config.GlobalKillCountMultiplier - 1.0) > 0.001
            || Math.Abs(config.GlobalHandoverCountMultiplier - 1.0) > 0.001
            || config.RemoveFIRRequirements
            || config.GlobalLevelRequirementShift != 0;
    }

    // ═══════════════════════════════════════════════════════════════
    //  APPLY — Main override loop
    // ═══════════════════════════════════════════════════════════════

    public QuestApplyResult ApplyConfig()
    {
        lock (_lock)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var config = configService.GetConfig().Quests;
            var questTemplates = databaseService.GetQuests();
            var snapshots = discoveryService.GetAllSnapshots();

            int questsModified = 0, objectivesModified = 0, rewardsModified = 0;

            try
            {
                // Step 0: Restore all locale entries from snapshots
                localeService.RestoreLocales(snapshots);

                foreach (var (questMongoId, quest) in questTemplates)
                {
                    var qid = questMongoId.ToString();
                    if (!snapshots.TryGetValue(qid, out var snapshot)) continue;

                    // Step 1: Check if disabled → set level to 999
                    if (config.DisabledQuests.Contains(qid))
                    {
                        DisableQuest(quest);
                        questsModified++;
                        continue;
                    }

                    // Step 2: Restore from snapshot (prevent compounding)
                    RestoreQuest(quest, snapshot);

                    var questModified = false;

                    // Step 3: Apply global objective multipliers
                    var objMods = ApplyObjectiveMultipliers(quest, config, snapshot);
                    objectivesModified += objMods;
                    if (objMods > 0) questModified = true;

                    // Step 4: Apply global FIR removal
                    if (config.RemoveFIRRequirements)
                    {
                        var firMods = RemoveFIRRequirements(quest);
                        objectivesModified += firMods;
                        if (firMods > 0) questModified = true;
                    }

                    // Step 5: Apply global level requirement shift
                    if (config.GlobalLevelRequirementShift != 0)
                    {
                        if (ApplyLevelShift(quest, config.GlobalLevelRequirementShift, snapshot))
                        {
                            objectivesModified++;
                            questModified = true;
                        }
                    }

                    // Step 6: Apply global reward multipliers
                    var rwdMods = ApplyRewardMultipliers(quest, config, snapshot);
                    rewardsModified += rwdMods;
                    if (rwdMods > 0) questModified = true;

                    // Step 7: Apply per-quest overrides
                    if (config.QuestOverrides.TryGetValue(qid, out var questOverride))
                    {
                        var (oMods, rMods) = ApplyQuestSpecificOverrides(quest, questOverride, snapshot);
                        objectivesModified += oMods;
                        rewardsModified += rMods;
                        questModified = true;
                    }

                    if (questModified) questsModified++;
                }

                // Step 8: Update locale strings
                var localeMods = localeService.ApplyLocaleOverrides(config, snapshots);
                if (localeMods > 0)
                    logger.Info($"[ZSlayerHQ] Quests: Updated {localeMods} locale strings");

                // Rebuild summaries to reflect changes
                discoveryService.RebuildSummaries();

                sw.Stop();
                return new QuestApplyResult
                {
                    Success = true,
                    QuestsModified = questsModified,
                    ObjectivesModified = objectivesModified,
                    RewardsModified = rewardsModified,
                    ApplyTimeMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Quests: Error applying overrides: {ex.Message}\n{ex.StackTrace}");
                sw.Stop();
                return new QuestApplyResult
                {
                    Success = false,
                    Error = ex.Message,
                    ApplyTimeMs = sw.ElapsedMilliseconds
                };
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  DISABLE / RESTORE
    // ═══════════════════════════════════════════════════════════════

    private static void DisableQuest(Quest quest)
    {
        // Set level requirement to 999 — quest exists but is unreachable
        var startConditions = quest.Conditions?.AvailableForStart;
        if (startConditions == null) return;

        var levelCond = startConditions.FirstOrDefault(c =>
            string.Equals(c.ConditionType, "Level", StringComparison.OrdinalIgnoreCase));

        if (levelCond != null)
        {
            levelCond.Value = 999;
        }
        else
        {
            // No level condition exists — add one
            startConditions.Add(new QuestCondition
            {
                Id = new MongoId(Guid.NewGuid().ToString("N")[..24]),
                ConditionType = "Level",
                Value = 999,
                CompareMethod = ">=",
                DynamicLocale = false
            });
        }
    }

    private static void RestoreQuest(Quest quest, QuestSnapshot snapshot)
    {
        // Restore TraderId
        quest.TraderId = new MongoId(snapshot.TraderId);

        // Restore condition values
        RestoreConditions(quest.Conditions?.AvailableForStart, snapshot);
        RestoreConditions(quest.Conditions?.AvailableForFinish, snapshot);
        RestoreConditions(quest.Conditions?.Fail, snapshot);

        // Restore reward values
        if (quest.Rewards != null)
        {
            foreach (var (_, rewardList) in quest.Rewards)
            {
                if (rewardList == null) continue;
                foreach (var reward in rewardList)
                {
                    var rid = reward.Id.ToString();
                    if (snapshot.Rewards.TryGetValue(rid, out var rs))
                    {
                        reward.Value = rs.Value;
                    }
                }
            }
        }
    }

    private static void RestoreConditions(List<QuestCondition>? conditions, QuestSnapshot snapshot)
    {
        if (conditions == null) return;
        foreach (var cond in conditions)
        {
            var condId = cond.Id.ToString();
            if (snapshot.Conditions.TryGetValue(condId, out var cs))
            {
                cond.Value = cs.Value;
                cond.OnlyFoundInRaid = cs.OnlyFoundInRaid;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  GLOBAL OBJECTIVE MULTIPLIERS
    // ═══════════════════════════════════════════════════════════════

    private static int ApplyObjectiveMultipliers(Quest quest, QuestEditorConfig config, QuestSnapshot snapshot)
    {
        var modified = 0;
        var finishConditions = quest.Conditions?.AvailableForFinish;
        if (finishConditions == null) return 0;

        foreach (var cond in finishConditions)
        {
            var condId = cond.Id.ToString();
            if (!snapshot.Conditions.TryGetValue(condId, out var cs) || cs.Value == null) continue;
            var originalValue = cs.Value.Value;

            switch (cond.ConditionType?.ToLowerInvariant())
            {
                case "countercreator" when Math.Abs(config.GlobalKillCountMultiplier - 1.0) > 0.001:
                    cond.Value = Math.Max(1, Math.Round(originalValue * config.GlobalKillCountMultiplier));
                    modified++;
                    break;

                case "handoveritem" or "finditem" when Math.Abs(config.GlobalHandoverCountMultiplier - 1.0) > 0.001:
                    cond.Value = Math.Max(1, Math.Round(originalValue * config.GlobalHandoverCountMultiplier));
                    modified++;
                    break;
            }
        }

        return modified;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GLOBAL FIR REMOVAL
    // ═══════════════════════════════════════════════════════════════

    private static int RemoveFIRRequirements(Quest quest)
    {
        var modified = 0;
        var finishConditions = quest.Conditions?.AvailableForFinish;
        if (finishConditions == null) return 0;

        foreach (var cond in finishConditions)
        {
            var ct = cond.ConditionType?.ToLowerInvariant();
            if (ct is "handoveritem" or "finditem" && cond.OnlyFoundInRaid == true)
            {
                cond.OnlyFoundInRaid = false;
                modified++;
            }
        }

        return modified;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GLOBAL LEVEL SHIFT
    // ═══════════════════════════════════════════════════════════════

    private static bool ApplyLevelShift(Quest quest, int shift, QuestSnapshot snapshot)
    {
        var startConditions = quest.Conditions?.AvailableForStart;
        if (startConditions == null) return false;

        foreach (var cond in startConditions)
        {
            if (!string.Equals(cond.ConditionType, "Level", StringComparison.OrdinalIgnoreCase)) continue;

            var condId = cond.Id.ToString();
            var originalValue = snapshot.Conditions.TryGetValue(condId, out var cs) ? cs.Value ?? 0 : cond.Value ?? 0;
            cond.Value = Math.Max(1, originalValue + shift);
            return true;
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GLOBAL REWARD MULTIPLIERS
    // ═══════════════════════════════════════════════════════════════

    private static int ApplyRewardMultipliers(Quest quest, QuestEditorConfig config, QuestSnapshot snapshot)
    {
        var modified = 0;
        if (quest.Rewards == null) return 0;

        foreach (var (_, rewardList) in quest.Rewards)
        {
            if (rewardList == null) continue;
            foreach (var reward in rewardList)
            {
                var rid = reward.Id.ToString();
                if (!snapshot.Rewards.TryGetValue(rid, out var rs) || rs.Value == null) continue;
                var originalValue = rs.Value.Value;

                switch (reward.Type)
                {
                    case RewardType.Experience when Math.Abs(config.GlobalXpMultiplier - 1.0) > 0.001:
                        reward.Value = Math.Round(originalValue * config.GlobalXpMultiplier);
                        modified++;
                        break;

                    case RewardType.TraderStanding when Math.Abs(config.GlobalStandingMultiplier - 1.0) > 0.001:
                        reward.Value = originalValue * config.GlobalStandingMultiplier;
                        modified++;
                        break;

                    case RewardType.Item when Math.Abs(config.GlobalItemRewardMultiplier - 1.0) > 0.001:
                        // For item rewards, multiply the stack count of the first item
                        if (reward.Items is { Count: > 0 } && reward.Items[0].Upd != null)
                        {
                            var origCount = (int)(reward.Items[0].Upd.StackObjectsCount ?? 1);
                            reward.Items[0].Upd.StackObjectsCount = Math.Max(1,
                                (int)Math.Ceiling(origCount * config.GlobalItemRewardMultiplier));
                            modified++;
                        }
                        break;
                }
            }
        }

        return modified;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PER-QUEST OVERRIDES
    // ═══════════════════════════════════════════════════════════════

    private (int ObjectiveMods, int RewardMods) ApplyQuestSpecificOverrides(
        Quest quest, QuestOverrideConfig questOverride, QuestSnapshot snapshot)
    {
        int objMods = 0, rwdMods = 0;

        // Trader reassignment
        if (!string.IsNullOrEmpty(questOverride.TraderIdOverride))
        {
            quest.TraderId = new MongoId(questOverride.TraderIdOverride);
        }

        // Level requirement override
        if (questOverride.LevelRequirementOverride.HasValue)
        {
            var startConditions = quest.Conditions?.AvailableForStart;
            if (startConditions != null)
            {
                var levelCond = startConditions.FirstOrDefault(c =>
                    string.Equals(c.ConditionType, "Level", StringComparison.OrdinalIgnoreCase));
                if (levelCond != null)
                {
                    levelCond.Value = Math.Max(1, questOverride.LevelRequirementOverride.Value);
                    objMods++;
                }
            }
        }

        // Remove prerequisites
        if (questOverride.RemovedPrerequisites.Count > 0)
        {
            var startConditions = quest.Conditions?.AvailableForStart;
            if (startConditions != null)
            {
                startConditions.RemoveAll(c =>
                    string.Equals(c.ConditionType, "Quest", StringComparison.OrdinalIgnoreCase) &&
                    c.Target != null &&
                    QuestDiscoveryService.ToStringList(c.Target).Any(t => questOverride.RemovedPrerequisites.Contains(t)));
                objMods++;
            }
        }

        // Per-condition overrides
        foreach (var (conditionId, condOverride) in questOverride.ConditionOverrides)
        {
            var cond = FindConditionById(quest, conditionId);
            if (cond == null) continue;

            if (condOverride.ValueOverride.HasValue)
            {
                cond.Value = condOverride.ValueOverride.Value;
                objMods++;
            }

            if (condOverride.OnlyFoundInRaidOverride.HasValue)
            {
                cond.OnlyFoundInRaid = condOverride.OnlyFoundInRaidOverride.Value;
                objMods++;
            }
        }

        // Per-reward overrides
        foreach (var (rewardId, rewardOverride) in questOverride.RewardOverrides)
        {
            var reward = FindRewardById(quest, rewardId);
            if (reward == null) continue;

            if (rewardOverride.ValueOverride.HasValue)
            {
                reward.Value = rewardOverride.ValueOverride.Value;
                rwdMods++;
            }
        }

        // Remove rewards
        if (questOverride.RemovedRewards.Count > 0 && quest.Rewards != null)
        {
            foreach (var (_, rewardList) in quest.Rewards)
            {
                if (rewardList == null) continue;
                rewardList.RemoveAll(r => questOverride.RemovedRewards.Contains(r.Id.ToString()));
            }
            rwdMods++;
        }

        return (objMods, rwdMods);
    }

    private static QuestCondition? FindConditionById(Quest quest, string conditionId)
    {
        var mid = new MongoId(conditionId);
        return quest.Conditions?.AvailableForStart?.FirstOrDefault(c => c.Id == mid)
            ?? quest.Conditions?.AvailableForFinish?.FirstOrDefault(c => c.Id == mid)
            ?? quest.Conditions?.Fail?.FirstOrDefault(c => c.Id == mid);
    }

    private static Reward? FindRewardById(Quest quest, string rewardId)
    {
        if (quest.Rewards == null) return null;
        var mid = new MongoId(rewardId);
        foreach (var (_, rewardList) in quest.Rewards)
        {
            if (rewardList == null) continue;
            var found = rewardList.FirstOrDefault(r => r.Id == mid);
            if (found != null) return found;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  RESET — Clear all overrides, restore from snapshots
    // ═══════════════════════════════════════════════════════════════

    public QuestApplyResult ResetAll()
    {
        lock (_lock)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var config = configService.GetConfig();
            var questTemplates = databaseService.GetQuests();
            var snapshots = discoveryService.GetAllSnapshots();

            try
            {
                // Restore all locale entries
                localeService.RestoreLocales(snapshots);

                // Restore all quest data
                var restored = 0;
                foreach (var (questMongoId, quest) in questTemplates)
                {
                    var qid = questMongoId.ToString();
                    if (snapshots.TryGetValue(qid, out var snapshot))
                    {
                        RestoreQuest(quest, snapshot);
                        restored++;
                    }
                }

                // Clear config
                config.Quests = new QuestEditorConfig();
                configService.SaveConfig();

                // Rebuild summaries
                discoveryService.RebuildSummaries();

                sw.Stop();
                logger.Info($"[ZSlayerHQ] Quests: Reset all — {restored} quests restored in {sw.ElapsedMilliseconds}ms");

                return new QuestApplyResult
                {
                    Success = true,
                    QuestsModified = restored,
                    ApplyTimeMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Quests: Error resetting: {ex.Message}");
                sw.Stop();
                return new QuestApplyResult { Success = false, Error = ex.Message, ApplyTimeMs = sw.ElapsedMilliseconds };
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PER-PLAYER QUEST STATE
    // ═══════════════════════════════════════════════════════════════

    public object SetPlayerQuestState(string adminSessionId, string questId, string targetSessionId, string status)
    {
        try
        {
            var profiles = saveServer.GetProfiles();
            var targetMongoId = new MongoId(targetSessionId);

            if (!profiles.TryGetValue(targetMongoId, out var profile))
                return new { success = false, error = "Player not found" };

            var pmc = profile.CharacterData?.PmcData;
            if (pmc == null)
                return new { success = false, error = "Player PMC data not found" };

            if (!Enum.TryParse<QuestStatusEnum>(status, ignoreCase: true, out var questStatus))
                return new { success = false, error = $"Invalid quest status: {status}" };

            var questMongoId = new MongoId(questId);
            var playerName = pmc.Info?.Nickname ?? "Unknown";

            // Find existing quest entry
            var existingQuest = pmc.Quests.FirstOrDefault(q => q.QId == questMongoId);
            var previousStatus = existingQuest?.Status.ToString() ?? "NotStarted";

            if (questStatus == QuestStatusEnum.Locked && existingQuest != null)
            {
                // Setting to Locked = remove entry entirely
                pmc.Quests.Remove(existingQuest);
            }
            else if (existingQuest != null)
            {
                existingQuest.Status = questStatus;
                existingQuest.StatusTimers ??= new Dictionary<QuestStatusEnum, double>();
                existingQuest.StatusTimers[questStatus] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                // Create new quest entry
                pmc.Quests.Add(new SPTarkov.Server.Core.Models.Eft.Common.Tables.QuestStatus
                {
                    QId = questMongoId,
                    Status = questStatus,
                    StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CompletedConditions = [],
                    StatusTimers = new Dictionary<QuestStatusEnum, double>
                    {
                        [questStatus] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                });
            }

            _ = saveServer.SaveProfileAsync(targetSessionId);

            // Resolve quest name for response
            var questName = questId;
            try
            {
                var locale = sptLocaleService.GetLocaleDb("en");
                if (locale.TryGetValue($"{questId} name", out var locName) && !string.IsNullOrEmpty(locName))
                    questName = locName;
            }
            catch { /* locale lookup failed, use raw ID */ }

            logger.Info($"[ZSlayerHQ] Admin {adminSessionId} set quest '{questName}' to {status} for player {playerName} ({targetSessionId})");

            return new
            {
                success = true,
                questName,
                playerName,
                previousStatus,
                newStatus = status
            };
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Error setting quest state: {ex.Message}");
            return new { success = false, error = ex.Message };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATUS
    // ═══════════════════════════════════════════════════════════════

    public QuestStatusResponse GetStatus()
    {
        var config = configService.GetConfig().Quests;
        var questTemplates = databaseService.GetQuests();

        return new QuestStatusResponse
        {
            TotalQuests = questTemplates.Count,
            OverrideCount = config.QuestOverrides.Count,
            DisabledCount = config.DisabledQuests.Count,
            GlobalsActive = HasAnyOverrides(config)
        };
    }
}
