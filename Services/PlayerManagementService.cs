using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class PlayerManagementService(
    SaveServer saveServer,
    ProfileHelper profileHelper,
    HandbookHelper handbookHelper,
    TraderHelper traderHelper,
    DatabaseService databaseService,
    LocaleService localeService,
    ProfileActivityService profileActivityService,
    AccessControlService accessControlService,
    ActivityLogService activityLogService,
    MailSendService mailSendService,
    ISptLogger<PlayerManagementService> logger)
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string DollarsTpl = "5696686a4bdc2da3298b456a";
    private const string EurosTpl = "569668774bdc2da2298b4568";

    public PlayerRosterDto GetRoster(string? search, string? faction, string? sortField, string? sortDir)
    {
        var profiles = saveServer.GetProfiles();
        var activeIds = profileActivityService.GetActiveProfileIdsWithinMinutes(5);
        var activeSet = new HashSet<string>(activeIds.Select(id => id.ToString()));

        var players = new List<PlayerRosterEntry>();

        foreach (var (sessionId, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null) continue;

            var sid = sessionId.ToString();
            var nickname = pmc.Info.Nickname ?? "Unknown";
            var side = pmc.Info.Side ?? "";

            // Filter by search
            if (!string.IsNullOrEmpty(search) &&
                !nickname.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            // Filter by faction
            if (!string.IsNullOrEmpty(faction) &&
                !side.Equals(faction, StringComparison.OrdinalIgnoreCase))
                continue;

            var roubles = GetCurrencyCount(pmc.Inventory?.Items, RoublesTpl);

            players.Add(new PlayerRosterEntry
            {
                SessionId = sid,
                Nickname = nickname,
                Level = pmc.Info.Level ?? 0,
                Side = side,
                Experience = pmc.Info.Experience ?? 0,
                Online = activeSet.Contains(sid),
                Roubles = roubles,
                RegistrationDate = (long)pmc.Info.RegistrationDate,
                Banned = accessControlService.IsBanned(sid)
            });
        }

        // Sort
        var field = sortField?.ToLowerInvariant() ?? "level";
        var desc = sortDir?.ToLowerInvariant() == "desc";

        players.Sort((a, b) =>
        {
            // Online always first
            if (a.Online != b.Online) return b.Online.CompareTo(a.Online);

            var cmp = field switch
            {
                "name" => string.Compare(a.Nickname, b.Nickname, StringComparison.OrdinalIgnoreCase),
                "level" => a.Level.CompareTo(b.Level),
                "roubles" => a.Roubles.CompareTo(b.Roubles),
                _ => a.Level.CompareTo(b.Level)
            };

            return desc ? -cmp : cmp;
        });

        return new PlayerRosterDto
        {
            Total = players.Count,
            Players = players
        };
    }

    public PlayerProfileDto? GetProfile(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile))
            return null;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Info == null)
            return null;

        var level = pmc.Info.Level ?? 0;
        var maxLevel = profileHelper.GetMaxLevel();
        var xpToNext = 0;
        if (level < maxLevel)
        {
            var nextXp = Convert.ToInt32(profileHelper.GetExperience(level + 1));
            var curXp = pmc.Info.Experience ?? 0;
            var diff = nextXp - curXp;
            if (diff > 0) xpToNext = diff;
        }

        var items = pmc.Inventory?.Items?.ToList() ?? [];
        var stashValue = handbookHelper.GetTemplatePriceForItems(items);

        var dto = new PlayerProfileDto
        {
            SessionId = sessionId,
            Nickname = pmc.Info.Nickname ?? "Unknown",
            Level = level,
            Side = pmc.Info.Side ?? "",
            Experience = pmc.Info.Experience ?? 0,
            ExperienceToNextLevel = xpToNext,
            StashValue = stashValue,
            Currencies = new PlayerCurrenciesDto
            {
                Roubles = GetCurrencyCount(items, RoublesTpl),
                Dollars = GetCurrencyCount(items, DollarsTpl),
                Euros = GetCurrencyCount(items, EurosTpl)
            }
        };

        // Stats — extract from counters if available
        try
        {
            var stats = pmc.Stats?.Eft;
            dto.OnlineTimeSec = (int)(stats?.TotalInGameTime ?? 0);

            int ammoUsed = 0, hitCount = 0;

            if (stats?.OverallCounters?.Items != null)
            {
                foreach (var counter in stats.OverallCounters.Items)
                {
                    if (counter.Key == null || counter.Key.Count == 0) continue;
                    var k = counter.Key;
                    var val = (int)(counter.Value ?? 0);

                    if (k.Count == 1)
                    {
                        var key = k.First();
                        switch (key)
                        {
                            case "Kills": dto.Kills = val; break;
                            case "KilledPmc": dto.PmcKills = val; break;
                            case "KilledSavage": dto.ScavKills = val; break;
                            case "KilledBoss": dto.BossKills = val; break;
                            case "HeadShots": dto.Headshots = val; break;
                            case "LongestShot": dto.LongestShot = counter.Value ?? 0; break;
                            case "AmmoUsed": ammoUsed = val; break;
                            case "HitCount": hitCount = val; break;
                        }
                    }
                    else
                    {
                        if (k.Contains("Sessions") && k.Contains("Pmc")) dto.TotalRaids = val;
                        else if (k.Contains("ExitStatus") && k.Contains("Survived")) dto.Survived = val;
                        else if (k.Contains("ExitStatus") && k.Contains("Killed")) dto.KIA = val;
                        else if (k.Contains("ExitStatus") && k.Contains("MissingInAction")) dto.MIA = val;
                        else if (k.Contains("ExitStatus") && k.Contains("Runner")) dto.RunThrough = val;
                        else if (k.Contains("LongestWinStreak") && k.Contains("Pmc")) dto.LongestWinStreak = val;
                    }
                }
                dto.SurvivalRate = dto.TotalRaids > 0
                    ? Math.Round((double)dto.Survived / dto.TotalRaids * 100, 1)
                    : 0;
                var deaths = dto.KIA + dto.MIA;
                dto.KdRatio = deaths > 0
                    ? Math.Round((double)dto.Kills / deaths, 2)
                    : dto.Kills;
                dto.OverallAccuracy = ammoUsed > 0
                    ? Math.Round((double)hitCount / ammoUsed * 100, 1)
                    : 0;
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error reading stats for {sessionId}: {ex.Message}");
        }

        // Skills
        try
        {
            var commonSkills = pmc.Skills?.Common;
            if (commonSkills != null)
            {
                dto.Skills = commonSkills.Select(s => new PlayerSkillDto
                {
                    Id = s.Id.ToString(),
                    Progress = s.Progress,
                    Level = (int)(s.Progress / 100),
                    IsElite = s.Progress >= 5100
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error reading skills for {sessionId}: {ex.Message}");
        }

        // Quests
        try
        {
            if (pmc.Quests != null)
            {
                var questTemplates = databaseService.GetQuests();
                var allTraders = databaseService.GetTables().Traders;
                var locale = localeService.GetLocaleDb("en");

                dto.Quests = pmc.Quests.Select(q =>
                {
                    var qid = q.QId.ToString();
                    var questDto = new PlayerQuestDto
                    {
                        QuestId = qid,
                        QuestName = qid,
                        TraderName = "Unknown",
                        Status = q.Status.ToString(),
                        StartTime = (long)q.StartTime,
                        StatusTimers = q.StatusTimers?.ToDictionary(
                            kv => kv.Key.ToString(),
                            kv => (long)kv.Value) ?? new()
                    };

                    try
                    {
                        // Quest name from locale, fallback to QuestName property
                        if (locale.TryGetValue($"{qid} name", out var localeName) && !string.IsNullOrEmpty(localeName))
                            questDto.QuestName = localeName;

                        if (questTemplates.TryGetValue(q.QId, out var questTemplate))
                        {
                            if (questDto.QuestName == qid)
                                questDto.QuestName = questTemplate.QuestName ?? qid;

                            var traderId = questTemplate.TraderId.ToString();
                            if (allTraders.TryGetValue(traderId, out var trader))
                                questDto.TraderName = trader.Base?.Nickname ?? traderId;

                            var completedSet = new HashSet<string>(q.CompletedConditions ?? []);
                            var finishConditions = questTemplate.Conditions?.AvailableForFinish;
                            if (finishConditions != null)
                            {
                                foreach (var cond in finishConditions)
                                {
                                    try
                                    {
                                        var condId = cond.Id.ToString();
                                        locale.TryGetValue(condId, out var localeDesc);

                                        var val = (int)(cond.Value ?? 0);
                                        var desc = !string.IsNullOrEmpty(localeDesc)
                                            ? $"{localeDesc} (0/{val})"
                                            : $"{cond.ConditionType}: (0/{val})";

                                        questDto.Conditions.Add(new QuestConditionDto
                                        {
                                            Type = cond.ConditionType ?? "",
                                            Description = desc,
                                            Value = val,
                                            Completed = completedSet.Contains(condId)
                                        });
                                    }
                                    catch { /* skip malformed conditions */ }
                                }
                            }
                        }
                    }
                    catch { /* quest template lookup failed, keep defaults */ }

                    return questDto;
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error reading quests for {sessionId}: {ex.Message}");
        }

        // Hideout Areas
        try
        {
            if (pmc.Hideout?.Areas != null)
            {
                dto.HideoutAreas = pmc.Hideout.Areas.Select(a => new PlayerHideoutDto
                {
                    AreaType = (int)a.Type,
                    Level = a.Level ?? 0,
                    Active = a.Active ?? false,
                    Constructing = a.Constructing ?? false
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error reading hideout for {sessionId}: {ex.Message}");
        }

        // Traders
        try
        {
            if (pmc.TradersInfo != null)
            {
                var traders = databaseService.GetTables().Traders;
                dto.Traders = pmc.TradersInfo.Select(kv =>
                {
                    var traderName = "Unknown";
                    if (traders.TryGetValue(kv.Key, out var trader))
                        traderName = trader.Base?.Nickname ?? kv.Key;

                    return new PlayerTraderDto
                    {
                        TraderId = kv.Key,
                        TraderName = traderName,
                        LoyaltyLevel = kv.Value.LoyaltyLevel ?? 0,
                        Standing = kv.Value.Standing ?? 0,
                        SalesSum = (long)(kv.Value.SalesSum ?? 0)
                    };
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error reading traders for {sessionId}: {ex.Message}");
        }

        return dto;
    }

    public PlayerFullStatsDto? GetPlayerFullStats(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile))
            return null;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Info == null)
            return null;

        var dto = new PlayerFullStatsDto();

        try
        {
            // Top-level stats fields
            var eftStats = pmc.Stats?.Eft;
            dto.OnlineTimeSec = (int)(eftStats?.TotalInGameTime ?? 0);
            dto.SurvivorClass = eftStats?.SurvivorClass?.ToString() ?? "";
            dto.LastSessionDate = (long)(eftStats?.LastSessionDate ?? 0);
            dto.RegistrationDate = (long)pmc.Info.RegistrationDate;

            // Account lifetime
            if (dto.RegistrationDate > 0)
            {
                var regDate = DateTimeOffset.FromUnixTimeSeconds(dto.RegistrationDate);
                dto.AccountLifetimeDays = (int)(DateTimeOffset.UtcNow - regDate).TotalDays;
            }

            // Stash value
            var items = pmc.Inventory?.Items?.ToList() ?? [];
            dto.StashValue = handbookHelper.GetTemplatePriceForItems(items);

            // Body part damage tracking for least-damaged calculation
            var bodyPartDamage = new Dictionary<string, double>();

            if (eftStats?.OverallCounters?.Items != null)
            {
                foreach (var counter in eftStats.OverallCounters.Items)
                {
                    if (counter.Key == null || counter.Key.Count == 0) continue;
                    var k = counter.Key;
                    var v = (int)(counter.Value ?? 0);
                    var vd = counter.Value ?? 0;

                    if (k.Count == 1)
                    {
                        // Single-key counters
                        var key = k.First();
                        switch (key)
                        {
                            case "Kills": dto.Kills = v; break;
                            case "Deaths": break; // deaths calculated from KIA+MIA
                            case "KilledPmc": dto.PmcsKilled = v; break;
                            case "KilledSavage": dto.ScavsKilled = v; break;
                            case "KilledBoss": dto.BossesKilled = v; break;
                            case "KilledBear": dto.BearsKilled = v; break;
                            case "KilledUsec": dto.UsecsKilled = v; break;
                            case "KilledLevel0010": dto.KilledLevel010 = v; break;
                            case "KilledLevel1030": dto.KilledLevel1130 = v; break;
                            case "KilledLevel3050": dto.KilledLevel3150 = v; break;
                            case "KilledLevel5070": dto.KilledLevel5170 = v; break;
                            case "KilledLevel7099": dto.KilledLevel7199 = v; break;
                            case "KilledLevel100": dto.KilledLevel100 = v; break;
                            case "HeadShots": dto.Headshots = v; break;
                            case "BloodLoss": dto.BloodLost = v; break;
                            case "BodyPartsDestroyed": dto.LimbsLost = v; break;
                            case "Heal": dto.HpHealed = vd; break;
                            case "Fractures": dto.Fractures = v; break;
                            case "Contusions": dto.Concussions = v; break;
                            case "Dehydrations": dto.Dehydrations = v; break;
                            case "Exhaustions": dto.Exhaustions = v; break;
                            case "UsedDrinks": dto.DrinksConsumed = v; break;
                            case "UsedFoods": dto.FoodConsumed = v; break;
                            case "Medicines": dto.MedicineUsed = v; break;
                            case "BodiesLooted": dto.BodiesLooted = v; break;
                            case "SafeLooted": dto.SafesUnlocked = v; break;
                            case "Weapons": dto.WeaponsFound = v; break;
                            case "Mods": dto.ModsFound = v; break;
                            case "ThrowWeapons": dto.ThrowablesFound = v; break;
                            case "SpecialItems": dto.SpecialItemsFound = v; break;
                            case "FoodDrinks": dto.ProvisionsFound = v; break;
                            case "Keys": dto.KeysFound = v; break;
                            case "BartItems": dto.BarterGoodsFound = v; break;
                            case "Equipments": dto.EquipmentFound = v; break;
                            case "AmmoUsed": dto.AmmoUsed = v; break;
                            case "HitCount": dto.HitCount = v; break;
                            case "CauseArmorDamage": dto.DamageAbsorbedByArmor = v; break;
                            case "ExpKill": dto.KillExperience = v; break;
                            case "ExpLooting": dto.LootingExperience = v; break;
                            case "LongestShot": dto.LongestShot = vd; break;
                            case "MobContainers": dto.PlacesLooted = v; break;
                        }
                    }
                    else
                    {
                        // Multi-key counters
                        if (k.Contains("Sessions") && k.Contains("Pmc")) dto.Raids = v;
                        else if (k.Contains("ExitStatus") && k.Contains("Survived")) dto.Survived = v;
                        else if (k.Contains("ExitStatus") && k.Contains("Killed")) dto.KIA = v;
                        else if (k.Contains("ExitStatus") && k.Contains("MissingInAction")) dto.MIA = v;
                        else if (k.Contains("ExitStatus") && k.Contains("Left")) dto.AWOL = v;
                        else if (k.Contains("ExitStatus") && k.Contains("Runner")) dto.RunThroughs = v;
                        else if (k.Contains("LifeTime") && k.Contains("Pmc")) dto.AvgLifeSpanSec = v;
                        else if (k.Contains("CurrentWinStreak") && k.Contains("Pmc")) dto.CurrentWinStreak = v;
                        else if (k.Contains("LongestWinStreak") && k.Contains("Pmc")) dto.LongestWinStreak = v;
                        else if (k.Contains("Exp") && k.Contains("ExpHeal")) dto.HealingExperience = v;
                        else if (k.Contains("Exp") && k.Contains("ExpExitStatus")) dto.SurvivalExperience = v;
                        else if (k.Contains("Money") && k.Contains("RUB")) dto.RubFound = v;
                        else if (k.Contains("Money") && k.Contains("EUR")) dto.EurFound = v;
                        else if (k.Contains("Money") && k.Contains("USD")) dto.UsdFound = v;
                        else if (k.Contains("BodyPartDamage") && k.Count >= 2)
                        {
                            var part = k.FirstOrDefault(x => x != "BodyPartDamage") ?? "";
                            if (part != "") bodyPartDamage[part] = vd;
                        }
                    }
                }
            }

            // Calculated fields
            var deaths = dto.KIA + dto.MIA;
            dto.FatalHits = dto.Kills;
            dto.SurvivalRate = dto.Raids > 0 ? Math.Round((double)dto.Survived / dto.Raids * 100, 1) : 0;
            dto.LeaveRate = dto.Raids > 0 ? Math.Round((double)(dto.AWOL + dto.RunThroughs) / dto.Raids * 100, 1) : 0;
            dto.KdRatio = deaths > 0 ? Math.Round((double)dto.Kills / deaths, 2) : dto.Kills;
            dto.OverallAccuracy = dto.AmmoUsed > 0 ? Math.Round((double)dto.HitCount / dto.AmmoUsed * 100, 1) : 0;

            // AvgLifeSpanSec from LifeTime counter is total — divide by raids for average
            if (dto.Raids > 0 && dto.AvgLifeSpanSec > 0)
                dto.AvgLifeSpanSec = dto.AvgLifeSpanSec / dto.Raids;

            // Least damaged area
            if (bodyPartDamage.Count > 0)
            {
                var min = bodyPartDamage.MinBy(kv => kv.Value);
                dto.LeastDamagedArea = min.Key;
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error building full stats for {sessionId}: {ex.Message}");
        }

        return dto;
    }

    public PlayerStashDto GetStash(string sessionId, string? search, int limit, int offset)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile))
            return new PlayerStashDto();

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Inventory == null)
            return new PlayerStashDto();

        var stashId = pmc.Inventory.Stash.ToString();
        var allItems = pmc.Inventory.Items?.ToList() ?? [];
        var itemDb = databaseService.GetItems();

        // Filter to stash items only (parentId == stash container)
        var stashItems = new List<PlayerStashItemDto>();
        foreach (var i in allItems)
        {
            try
            {
                var parentId = i.ParentId.ToString();
                if (parentId != stashId) continue;

                var tpl = i.Template.ToString();
                var name = tpl;
                if (!string.IsNullOrEmpty(tpl) && itemDb.TryGetValue(tpl, out var template))
                    name = template.Name ?? tpl;

                stashItems.Add(new PlayerStashItemDto
                {
                    Id = i.Id.ToString(),
                    Tpl = tpl,
                    Name = name,
                    Count = (int)(i.Upd?.StackObjectsCount ?? 1)
                });
            }
            catch { /* skip items that can't be read */ }
        }

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            stashItems = stashItems
                .Where(i => i.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            i.Tpl.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = stashItems.Count;
        var stashValue = handbookHelper.GetTemplatePriceForItems(allItems);

        var paged = stashItems
            .Skip(offset)
            .Take(limit)
            .ToList();

        return new PlayerStashDto
        {
            TotalItems = total,
            StashValue = stashValue,
            Items = paged,
            Limit = limit,
            Offset = offset
        };
    }

    public PlayerActionResponse ModifyPlayer(string adminSessionId, string targetSessionId, PlayerModifyRequest request)
    {
        try
        {
            var profiles = saveServer.GetProfiles();
            if (!profiles.TryGetValue(targetSessionId, out var profile))
                return new PlayerActionResponse { Success = false, Error = "Player not found" };

            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null)
                return new PlayerActionResponse { Success = false, Error = "Invalid player profile" };

            var changes = new List<string>();

            // Level
            if (request.Level.HasValue)
            {
                var maxLevel = profileHelper.GetMaxLevel();
                var newLevel = Math.Clamp(request.Level.Value, 1, maxLevel);
                pmc.Info.Level = newLevel;
                pmc.Info.Experience = profileHelper.GetExperience(newLevel);
                changes.Add($"Level → {newLevel}");
            }

            // Experience
            if (request.Experience.HasValue && request.Experience.Value > 0)
            {
                profileHelper.AddExperienceToPmc(targetSessionId, request.Experience.Value);
                changes.Add($"+{request.Experience.Value} XP");
            }

            // Faction
            if (!string.IsNullOrEmpty(request.Faction))
            {
                var side = request.Faction.Equals("Bear", StringComparison.OrdinalIgnoreCase) ? "Bear" : "Usec";
                pmc.Info.Side = side;
                changes.Add($"Faction → {side}");
            }

            // Money — send via mail
            var moneyItems = new List<Item>();
            if (request.Roubles.HasValue && request.Roubles.Value > 0)
            {
                AddCurrencyItems(moneyItems, RoublesTpl, request.Roubles.Value);
                changes.Add($"+{request.Roubles.Value:N0} ₽");
            }
            if (request.Dollars.HasValue && request.Dollars.Value > 0)
            {
                AddCurrencyItems(moneyItems, DollarsTpl, request.Dollars.Value);
                changes.Add($"+{request.Dollars.Value:N0} $");
            }
            if (request.Euros.HasValue && request.Euros.Value > 0)
            {
                AddCurrencyItems(moneyItems, EurosTpl, request.Euros.Value);
                changes.Add($"+{request.Euros.Value:N0} €");
            }
            if (moneyItems.Count > 0)
            {
                mailSendService.SendSystemMessageToPlayer(targetSessionId, "ZSlayer Command Center - Funds", moneyItems);
            }

            // Trader Standings
            if (request.TraderStandings != null)
            {
                foreach (var (traderId, standing) in request.TraderStandings)
                {
                    traderHelper.AddStandingToTrader(targetSessionId, traderId, standing);
                    traderHelper.LevelUp(traderId, pmc);
                    changes.Add($"Trader {traderId[..8]}... standing +{standing:F2}");
                }
            }

            // Save
            _ = saveServer.SaveProfileAsync(targetSessionId);

            var targetName = pmc.Info.Nickname ?? targetSessionId;
            var changesSummary = string.Join(", ", changes);
            activityLogService.LogAction(ActionType.PlayerModify, adminSessionId,
                $"Modified {targetName}: {changesSummary}");

            return new PlayerActionResponse
            {
                Success = true,
                Message = $"Modified {targetName}: {changesSummary}"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error modifying player {targetSessionId}: {ex.Message}");
            return new PlayerActionResponse { Success = false, Error = ex.Message };
        }
    }

    public PlayerActionResponse ResetPlayer(string adminSessionId, string targetSessionId, List<string> categories)
    {
        try
        {
            var profiles = saveServer.GetProfiles();
            if (!profiles.TryGetValue(targetSessionId, out var profile))
                return new PlayerActionResponse { Success = false, Error = "Player not found" };

            var pmc = profile.CharacterData?.PmcData;
            if (pmc == null)
                return new PlayerActionResponse { Success = false, Error = "Invalid player profile" };

            var resetActions = new List<string>();

            var isFull = categories.Contains("full", StringComparer.OrdinalIgnoreCase);

            if (isFull || categories.Contains("skills", StringComparer.OrdinalIgnoreCase))
            {
                if (pmc.Skills?.Common != null)
                {
                    foreach (var skill in pmc.Skills.Common)
                        skill.Progress = 0;
                    resetActions.Add("skills");
                }
            }

            if (isFull || categories.Contains("quests", StringComparer.OrdinalIgnoreCase))
            {
                if (pmc.Quests != null)
                {
                    pmc.Quests.Clear();
                    resetActions.Add("quests");
                }
            }

            if (isFull || categories.Contains("traders", StringComparer.OrdinalIgnoreCase))
            {
                // Reset each trader individually via TraderHelper
                if (pmc.TradersInfo != null)
                {
                    foreach (var traderId in pmc.TradersInfo.Keys.ToList())
                    {
                        try
                        {
                            traderHelper.ResetTrader(targetSessionId, traderId);
                        }
                        catch { /* skip traders that can't be reset */ }
                    }
                }
                resetActions.Add("traders");
            }

            if (isFull || categories.Contains("hideout", StringComparer.OrdinalIgnoreCase))
            {
                if (pmc.Hideout?.Areas != null)
                {
                    foreach (var area in pmc.Hideout.Areas)
                        area.Level = 0;
                    pmc.Hideout.Production?.Clear();
                    resetActions.Add("hideout");
                }
            }

            // Save
            _ = saveServer.SaveProfileAsync(targetSessionId);

            var targetName = pmc.Info?.Nickname ?? targetSessionId;
            var summary = isFull ? "full reset" : string.Join(", ", resetActions);
            activityLogService.LogAction(ActionType.PlayerReset, adminSessionId,
                $"Reset {targetName}: {summary}");

            return new PlayerActionResponse
            {
                Success = true,
                Message = $"Reset {targetName}: {summary}"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error resetting player {targetSessionId}: {ex.Message}");
            return new PlayerActionResponse { Success = false, Error = ex.Message };
        }
    }

    private static long GetCurrencyCount(IEnumerable<Item>? items, string tpl)
    {
        if (items == null) return 0;
        return items
            .Where(i => i.Template.ToString() == tpl)
            .Sum(i => (long)(i.Upd?.StackObjectsCount ?? 1));
    }

    private static void AddCurrencyItems(List<Item> items, string tpl, int amount)
    {
        items.Add(new Item
        {
            Id = new MongoId(),
            Template = tpl,
            Upd = new Upd { StackObjectsCount = amount }
        });
    }

}
