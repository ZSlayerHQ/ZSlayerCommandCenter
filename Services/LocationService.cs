using System.Diagnostics;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class LocationService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<LocationService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ── Snapshots (fields NOT managed by ProgressionControlService) ──
    private record LocationSnapshot(int BotMax, int? BotMaxPlayer);
    private record BossSnapshot(int Index, string BossName, double? BossChance, string BossZone,
        string BossEscortAmount, double? Time);
    private record ExitSnapshot(string Name, double? Chance, double? ExfiltrationTime,
        double? ChancePVE, int? Count, string PassageRequirement);
    private record WeatherSnapshot(double? Acceleration);
    private record GlobalRaidSnapshot(int ScavCooldown, double HostileChance,
        double CarExtract, double CoopExtract, double ScavExtract);

    private readonly Dictionary<string, LocationSnapshot> _locationSnapshots = new();
    private readonly Dictionary<string, List<BossSnapshot>> _bossSnapshots = new();
    private readonly Dictionary<string, List<ExitSnapshot>> _exitSnapshots = new();
    private WeatherSnapshot? _weatherSnapshot;
    private GlobalRaidSnapshot? _globalRaidSnapshot;

    // Also snapshot loot/time/bossChance originals for display purposes
    // (ProgressionControlService owns the actual restore for these)
    private readonly Dictionary<string, double?> _snapLootModifier = new();
    private readonly Dictionary<string, double?> _snapContainerModifier = new();
    private readonly Dictionary<string, double?> _snapEscapeTime = new();

    // ── Playable locations ──
    private static readonly string[] PlayableLocationIds =
    [
        "bigmap", "factory4_day", "factory4_night", "interchange",
        "laboratory", "lighthouse", "rezervbase", "sandbox", "sandbox_high",
        "shoreline", "tarkovstreets", "woods", "labyrinth"
    ];

    private static readonly Dictionary<string, string> LocationDisplayNames = new()
    {
        ["bigmap"] = "Customs",
        ["factory4_day"] = "Factory (Day)",
        ["factory4_night"] = "Factory (Night)",
        ["interchange"] = "Interchange",
        ["laboratory"] = "Labs",
        ["lighthouse"] = "Lighthouse",
        ["rezervbase"] = "Reserve",
        ["sandbox"] = "Ground Zero",
        ["sandbox_high"] = "Ground Zero (High)",
        ["shoreline"] = "Shoreline",
        ["tarkovstreets"] = "Streets of Tarkov",
        ["woods"] = "Woods",
        ["labyrinth"] = "Labyrinth"
    };

    private static readonly Dictionary<string, string> MapThumbnails = new()
    {
        ["bigmap"] = "maps/customs_level_0.png",
        ["factory4_day"] = "maps/factory_layer_0.png",
        ["factory4_night"] = "maps/factory_layer_0.png",
        ["interchange"] = "maps/interchange_layer_0.png",
        ["laboratory"] = "maps/labs_layer_0.png",
        ["lighthouse"] = "maps/lighthouse_layer_0.png",
        ["rezervbase"] = "maps/reserve_layer_0.png",
        ["sandbox"] = "maps/ground_zero_layer_0.png",
        ["sandbox_high"] = "maps/ground_zero_layer_0.png",
        ["shoreline"] = "maps/shoreline_layer_0.png",
        ["tarkovstreets"] = "maps/streets_layer_0.png",
        ["woods"] = "maps/woods_layer_0.png",
        ["labyrinth"] = "maps/labyrinth_layer_0.png"
    };

    private static readonly Dictionary<string, string> BossDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bossBully"] = "Reshala",
        ["bossKnight"] = "Knight",
        ["bossPartisan"] = "Partisan",
        ["sectantPriest"] = "Cultist Priest",
        ["bossTagilla"] = "Tagilla",
        ["bossTagillaAgro"] = "Tagilla (Agro)",
        ["bossKilla"] = "Killa",
        ["bossKillaAgro"] = "Killa (Agro)",
        ["bossGluhar"] = "Glukhar",
        ["bossZryachiy"] = "Zryachiy",
        ["bossSanitar"] = "Sanitar",
        ["bossBoar"] = "Kaban",
        ["bossBoarSniper"] = "Kaban (Sniper)",
        ["bossKojaniy"] = "Shturman",
        ["bossKolontay"] = "Kolontay",
        ["peacemaker"] = "Peacemaker",
        ["exUsec"] = "Rogue"
    };

    // Season names for weather UI
    private static readonly Dictionary<int, string> SeasonNames = new()
    {
        [0] = "Summer",
        [1] = "Autumn",
        [2] = "Winter",
        [3] = "Spring",
        [4] = "Late Autumn",
        [5] = "Early Spring",
        [6] = "Storm"
    };

    // ═══════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var count = ApplyAll();
            if (count > 0)
                logger.Success($"[ZSlayerHQ] Locations: applied {count} location override(s)");
            else
                logger.Info("[ZSlayerHQ] Locations: initialized (no overrides)");
        }
    }

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        foreach (var locId in PlayableLocationIds)
        {
            var loc = databaseService.GetLocation(locId);
            if (loc?.Base == null) continue;

            // Snapshot fields we own (bot counts)
            _locationSnapshots[locId] = new LocationSnapshot(
                loc.Base.BotMax,
                loc.Base.BotMaxPlayer);

            // Snapshot originals for display (ProgressionControlService owns restore for these)
            _snapLootModifier[locId] = loc.Base.GlobalLootChanceModifier;
            _snapContainerModifier[locId] = loc.Base.GlobalContainerChanceModifier;
            _snapEscapeTime[locId] = loc.Base.EscapeTimeLimit;

            // Boss snapshots
            if (loc.Base.BossLocationSpawn != null)
            {
                var bossList = new List<BossSnapshot>();
                for (int i = 0; i < loc.Base.BossLocationSpawn.Count; i++)
                {
                    var boss = loc.Base.BossLocationSpawn[i];
                    if (boss.BossName == null) continue;
                    bossList.Add(new BossSnapshot(
                        i,
                        boss.BossName,
                        boss.BossChance,
                        boss.BossZone ?? "",
                        boss.BossEscortAmount ?? "",
                        boss.Time));
                }
                _bossSnapshots[locId] = bossList;
            }

            // Exit snapshots
            if (loc.Base.Exits != null)
            {
                var exitList = new List<ExitSnapshot>();
                foreach (var exit in loc.Base.Exits)
                {
                    exitList.Add(new ExitSnapshot(
                        exit.Name ?? "",
                        exit.Chance,
                        exit.ExfiltrationTime,
                        exit.ChancePVE,
                        exit.Count,
                        exit.PassageRequirement.ToString()));
                }
                _exitSnapshots[locId] = exitList;
            }
        }

        // Weather snapshot
        var weatherCfg = configServer.GetConfig<WeatherConfig>();
        _weatherSnapshot = new WeatherSnapshot(weatherCfg.Acceleration);

        // Global raid settings snapshot
        var inraidCfg = configServer.GetConfig<InRaidConfig>();
        var globals = databaseService.GetGlobals();
        _globalRaidSnapshot = new GlobalRaidSnapshot(
            globals.Configuration.SavagePlayCooldown,
            inraidCfg.PlayerScavHostileChancePercent,
            inraidCfg.CarExtractBaseStandingGain,
            inraidCfg.CoopExtractBaseStandingGain,
            inraidCfg.ScavExtractStandingGain);

        _snapshotTaken = true;
    }

    // ═══════════════════════════════════════════════════════
    // APPLY
    // ═══════════════════════════════════════════════════════

    private int ApplyAll()
    {
        var config = configService.GetConfig().GameValues;
        int count = 0;

        // Apply location overrides
        foreach (var (locId, ov) in config.LocationOverrides)
        {
            count += ApplyLocationOverride(locId, ov);
        }

        // Apply weather
        count += ApplyWeather(config.WeatherOverride);

        // Apply global raid settings
        count += ApplyGlobalRaidSettings(config.GlobalRaidSettings);

        return count;
    }

    private int ApplyLocationOverride(string locId, LocationOverride ov)
    {
        var loc = databaseService.GetLocation(locId);
        if (loc?.Base == null) return 0;
        int count = 0;

        // Bot counts (we own these — restore from snapshot then apply)
        if (_locationSnapshots.TryGetValue(locId, out var snap))
        {
            if (ov.BotMax.HasValue)
            {
                loc.Base.BotMax = ov.BotMax.Value;
                count++;
            }
            else
            {
                loc.Base.BotMax = snap.BotMax;
            }

            if (ov.BotMaxPlayer.HasValue)
            {
                loc.Base.BotMaxPlayer = ov.BotMaxPlayer.Value;
                count++;
            }
            else
            {
                loc.Base.BotMaxPlayer = snap.BotMaxPlayer;
            }
        }

        // Loot/time/bossChance — these OVERLAY on top of ProgressionControlService's values
        // We write absolute values that replace whatever ProgressionControlService set
        if (ov.GlobalLootChanceModifier.HasValue)
        {
            loc.Base.GlobalLootChanceModifier = ov.GlobalLootChanceModifier.Value;
            count++;
        }

        if (ov.GlobalContainerChanceModifier.HasValue)
        {
            loc.Base.GlobalContainerChanceModifier = ov.GlobalContainerChanceModifier.Value;
            count++;
        }

        if (ov.EscapeTimeLimit.HasValue)
        {
            loc.Base.EscapeTimeLimit = ov.EscapeTimeLimit.Value;
            count++;
        }

        // Boss overrides (keyed by index as string)
        if (ov.BossOverrides.Count > 0 && loc.Base.BossLocationSpawn != null)
        {
            foreach (var (indexStr, bossOv) in ov.BossOverrides)
            {
                if (!int.TryParse(indexStr, out var idx) || idx < 0 || idx >= loc.Base.BossLocationSpawn.Count)
                    continue;

                var boss = loc.Base.BossLocationSpawn[idx];
                var bossSnap = _bossSnapshots.GetValueOrDefault(locId)
                    ?.FirstOrDefault(b => b.Index == idx);

                if (bossOv.BossChance.HasValue)
                {
                    boss.BossChance = Math.Clamp(bossOv.BossChance.Value, 0, 100);
                    count++;
                }
                else if (bossSnap != null)
                {
                    boss.BossChance = bossSnap.BossChance;
                }

                if (bossOv.BossEscortAmount != null)
                {
                    boss.BossEscortAmount = bossOv.BossEscortAmount;
                    count++;
                }
                else if (bossSnap != null)
                {
                    boss.BossEscortAmount = bossSnap.BossEscortAmount;
                }

                if (bossOv.Time.HasValue)
                {
                    boss.Time = bossOv.Time.Value;
                    count++;
                }
                else if (bossSnap != null)
                {
                    boss.Time = bossSnap.Time;
                }
            }
        }

        // Exit overrides (keyed by exit name)
        if (ov.ExitOverrides.Count > 0 && loc.Base.Exits != null)
        {
            foreach (var (exitName, exitOv) in ov.ExitOverrides)
            {
                var exit = loc.Base.Exits.FirstOrDefault(e => e.Name == exitName);
                if (exit == null) continue;

                var exitSnap = _exitSnapshots.GetValueOrDefault(locId)
                    ?.FirstOrDefault(e => e.Name == exitName);

                if (exitOv.Chance.HasValue)
                {
                    exit.Chance = exitOv.Chance.Value;
                    count++;
                }
                else if (exitSnap != null)
                {
                    exit.Chance = exitSnap.Chance;
                }

                if (exitOv.ExfiltrationTime.HasValue)
                {
                    exit.ExfiltrationTime = exitOv.ExfiltrationTime.Value;
                    count++;
                }
                else if (exitSnap != null)
                {
                    exit.ExfiltrationTime = exitSnap.ExfiltrationTime;
                }

                if (exitOv.ChancePVE.HasValue)
                {
                    exit.ChancePVE = exitOv.ChancePVE.Value;
                    count++;
                }
                else if (exitSnap != null)
                {
                    exit.ChancePVE = exitSnap.ChancePVE;
                }
            }
        }

        return count;
    }

    private int ApplyWeather(WeatherOverrideConfig weather)
    {
        if (weather.Acceleration == null && weather.SeasonOverride == null)
            return 0;

        var weatherCfg = configServer.GetConfig<WeatherConfig>();
        int count = 0;

        if (weather.Acceleration.HasValue)
        {
            weatherCfg.Acceleration = weather.Acceleration.Value;
            count++;
        }
        else if (_weatherSnapshot != null)
        {
            weatherCfg.Acceleration = _weatherSnapshot.Acceleration;
        }

        if (weather.SeasonOverride.HasValue)
        {
            weatherCfg.OverrideSeason = (Season)weather.SeasonOverride.Value;
            count++;
        }
        else
        {
            weatherCfg.OverrideSeason = null;
        }

        return count;
    }

    private int ApplyGlobalRaidSettings(GlobalRaidSettingsConfig settings)
    {
        var globals = databaseService.GetGlobals();
        var inraidCfg = configServer.GetConfig<InRaidConfig>();
        int count = 0;

        if (settings.ScavCooldownSeconds.HasValue)
        {
            globals.Configuration.SavagePlayCooldown = settings.ScavCooldownSeconds.Value;
            count++;
        }
        else if (_globalRaidSnapshot != null)
        {
            globals.Configuration.SavagePlayCooldown = _globalRaidSnapshot.ScavCooldown;
        }

        if (settings.PlayerScavHostileChancePercent.HasValue)
        {
            inraidCfg.PlayerScavHostileChancePercent = settings.PlayerScavHostileChancePercent.Value;
            count++;
        }
        else if (_globalRaidSnapshot != null)
        {
            inraidCfg.PlayerScavHostileChancePercent = _globalRaidSnapshot.HostileChance;
        }

        if (settings.CarExtractBaseStandingGain.HasValue)
        {
            inraidCfg.CarExtractBaseStandingGain = settings.CarExtractBaseStandingGain.Value;
            count++;
        }
        else if (_globalRaidSnapshot != null)
        {
            inraidCfg.CarExtractBaseStandingGain = _globalRaidSnapshot.CarExtract;
        }

        if (settings.CoopExtractBaseStandingGain.HasValue)
        {
            inraidCfg.CoopExtractBaseStandingGain = settings.CoopExtractBaseStandingGain.Value;
            count++;
        }
        else if (_globalRaidSnapshot != null)
        {
            inraidCfg.CoopExtractBaseStandingGain = _globalRaidSnapshot.CoopExtract;
        }

        if (settings.ScavExtractStandingGain.HasValue)
        {
            inraidCfg.ScavExtractStandingGain = settings.ScavExtractStandingGain.Value;
            count++;
        }
        else if (_globalRaidSnapshot != null)
        {
            inraidCfg.ScavExtractStandingGain = _globalRaidSnapshot.ScavExtract;
        }

        return count;
    }

    /// <summary>
    /// Called after ProgressionControlService re-applies its multipliers.
    /// Re-overlays per-map absolute overrides for loot/time/boss fields.
    /// </summary>
    public void ReapplyOverrides()
    {
        lock (_lock)
        {
            var config = configService.GetConfig().GameValues;
            foreach (var (locId, ov) in config.LocationOverrides)
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base == null) continue;

                // Only re-apply the overlapping fields (loot, time, boss chance)
                if (ov.GlobalLootChanceModifier.HasValue)
                    loc.Base.GlobalLootChanceModifier = ov.GlobalLootChanceModifier.Value;

                if (ov.GlobalContainerChanceModifier.HasValue)
                    loc.Base.GlobalContainerChanceModifier = ov.GlobalContainerChanceModifier.Value;

                if (ov.EscapeTimeLimit.HasValue)
                    loc.Base.EscapeTimeLimit = ov.EscapeTimeLimit.Value;

                if (ov.BossOverrides.Count > 0 && loc.Base.BossLocationSpawn != null)
                {
                    foreach (var (indexStr, bossOv) in ov.BossOverrides)
                    {
                        if (!int.TryParse(indexStr, out var idx) || idx < 0 || idx >= loc.Base.BossLocationSpawn.Count)
                            continue;
                        if (bossOv.BossChance.HasValue)
                            loc.Base.BossLocationSpawn[idx].BossChance = Math.Clamp(bossOv.BossChance.Value, 0, 100);
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET — LOCATIONS LIST
    // ═══════════════════════════════════════════════════════

    public LocationListResponse GetLocations()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;
            var locations = new List<LocationSummaryDto>();
            int totalModified = 0;

            foreach (var locId in PlayableLocationIds)
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base == null) continue;

                var isModified = config.LocationOverrides.ContainsKey(locId);
                if (isModified) totalModified++;

                var bossCount = 0;
                if (loc.Base.BossLocationSpawn != null)
                {
                    bossCount = loc.Base.BossLocationSpawn
                        .Count(b => b.BossName != null && BossDisplayNames.ContainsKey(b.BossName));
                }

                locations.Add(new LocationSummaryDto
                {
                    Id = locId,
                    DisplayName = LocationDisplayNames.GetValueOrDefault(locId, locId),
                    EscapeTimeLimit = loc.Base.EscapeTimeLimit ?? 0,
                    GlobalLootChance = loc.Base.GlobalLootChanceModifier ?? 0,
                    GlobalContainerChance = loc.Base.GlobalContainerChanceModifier ?? 0,
                    BotMax = loc.Base.BotMax,
                    BossCount = bossCount,
                    ExitCount = loc.Base.Exits?.Count() ?? 0,
                    IsModified = isModified,
                    MapThumbnail = MapThumbnails.GetValueOrDefault(locId, "")
                });
            }

            return new LocationListResponse
            {
                Locations = locations,
                DetectedMods = DetectMods(),
                Weather = GetWeatherDto(),
                GlobalRaidSettings = GetGlobalRaidSettingsDto(),
                Presets = GetLocationPresets(),
                TotalModified = totalModified
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET — LOCATION DETAIL
    // ═══════════════════════════════════════════════════════

    public LocationDetailDto? GetLocationDetail(string locId)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var loc = databaseService.GetLocation(locId);
            if (loc?.Base == null) return null;

            var config = configService.GetConfig().GameValues;
            var hasOverride = config.LocationOverrides.ContainsKey(locId);

            // Build boss list
            var bosses = new List<BossDto>();
            if (loc.Base.BossLocationSpawn != null && _bossSnapshots.TryGetValue(locId, out var bossSnaps))
            {
                foreach (var snap in bossSnaps)
                {
                    if (snap.Index >= loc.Base.BossLocationSpawn.Count) continue;
                    if (!BossDisplayNames.ContainsKey(snap.BossName)) continue;

                    var current = loc.Base.BossLocationSpawn[snap.Index];
                    var isModified = false;

                    if (hasOverride && config.LocationOverrides[locId].BossOverrides
                        .TryGetValue(snap.Index.ToString(), out _))
                        isModified = true;

                    bosses.Add(new BossDto
                    {
                        BossName = snap.BossName,
                        DisplayName = BossDisplayNames.GetValueOrDefault(snap.BossName, snap.BossName),
                        BossChance = current.BossChance,
                        BossZone = current.BossZone ?? "",
                        BossEscortAmount = current.BossEscortAmount ?? "",
                        Time = current.Time,
                        OriginalChance = snap.BossChance,
                        OriginalEscortAmount = snap.BossEscortAmount,
                        OriginalTime = snap.Time,
                        IsModified = isModified,
                        Index = snap.Index
                    });
                }
            }

            // Build exit list
            var exits = new List<ExitDto>();
            if (loc.Base.Exits != null && _exitSnapshots.TryGetValue(locId, out var exitSnaps))
            {
                foreach (var snap in exitSnaps)
                {
                    var current = loc.Base.Exits.FirstOrDefault(e => e.Name == snap.Name);
                    if (current == null) continue;

                    var isModified = false;
                    if (hasOverride && config.LocationOverrides[locId].ExitOverrides
                        .ContainsKey(snap.Name))
                        isModified = true;

                    exits.Add(new ExitDto
                    {
                        Name = snap.Name,
                        Chance = current.Chance ?? 0,
                        ExfiltrationTime = current.ExfiltrationTime ?? 0,
                        ChancePVE = current.ChancePVE ?? 0,
                        PassageRequirement = snap.PassageRequirement,
                        OriginalChance = snap.Chance ?? 0,
                        OriginalExfiltrationTime = snap.ExfiltrationTime ?? 0,
                        OriginalChancePVE = snap.ChancePVE ?? 0,
                        IsModified = isModified,
                        Count = current.Count
                    });
                }
            }

            return new LocationDetailDto
            {
                Id = locId,
                DisplayName = LocationDisplayNames.GetValueOrDefault(locId, locId),
                EscapeTimeLimit = loc.Base.EscapeTimeLimit ?? 0,
                GlobalLootChance = loc.Base.GlobalLootChanceModifier ?? 0,
                GlobalContainerChance = loc.Base.GlobalContainerChanceModifier ?? 0,
                BotMax = loc.Base.BotMax,
                BotMaxPlayer = loc.Base.BotMaxPlayer,
                IsModified = hasOverride,
                MapThumbnail = MapThumbnails.GetValueOrDefault(locId, ""),
                Bosses = bosses,
                Exits = exits,
                Original = new LocationOriginalValues
                {
                    EscapeTimeLimit = _snapEscapeTime.GetValueOrDefault(locId) ?? 0,
                    GlobalLootChance = _snapLootModifier.GetValueOrDefault(locId) ?? 0,
                    GlobalContainerChance = _snapContainerModifier.GetValueOrDefault(locId) ?? 0,
                    BotMax = _locationSnapshots.GetValueOrDefault(locId)?.BotMax ?? 0,
                    BotMaxPlayer = _locationSnapshots.GetValueOrDefault(locId)?.BotMaxPlayer ?? 0
                }
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // UPDATE LOCATION
    // ═══════════════════════════════════════════════════════

    public LocationApplyResult UpdateLocation(string locId, LocationUpdateRequest request)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var sw = Stopwatch.StartNew();
            var config = configService.GetConfig().GameValues;

            if (!config.LocationOverrides.TryGetValue(locId, out var ov))
            {
                ov = new LocationOverride();
                config.LocationOverrides[locId] = ov;
            }

            // Merge request into override
            if (request.EscapeTimeLimit.HasValue) ov.EscapeTimeLimit = request.EscapeTimeLimit;
            if (request.GlobalLootChanceModifier.HasValue) ov.GlobalLootChanceModifier = request.GlobalLootChanceModifier;
            if (request.GlobalContainerChanceModifier.HasValue) ov.GlobalContainerChanceModifier = request.GlobalContainerChanceModifier;
            if (request.BotMax.HasValue) ov.BotMax = request.BotMax;
            if (request.BotMaxPlayer.HasValue) ov.BotMaxPlayer = request.BotMaxPlayer;

            if (request.BossOverrides != null)
            {
                foreach (var (key, bOv) in request.BossOverrides)
                    ov.BossOverrides[key] = bOv;
            }

            if (request.ExitOverrides != null)
            {
                foreach (var (key, eOv) in request.ExitOverrides)
                    ov.ExitOverrides[key] = eOv;
            }

            // Clean up: if all fields null, remove the override
            if (IsOverrideEmpty(ov))
                config.LocationOverrides.Remove(locId);

            // Apply
            RestoreLocation(locId);
            if (config.LocationOverrides.TryGetValue(locId, out var finalOv))
                ApplyLocationOverride(locId, finalOv);

            configService.SaveConfig();
            sw.Stop();

            return new LocationApplyResult
            {
                Success = true,
                Message = $"Location {locId} updated in {sw.ElapsedMilliseconds}ms",
                LocationsModified = config.LocationOverrides.Count
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════

    public LocationApplyResult ResetLocation(string locId)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;
            config.LocationOverrides.Remove(locId);

            RestoreLocation(locId);
            configService.SaveConfig();

            return new LocationApplyResult
            {
                Success = true,
                Message = $"Location {locId} reset to defaults",
                LocationsModified = config.LocationOverrides.Count
            };
        }
    }

    public LocationApplyResult ResetAllLocations()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;
            config.LocationOverrides.Clear();

            foreach (var locId in PlayableLocationIds)
                RestoreLocation(locId);

            configService.SaveConfig();

            return new LocationApplyResult
            {
                Success = true,
                Message = "All locations reset to defaults",
                LocationsModified = 0
            };
        }
    }

    private void RestoreLocation(string locId)
    {
        var loc = databaseService.GetLocation(locId);
        if (loc?.Base == null) return;

        // Restore fields we own
        if (_locationSnapshots.TryGetValue(locId, out var snap))
        {
            loc.Base.BotMax = snap.BotMax;
            loc.Base.BotMaxPlayer = snap.BotMaxPlayer;
        }

        // Restore exits
        if (_exitSnapshots.TryGetValue(locId, out var exitSnaps) && loc.Base.Exits != null)
        {
            foreach (var es in exitSnaps)
            {
                var exit = loc.Base.Exits.FirstOrDefault(e => e.Name == es.Name);
                if (exit == null) continue;
                exit.Chance = es.Chance;
                exit.ExfiltrationTime = es.ExfiltrationTime;
                exit.ChancePVE = es.ChancePVE;
            }
        }

        // Restore boss fields we own (escort amount, time — but NOT BossChance, which ProgressionControlService manages)
        if (_bossSnapshots.TryGetValue(locId, out var bossSnaps) && loc.Base.BossLocationSpawn != null)
        {
            foreach (var bs in bossSnaps)
            {
                if (bs.Index >= loc.Base.BossLocationSpawn.Count) continue;
                var boss = loc.Base.BossLocationSpawn[bs.Index];
                boss.BossEscortAmount = bs.BossEscortAmount;
                boss.Time = bs.Time;
            }
        }

        // Note: loot/time/bossChance are NOT restored here — ProgressionControlService handles that
    }

    // ═══════════════════════════════════════════════════════
    // WEATHER
    // ═══════════════════════════════════════════════════════

    public WeatherDto GetWeather()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            return GetWeatherDto();
        }
    }

    public LocationApplyResult UpdateWeather(WeatherUpdateRequest request)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            if (request.Acceleration.HasValue)
                config.WeatherOverride.Acceleration = request.Acceleration;

            // Allow null to clear the season override (back to auto)
            config.WeatherOverride.SeasonOverride = request.SeasonOverride;

            ApplyWeather(config.WeatherOverride);
            configService.SaveConfig();

            return new LocationApplyResult
            {
                Success = true,
                Message = "Weather settings updated (restart client to apply)",
                LocationsModified = 0
            };
        }
    }

    public LocationApplyResult ResetWeather()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;
            config.WeatherOverride = new WeatherOverrideConfig();

            var weatherCfg = configServer.GetConfig<WeatherConfig>();
            if (_weatherSnapshot != null)
            {
                weatherCfg.Acceleration = _weatherSnapshot.Acceleration;
                weatherCfg.OverrideSeason = null;
            }

            configService.SaveConfig();

            return new LocationApplyResult
            {
                Success = true,
                Message = "Weather reset to defaults",
                LocationsModified = 0
            };
        }
    }

    private WeatherDto GetWeatherDto()
    {
        var weatherCfg = configServer.GetConfig<WeatherConfig>();
        var config = configService.GetConfig().GameValues;

        return new WeatherDto
        {
            Acceleration = weatherCfg.Acceleration,
            SeasonOverride = weatherCfg.OverrideSeason.HasValue ? (int)weatherCfg.OverrideSeason.Value : null,
            OriginalAcceleration = _weatherSnapshot?.Acceleration ?? weatherCfg.Acceleration,
            SeasonNames = SeasonNames,
            IsModified = config.WeatherOverride.Acceleration.HasValue || config.WeatherOverride.SeasonOverride.HasValue
        };
    }

    private GlobalRaidSettingsDto GetGlobalRaidSettingsDto()
    {
        var globals = databaseService.GetGlobals();
        var inraidCfg = configServer.GetConfig<InRaidConfig>();
        var config = configService.GetConfig().GameValues;
        var gs = config.GlobalRaidSettings;

        return new GlobalRaidSettingsDto
        {
            ScavCooldownSeconds = globals.Configuration.SavagePlayCooldown,
            PlayerScavHostileChancePercent = inraidCfg.PlayerScavHostileChancePercent,
            CarExtractBaseStandingGain = inraidCfg.CarExtractBaseStandingGain,
            CoopExtractBaseStandingGain = inraidCfg.CoopExtractBaseStandingGain,
            ScavExtractStandingGain = inraidCfg.ScavExtractStandingGain,
            OriginalScavCooldownSeconds = _globalRaidSnapshot?.ScavCooldown ?? globals.Configuration.SavagePlayCooldown,
            OriginalPlayerScavHostileChancePercent = _globalRaidSnapshot?.HostileChance ?? inraidCfg.PlayerScavHostileChancePercent,
            OriginalCarExtractBaseStandingGain = _globalRaidSnapshot?.CarExtract ?? inraidCfg.CarExtractBaseStandingGain,
            OriginalCoopExtractBaseStandingGain = _globalRaidSnapshot?.CoopExtract ?? inraidCfg.CoopExtractBaseStandingGain,
            OriginalScavExtractStandingGain = _globalRaidSnapshot?.ScavExtract ?? inraidCfg.ScavExtractStandingGain,
            IsModified = gs.ScavCooldownSeconds.HasValue || gs.PlayerScavHostileChancePercent.HasValue
                || gs.CarExtractBaseStandingGain.HasValue || gs.CoopExtractBaseStandingGain.HasValue
                || gs.ScavExtractStandingGain.HasValue
        };
    }

    // ═══════════════════════════════════════════════════════
    // GLOBAL RAID SETTINGS — PUBLIC CRUD
    // ═══════════════════════════════════════════════════════

    public GlobalRaidSettingsDto GetGlobalRaidSettings()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            return GetGlobalRaidSettingsDto();
        }
    }

    public LocationApplyResult UpdateGlobalRaidSettings(GlobalRaidSettingsConfig request)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;
            var gs = config.GlobalRaidSettings;

            if (request.ScavCooldownSeconds.HasValue) gs.ScavCooldownSeconds = request.ScavCooldownSeconds;
            if (request.PlayerScavHostileChancePercent.HasValue) gs.PlayerScavHostileChancePercent = request.PlayerScavHostileChancePercent;
            if (request.CarExtractBaseStandingGain.HasValue) gs.CarExtractBaseStandingGain = request.CarExtractBaseStandingGain;
            if (request.CoopExtractBaseStandingGain.HasValue) gs.CoopExtractBaseStandingGain = request.CoopExtractBaseStandingGain;
            if (request.ScavExtractStandingGain.HasValue) gs.ScavExtractStandingGain = request.ScavExtractStandingGain;

            ApplyGlobalRaidSettings(gs);
            configService.SaveConfig();

            return new LocationApplyResult
            {
                Success = true,
                Message = "Global raid settings updated"
            };
        }
    }

    public LocationApplyResult ResetGlobalRaidSettings()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;
            config.GlobalRaidSettings = new GlobalRaidSettingsConfig();

            ApplyGlobalRaidSettings(config.GlobalRaidSettings);
            configService.SaveConfig();

            return new LocationApplyResult
            {
                Success = true,
                Message = "Global raid settings reset to defaults"
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // MOD DETECTION
    // ═══════════════════════════════════════════════════════

    public List<DetectedModDto> DetectMods()
    {
        var modsPath = Path.Combine(configService.ModPath, "..");
        var mods = new List<DetectedModDto>();

        // ABPS
        mods.Add(DetectMod(modsPath, "acidphantasm-botplacementsystem",
            "Acid's Bot Placement System", "com.acidphantasm.botplacementsystem"));

        // APBS
        mods.Add(DetectMod(modsPath, "acidphantasm-progressivebotsystem",
            "Acid's Progressive Bot System", "com.acidphantasm.progressivebotsystem"));

        return mods;
    }

    private static DetectedModDto DetectMod(string modsPath, string folderName, string displayName, string guid)
    {
        var modFolder = Path.Combine(modsPath, folderName);
        var packageJson = Path.Combine(modFolder, "package.json");

        if (!Directory.Exists(modFolder) || !File.Exists(packageJson))
        {
            return new DetectedModDto
            {
                Name = displayName,
                Guid = guid,
                IsDetected = false
            };
        }

        var version = "unknown";
        try
        {
            var json = File.ReadAllText(packageJson);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("version", out var ver))
                version = ver.GetString() ?? "unknown";
        }
        catch { /* ignore parse errors */ }

        return new DetectedModDto
        {
            Name = displayName,
            Guid = guid,
            Version = version,
            WebUiPath = $"/{folderName}/",
            IsDetected = true
        };
    }

    // ═══════════════════════════════════════════════════════
    // LOCATION PRESETS
    // ═══════════════════════════════════════════════════════

    private static readonly string[] PresetNames =
    [
        "Easy Mode", "Hardcore", "Horde Mode", "Loot Run", "Boss Rush", "Vanilla"
    ];

    private static readonly Dictionary<string, string> PresetDescriptions = new()
    {
        ["Easy Mode"] = "All bosses 50%, +50% loot, +10 min raid time",
        ["Hardcore"] = "All bosses 100%, -30% loot, normal raid time",
        ["Horde Mode"] = "Max bots, all bosses 100%, 2x loot",
        ["Loot Run"] = "3x loot, +20 min raid time, all exits 100%",
        ["Boss Rush"] = "All bosses 100% with max escort amounts",
        ["Vanilla"] = "Reset all location overrides to defaults"
    };

    public List<LocationPresetInfo> GetLocationPresets()
    {
        return PresetNames.Select(n => new LocationPresetInfo
        {
            Name = n,
            Description = PresetDescriptions.GetValueOrDefault(n, ""),
            IsBuiltIn = true
        }).ToList();
    }

    public LocationApplyResult ApplyLocationPreset(string presetName)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            if (presetName == "Vanilla")
            {
                config.LocationOverrides.Clear();
                foreach (var locId in PlayableLocationIds)
                    RestoreLocation(locId);
                configService.SaveConfig();
                return new LocationApplyResult { Success = true, Message = "All locations reset to vanilla", LocationsModified = 0 };
            }

            // Generate overrides for each location based on preset
            config.LocationOverrides.Clear();
            foreach (var locId in PlayableLocationIds)
            {
                RestoreLocation(locId);
                var ov = GeneratePresetOverride(locId, presetName);
                if (ov != null && !IsOverrideEmpty(ov))
                    config.LocationOverrides[locId] = ov;
            }

            // Apply all overrides
            foreach (var (locId, ov) in config.LocationOverrides)
                ApplyLocationOverride(locId, ov);

            configService.SaveConfig();
            return new LocationApplyResult
            {
                Success = true,
                Message = $"Applied '{presetName}' preset to {config.LocationOverrides.Count} locations",
                LocationsModified = config.LocationOverrides.Count
            };
        }
    }

    private LocationOverride? GeneratePresetOverride(string locId, string preset)
    {
        var snapTime = _snapEscapeTime.GetValueOrDefault(locId) ?? 0;
        var snapLoot = _snapLootModifier.GetValueOrDefault(locId) ?? 0;
        var snapContainer = _snapContainerModifier.GetValueOrDefault(locId) ?? 0;
        var snapBotMax = _locationSnapshots.GetValueOrDefault(locId)?.BotMax ?? 0;
        var bossSnaps = _bossSnapshots.GetValueOrDefault(locId);

        var ov = new LocationOverride();

        switch (preset)
        {
            case "Easy Mode":
                ov.EscapeTimeLimit = snapTime + 10;
                ov.GlobalLootChanceModifier = Math.Round(snapLoot * 1.5, 2);
                ov.GlobalContainerChanceModifier = Math.Round(snapContainer * 1.5, 2);
                if (bossSnaps != null)
                    foreach (var bs in bossSnaps)
                        if (BossDisplayNames.ContainsKey(bs.BossName))
                            ov.BossOverrides[bs.Index.ToString()] = new BossOverride { BossChance = 50 };
                break;

            case "Hardcore":
                ov.GlobalLootChanceModifier = Math.Round(snapLoot * 0.7, 2);
                ov.GlobalContainerChanceModifier = Math.Round(snapContainer * 0.7, 2);
                if (bossSnaps != null)
                    foreach (var bs in bossSnaps)
                        if (BossDisplayNames.ContainsKey(bs.BossName))
                            ov.BossOverrides[bs.Index.ToString()] = new BossOverride { BossChance = 100 };
                break;

            case "Horde Mode":
                ov.BotMax = Math.Max(snapBotMax, 30);
                ov.GlobalLootChanceModifier = Math.Round(snapLoot * 2, 2);
                ov.GlobalContainerChanceModifier = Math.Round(snapContainer * 2, 2);
                if (bossSnaps != null)
                    foreach (var bs in bossSnaps)
                        if (BossDisplayNames.ContainsKey(bs.BossName))
                            ov.BossOverrides[bs.Index.ToString()] = new BossOverride { BossChance = 100 };
                break;

            case "Loot Run":
                ov.EscapeTimeLimit = snapTime + 20;
                ov.GlobalLootChanceModifier = Math.Round(snapLoot * 3, 2);
                ov.GlobalContainerChanceModifier = Math.Round(snapContainer * 3, 2);
                // All exits 100%
                if (_exitSnapshots.TryGetValue(locId, out var exitSnaps))
                    foreach (var es in exitSnaps)
                        ov.ExitOverrides[es.Name] = new ExitOverride { Chance = 100, ChancePVE = 100 };
                break;

            case "Boss Rush":
                if (bossSnaps != null)
                    foreach (var bs in bossSnaps)
                        if (BossDisplayNames.ContainsKey(bs.BossName))
                            ov.BossOverrides[bs.Index.ToString()] = new BossOverride { BossChance = 100 };
                break;

            default:
                return null;
        }

        return ov;
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════

    private static bool IsOverrideEmpty(LocationOverride ov)
    {
        return !ov.EscapeTimeLimit.HasValue
            && !ov.GlobalLootChanceModifier.HasValue
            && !ov.GlobalContainerChanceModifier.HasValue
            && !ov.BotMax.HasValue
            && !ov.BotMaxPlayer.HasValue
            && ov.BossOverrides.Count == 0
            && ov.ExitOverrides.Count == 0;
    }
}
