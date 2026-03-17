using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class PmcBotService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<PmcBotService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // Melee parent ID
    private const string MeleeParent = "5447e1d04bdc2dff2f8b4567";

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS
    // ═══════════════════════════════════════════════════════

    // A. PMC Configuration
    private double? _snapBearUsecEnemy;   // pmcbear → UsecEnemyChance (cross-faction for bear)
    private double? _snapUsecBearEnemy;   // pmcusec → BearEnemyChance (cross-faction for usec)
    private double? _snapBearBearEnemy;   // pmcbear → BearEnemyChance (same-faction for bear)
    private double? _snapUsecUsecEnemy;   // pmcusec → UsecEnemyChance (same-faction for usec)
    private double _snapNamePrefixChance;

    // Lootable melee: tpl → original UnlootableFromSide
    private readonly Dictionary<string, List<PlayerSideMask>?> _meleeUnlootableSnapshots = new();

    // B. Bot Equipment Durability
    private record DurabilitySnapshot(int ArmorMinDelta, int ArmorMaxDelta, int WeaponLowestMax, int WeaponHighestMax);
    private readonly Dictionary<string, DurabilitySnapshot> _durabilitySnapshots = new();

    // Bot type labels
    private static readonly (string Key, string Label)[] BotTypes =
    [
        ("pmc", "PMC"),
        ("assault", "Scav"),
        ("boss", "Boss"),
        ("follower", "Follower"),
        ("exusec", "Rogue"),
        ("pmcbot", "Raider"),
        ("marksman", "Marksman")
    ];

    // C. Scav Karma
    private readonly Dictionary<double, bool> _snapFenceBossHostile = new();

    // ═══════════════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            ApplyAll();
        }
    }

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var pmcConfig = configServer.GetConfig<PmcConfig>();
        var botConfig = configServer.GetConfig<BotConfig>();
        var globals = databaseService.GetGlobals();

        // ── A. PMC Hostility ──
        try
        {
            if (pmcConfig.HostilitySettings != null)
            {
                if (pmcConfig.HostilitySettings.TryGetValue("pmcbear", out var bear))
                {
                    _snapBearUsecEnemy = bear.UsecEnemyChance;
                    _snapBearBearEnemy = bear.BearEnemyChance;
                }
                if (pmcConfig.HostilitySettings.TryGetValue("pmcusec", out var usec))
                {
                    _snapUsecBearEnemy = usec.BearEnemyChance;
                    _snapUsecUsecEnemy = usec.UsecEnemyChance;
                }
            }
            _snapNamePrefixChance = pmcConfig.AllPMCsHavePlayerNameWithRandomPrefixChance;
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] PMC hostility snapshot failed: {ex.Message}"); }

        // Lootable melee — snapshot UnlootableFromSide for all melee items
        try
        {
            var items = databaseService.GetItems();
            foreach (var (tpl, item) in items)
            {
                if (item?.Parent != MeleeParent) continue;
                var original = item.Properties?.UnlootableFromSide;
                _meleeUnlootableSnapshots[tpl] = original != null ? original.ToList() : null;
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Melee snapshot failed: {ex.Message}"); }

        // ── B. Bot Equipment Durability ──
        try
        {
            // PMC has separate type PmcDurabilityArmor but same fields
            var pmcDur = botConfig.Durability?.Pmc;
            if (pmcDur != null)
            {
                _durabilitySnapshots["pmc"] = new DurabilitySnapshot(
                    pmcDur.Armor?.MinDelta ?? 0, pmcDur.Armor?.MaxDelta ?? 0,
                    pmcDur.Weapon?.LowestMax ?? 0, pmcDur.Weapon?.HighestMax ?? 0);
            }

            // Other bot types from BotDurabilities dictionary
            if (botConfig.Durability?.BotDurabilities != null)
            {
                foreach (var (key, _) in BotTypes)
                {
                    if (key == "pmc") continue; // already handled
                    if (botConfig.Durability.BotDurabilities.TryGetValue(key, out var dur))
                    {
                        _durabilitySnapshots[key] = new DurabilitySnapshot(
                            dur.Armor?.MinDelta ?? 0, dur.Armor?.MaxDelta ?? 0,
                            dur.Weapon?.LowestMax ?? 0, dur.Weapon?.HighestMax ?? 0);
                    }
                }
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Bot durability snapshot failed: {ex.Message}"); }

        // ── C. Scav Karma — Fence boss hostility per level ──
        try
        {
            var fenceLevels = globals.Configuration.FenceSettings?.Levels;
            if (fenceLevels != null)
            {
                foreach (var (rep, level) in fenceLevels)
                    _snapFenceBossHostile[rep] = level.AreHostileBossesPresent;
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Fence boss hostility snapshot failed: {ex.Message}"); }

        _snapshotTaken = true;
        logger.Info("[ZSlayerHQ] PMC & Bot snapshots taken");
    }

    // ═══════════════════════════════════════════════════════
    // APPLY ALL
    // ═══════════════════════════════════════════════════════

    private void ApplyAll()
    {
        var config = configService.GetConfig().PmcBot;
        ApplyConfig(config);
    }

    private void ApplyConfig(PmcBotConfig c)
    {
        var pmcConfig = configServer.GetConfig<PmcConfig>();
        var botConfig = configServer.GetConfig<BotConfig>();
        var globals = databaseService.GetGlobals();

        // ══════════════ RESTORE ALL from snapshots first ══════════════

        // A. PMC Hostility
        try
        {
            if (pmcConfig.HostilitySettings != null)
            {
                if (pmcConfig.HostilitySettings.TryGetValue("pmcbear", out var bear))
                {
                    bear.UsecEnemyChance = _snapBearUsecEnemy;
                    bear.BearEnemyChance = _snapBearBearEnemy;
                }
                if (pmcConfig.HostilitySettings.TryGetValue("pmcusec", out var usec))
                {
                    usec.BearEnemyChance = _snapUsecBearEnemy;
                    usec.UsecEnemyChance = _snapUsecUsecEnemy;
                }
            }
            pmcConfig.AllPMCsHavePlayerNameWithRandomPrefixChance = _snapNamePrefixChance;
        }
        catch { /* skip */ }

        // Restore melee UnlootableFromSide
        try
        {
            var items = databaseService.GetItems();
            foreach (var (tpl, original) in _meleeUnlootableSnapshots)
            {
                if (!items.TryGetValue(tpl, out var item)) continue;
                if (item?.Properties == null) continue;
                item.Properties.UnlootableFromSide = original != null ? new List<PlayerSideMask>(original) : null;
            }
        }
        catch { /* skip */ }

        // B. Bot Equipment Durability — restore all
        try
        {
            // PMC
            if (_durabilitySnapshots.TryGetValue("pmc", out var pmcSnap) && botConfig.Durability?.Pmc != null)
            {
                if (botConfig.Durability.Pmc.Armor != null)
                {
                    botConfig.Durability.Pmc.Armor.MinDelta = pmcSnap.ArmorMinDelta;
                    botConfig.Durability.Pmc.Armor.MaxDelta = pmcSnap.ArmorMaxDelta;
                }
                if (botConfig.Durability.Pmc.Weapon != null)
                {
                    botConfig.Durability.Pmc.Weapon.LowestMax = pmcSnap.WeaponLowestMax;
                    botConfig.Durability.Pmc.Weapon.HighestMax = pmcSnap.WeaponHighestMax;
                }
            }

            // Other bot types
            if (botConfig.Durability?.BotDurabilities != null)
            {
                foreach (var (key, snap) in _durabilitySnapshots)
                {
                    if (key == "pmc") continue;
                    if (!botConfig.Durability.BotDurabilities.TryGetValue(key, out var dur)) continue;
                    if (dur.Armor != null)
                    {
                        dur.Armor.MinDelta = snap.ArmorMinDelta;
                        dur.Armor.MaxDelta = snap.ArmorMaxDelta;
                    }
                    if (dur.Weapon != null)
                    {
                        dur.Weapon.LowestMax = snap.WeaponLowestMax;
                        dur.Weapon.HighestMax = snap.WeaponHighestMax;
                    }
                }
            }
        }
        catch { /* skip */ }

        // C. Scav Karma — restore fence boss hostility
        try
        {
            var fenceLevels = globals.Configuration.FenceSettings?.Levels;
            if (fenceLevels != null)
            {
                foreach (var (rep, hostile) in _snapFenceBossHostile)
                {
                    if (fenceLevels.TryGetValue(rep, out var level))
                        level.AreHostileBossesPresent = hostile;
                }
            }
        }
        catch { /* skip */ }

        // ══════════════ NOW APPLY user config on top ══════════════

        // A. PMC Configuration
        if (c.EnablePmcHostility)
        {
            try
            {
                if (pmcConfig.HostilitySettings != null)
                {
                    if (c.CrossFactionHostility.HasValue)
                    {
                        if (pmcConfig.HostilitySettings.TryGetValue("pmcbear", out var bear))
                            bear.UsecEnemyChance = c.CrossFactionHostility.Value;
                        if (pmcConfig.HostilitySettings.TryGetValue("pmcusec", out var usec))
                            usec.BearEnemyChance = c.CrossFactionHostility.Value;
                    }
                    if (c.SameFactionHostility.HasValue)
                    {
                        if (pmcConfig.HostilitySettings.TryGetValue("pmcbear", out var bear2))
                            bear2.BearEnemyChance = c.SameFactionHostility.Value;
                        if (pmcConfig.HostilitySettings.TryGetValue("pmcusec", out var usec2))
                            usec2.UsecEnemyChance = c.SameFactionHostility.Value;
                    }
                }
                if (c.PmcNamePrefixChance.HasValue)
                    pmcConfig.AllPMCsHavePlayerNameWithRandomPrefixChance = c.PmcNamePrefixChance.Value;
            }
            catch { /* skip */ }

            // Lootable melee
            if (c.LootableMelee)
            {
                try
                {
                    var items = databaseService.GetItems();
                    foreach (var (tpl, _) in _meleeUnlootableSnapshots)
                    {
                        if (!items.TryGetValue(tpl, out var item)) continue;
                        if (item?.Properties == null) continue;
                        item.Properties.UnlootableFromSide = [];
                    }
                }
                catch { /* skip */ }
            }
        }

        // B. Bot Equipment Durability
        if (c.EnableBotDurability && c.BotDurabilities != null)
        {
            try
            {
                foreach (var (key, entry) in c.BotDurabilities)
                {
                    if (key == "pmc")
                    {
                        // PMC uses separate Pmc durability
                        var pmcDur = botConfig.Durability?.Pmc;
                        if (pmcDur == null) continue;
                        if (entry.ArmorMin.HasValue && pmcDur.Armor != null) pmcDur.Armor.MinDelta = entry.ArmorMin.Value;
                        if (entry.ArmorMax.HasValue && pmcDur.Armor != null) pmcDur.Armor.MaxDelta = entry.ArmorMax.Value;
                        if (entry.WeaponMin.HasValue && pmcDur.Weapon != null) pmcDur.Weapon.LowestMax = entry.WeaponMin.Value;
                        if (entry.WeaponMax.HasValue && pmcDur.Weapon != null) pmcDur.Weapon.HighestMax = entry.WeaponMax.Value;
                    }
                    else
                    {
                        // Standard bot types from BotDurabilities dict
                        if (botConfig.Durability?.BotDurabilities == null) continue;
                        if (!botConfig.Durability.BotDurabilities.TryGetValue(key, out var dur)) continue;
                        if (entry.ArmorMin.HasValue && dur.Armor != null) dur.Armor.MinDelta = entry.ArmorMin.Value;
                        if (entry.ArmorMax.HasValue && dur.Armor != null) dur.Armor.MaxDelta = entry.ArmorMax.Value;
                        if (entry.WeaponMin.HasValue && dur.Weapon != null) dur.Weapon.LowestMax = entry.WeaponMin.Value;
                        if (entry.WeaponMax.HasValue && dur.Weapon != null) dur.Weapon.HighestMax = entry.WeaponMax.Value;
                    }
                }
            }
            catch { /* skip */ }
        }

        // C. Scav Karma
        if (c.EnableScavKarma && c.HostileBossesToScavs.HasValue)
        {
            try
            {
                var fenceLevels = globals.Configuration.FenceSettings?.Levels;
                if (fenceLevels != null)
                {
                    foreach (var (_, level) in fenceLevels)
                        level.AreHostileBossesPresent = c.HostileBossesToScavs.Value;
                }
            }
            catch { /* skip */ }
        }

        logger.Info("[ZSlayerHQ] PMC & Bot settings applied");
    }

    // ═══════════════════════════════════════════════════════
    // GET CONFIG (for frontend)
    // ═══════════════════════════════════════════════════════

    public PmcBotConfigResponse GetConfig()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var config = configService.GetConfig().PmcBot;

            // Cross-faction hostility default = average of bear→usec and usec→bear
            var crossDefault = ((_snapBearUsecEnemy ?? 0) + (_snapUsecBearEnemy ?? 0)) / 2.0;
            var sameDefault = ((_snapBearBearEnemy ?? 0) + (_snapUsecUsecEnemy ?? 0)) / 2.0;

            // Build bot type defaults
            var botTypeDefaults = new List<BotTypeDefaults>();
            foreach (var (key, label) in BotTypes)
            {
                if (_durabilitySnapshots.TryGetValue(key, out var snap))
                {
                    botTypeDefaults.Add(new BotTypeDefaults
                    {
                        Key = key,
                        Label = label,
                        ArmorMin = snap.ArmorMinDelta,
                        ArmorMax = snap.ArmorMaxDelta,
                        WeaponMin = snap.WeaponLowestMax,
                        WeaponMax = snap.WeaponHighestMax
                    });
                }
            }

            // Scav karma default: check if any fence level has bosses hostile (use first level as representative)
            var defaultBossHostile = _snapFenceBossHostile.Values.FirstOrDefault();

            var defaults = new PmcBotDefaults
            {
                // A
                CrossFactionHostility = crossDefault,
                SameFactionHostility = sameDefault,
                PmcNamePrefixChance = _snapNamePrefixChance,
                MeleeItemCount = _meleeUnlootableSnapshots.Count,

                // B
                BotTypes = botTypeDefaults,

                // C
                HostileBossesToScavs = defaultBossHostile
            };

            return new PmcBotConfigResponse { Config = config, Defaults = defaults };
        }
    }

    // ═══════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════

    public void Reset()
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.PmcBot = new PmcBotConfig();
            configService.SaveConfig();
            ApplyConfig(ccConfig.PmcBot);
        }
    }

    // ═══════════════════════════════════════════════════════
    // APPLY FROM REQUEST
    // ═══════════════════════════════════════════════════════

    public void Apply(PmcBotConfig incoming)
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.PmcBot = incoming;
            configService.SaveConfig();
            ApplyConfig(incoming);
        }
    }
}
