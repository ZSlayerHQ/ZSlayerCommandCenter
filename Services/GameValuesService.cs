using System.Diagnostics;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class GameValuesService(
    DatabaseService databaseService,
    LocaleService localeService,
    HandbookHelper handbookHelper,
    ConfigService configService,
    ISptLogger<GameValuesService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS
    // ═══════════════════════════════════════════════════════

    private readonly Dictionary<string, AmmoSnapshot> _ammoSnapshots = new();
    private readonly Dictionary<string, ArmorSnapshot> _armorSnapshots = new();
    private readonly Dictionary<string, WeaponSnapshot> _weaponSnapshots = new();
    private readonly Dictionary<string, MedicalSnapshot> _medicalSnapshots = new();
    private readonly Dictionary<string, BackpackSnapshot> _backpackSnapshots = new();
    private readonly Dictionary<string, List<StimBuffEffectDto>> _stimBuffSnapshots = new();

    private record AmmoSnapshot(
        double Damage, double PenetrationPower, double ArmorDamage,
        double InitialSpeed, double FragmentationChance, double RicochetChance,
        double ProjectileCount, double BulletMassGram, double BallisticCoeficient,
        double MisfireChance, double HeatFactor, double DurabilityBurnModificator,
        double LightBleedingDelta, double HeavyBleedingDelta, double StaminaBurnPerDamage,
        double AmmoAccr, double AmmoRec);

    private record ArmorSnapshot(
        int ArmorClass, double Durability, double MaxDurability,
        double BluntThroughput, double SpeedPenaltyPercent, double MousePenalty,
        double WeaponErgonomicPenalty, string ArmorMaterial, string ArmorType,
        bool IsPlate);

    private record WeaponSnapshot(
        double Ergonomics, double RecoilForceUp, double RecoilForceBack,
        double RecoilAngle, double BFirerate, double CenterOfImpact,
        double SightingRange, double Durability, double MaxDurability,
        double HeatFactorGun, double CoolFactorGun, double BaseMalfunctionChance,
        double Velocity, double DeviationMax,
        string WeapClass, string AmmoCaliber, List<string> FireTypes, bool BoltAction);

    private record MedicalSnapshot(
        double MaxHpResource, double HpResourceRate, double MedUseTime,
        double? LightBleedingCost, double? HeavyBleedingCost, double? FractureCost,
        double? PainDuration, double? ContusionDuration,
        double? EnergyChange, double? HydrationChange,
        string MedType, string? StimBuffName, List<string> Treats);

    private record BackpackSnapshot(
        double Weight, double SpeedPenaltyPercent,
        double WeaponErgonomicPenalty, double MousePenalty,
        int GridWidth, int GridHeight, int TotalSlots,
        bool IsMultiGrid, string GridLayout);

    private const string BackpackParentId = "5448e53e4bdc2d60728b4567";

    // Parent IDs for medical/consumable item detection
    private static readonly Dictionary<string, string> MedicalParentIds = new()
    {
        ["5448f39d4bdc2d0a728b4568"] = "Medkit",
        ["5448f3ac4bdc2dce718b4569"] = "Medical",
        ["5448f3a14bdc2d27728b4569"] = "Drug",
        ["5448f3a64bdc2d60728b456a"] = "Stimulant",
        ["5448e8d04bdc2ddf718b4569"] = "Food",
        ["5448e8d64bdc2dce718b4568"] = "Drink",
    };

    // ═══════════════════════════════════════════════════════
    // CALIBER DISPLAY NAMES
    // ═══════════════════════════════════════════════════════

    private static readonly Dictionary<string, string> CaliberDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Caliber9x18PM"] = "9x18mm PM",
        ["Caliber9x18PMM"] = "9x18mm PMM",
        ["Caliber9x19PARA"] = "9x19mm",
        ["Caliber9x21"] = "9x21mm",
        ["Caliber9x33R"] = ".357 Magnum",
        ["Caliber9x39"] = "9x39mm",
        ["Caliber46x30"] = "4.6x30mm",
        ["Caliber57x28"] = "5.7x28mm",
        ["Caliber545x39"] = "5.45x39mm",
        ["Caliber556x45NATO"] = "5.56x45mm NATO",
        ["Caliber762x25TT"] = "7.62x25mm TT",
        ["Caliber762x35"] = ".300 Blackout",
        ["Caliber762x39"] = "7.62x39mm",
        ["Caliber762x51"] = "7.62x51mm NATO",
        ["Caliber762x54R"] = "7.62x54mmR",
        ["Caliber86x70"] = ".338 Lapua",
        ["Caliber127x55"] = "12.7x55mm",
        ["Caliber12g"] = "12 Gauge",
        ["Caliber20g"] = "20 Gauge",
        ["Caliber23x75"] = "23x75mm",
        ["Caliber366TKM"] = ".366 TKM",
        ["Caliber40x46"] = "40x46mm",
        ["Caliber1143x23ACP"] = ".45 ACP",
        ["Caliber40mmRU"] = "40mm RU",
        ["Caliber26x75"] = "26x75mm Flare",
        ["Caliber68x51"] = "6.8x51mm",
    };

    // ═══════════════════════════════════════════════════════
    // WEAPON CLASS DISPLAY NAMES
    // ═══════════════════════════════════════════════════════

    private static readonly Dictionary<string, string> WeapClassDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["assaultRifle"] = "Assault Rifle",
        ["assaultCarbine"] = "Assault Carbine",
        ["machinegun"] = "Machine Gun",
        ["smg"] = "SMG",
        ["pistol"] = "Pistol",
        ["marksmanRifle"] = "Marksman Rifle",
        ["sniperRifle"] = "Sniper Rifle",
        ["shotgun"] = "Shotgun",
        ["grenadeLauncher"] = "Grenade Launcher",
        ["specialWeapon"] = "Special Weapon",
        ["revolver"] = "Revolver",
    };

    // ═══════════════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            GenerateBalancePresets();
            ApplyAll();
            var config = configService.GetConfig().GameValues;
            var total = config.AmmoOverrides.Count + config.ArmorOverrides.Count + config.WeaponOverrides.Count + config.MedicalOverrides.Count + config.BackpackOverrides.Count + config.StimBuffOverrides.Count;
            if (total > 0)
                logger.Success($"[ZSlayerHQ] Game Values: applied {total} overrides ({config.AmmoOverrides.Count} ammo, {config.ArmorOverrides.Count} armor, {config.WeaponOverrides.Count} weapons, {config.MedicalOverrides.Count} medical, {config.BackpackOverrides.Count} backpacks)");
            else
                logger.Info("[ZSlayerHQ] Game Values: initialized (no overrides)");
        }
    }

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var items = databaseService.GetItems();
        var ammoCount = 0;
        var armorCount = 0;
        var weaponCount = 0;
        var medicalCount = 0;
        var backpackCount = 0;

        foreach (var (id, template) in items)
        {
            if (template.Type != "Item") continue;
            var props = template.Properties;
            if (props == null) continue;
            var tpl = id.ToString();

            // Ammo: has Caliber + Damage
            if (props.Caliber != null && props.Damage != null)
            {
                _ammoSnapshots[tpl] = new AmmoSnapshot(
                    Damage: props.Damage ?? 0,
                    PenetrationPower: props.PenetrationPower ?? 0,
                    ArmorDamage: props.ArmorDamage ?? 0,
                    InitialSpeed: props.InitialSpeed ?? 0,
                    FragmentationChance: props.FragmentationChance ?? 0,
                    RicochetChance: props.RicochetChance ?? 0,
                    ProjectileCount: props.ProjectileCount ?? 0,
                    BulletMassGram: props.BulletMassGram ?? 0,
                    BallisticCoeficient: props.BallisticCoeficient ?? 0,
                    MisfireChance: props.MisfireChance ?? 0,
                    HeatFactor: props.HeatFactor ?? 0,
                    DurabilityBurnModificator: props.DurabilityBurnModificator ?? 0,
                    LightBleedingDelta: props.LightBleedingDelta ?? 0,
                    HeavyBleedingDelta: props.HeavyBleedingDelta ?? 0,
                    StaminaBurnPerDamage: props.StaminaBurnPerDamage ?? 0,
                    AmmoAccr: props.AmmoAccr ?? 0,
                    AmmoRec: props.AmmoRec ?? 0
                );
                ammoCount++;
            }

            // Armor: has ArmorClass set, OR has ArmorMaterial + durability (catches plates)
            var hasArmorClass = props.ArmorClass is > 0;
            var hasArmorMaterial = props.ArmorMaterial != null && (props.MaxDurability ?? 0) > 0;
            if (hasArmorClass || hasArmorMaterial)
            {
                // Detect plates: item name contains "plate" or parent chain is armor plate category
                var nameKey = (template.Name ?? "").ToLowerInvariant();
                var isPlate = nameKey.Contains("plate") || nameKey.Contains("_plate_")
                    || tpl.Contains("plate", StringComparison.OrdinalIgnoreCase);

                _armorSnapshots[tpl] = new ArmorSnapshot(
                    ArmorClass: props.ArmorClass ?? 0,
                    Durability: props.Durability ?? 0,
                    MaxDurability: props.MaxDurability ?? 0,
                    BluntThroughput: props.BluntThroughput ?? 0,
                    SpeedPenaltyPercent: props.SpeedPenaltyPercent ?? 0,
                    MousePenalty: props.MousePenalty ?? 0,
                    WeaponErgonomicPenalty: props.WeaponErgonomicPenalty ?? 0,
                    ArmorMaterial: props.ArmorMaterial?.ToString() ?? "",
                    ArmorType: props.ArmorType ?? "",
                    IsPlate: isPlate
                );
                armorCount++;
            }

            // Weapons: has WeapClass
            if (!string.IsNullOrEmpty(props.WeapClass))
            {
                _weaponSnapshots[tpl] = new WeaponSnapshot(
                    Ergonomics: props.Ergonomics ?? 0,
                    RecoilForceUp: props.RecoilForceUp ?? 0,
                    RecoilForceBack: props.RecoilForceBack ?? 0,
                    RecoilAngle: props.RecoilAngle ?? 0,
                    BFirerate: props.BFirerate ?? 0,
                    CenterOfImpact: props.CenterOfImpact ?? 0,
                    SightingRange: props.SightingRange ?? 0,
                    Durability: props.Durability ?? 0,
                    MaxDurability: props.MaxDurability ?? 0,
                    HeatFactorGun: props.HeatFactorGun ?? 0,
                    CoolFactorGun: props.CoolFactorGun ?? 0,
                    BaseMalfunctionChance: props.BaseMalfunctionChance ?? 0,
                    Velocity: props.Velocity ?? 0,
                    DeviationMax: props.DeviationMax ?? 0,
                    WeapClass: props.WeapClass ?? "",
                    AmmoCaliber: props.AmmoCaliber ?? "",
                    FireTypes: props.WeapFireType?.ToList() ?? [],
                    BoltAction: props.BoltAction ?? false
                );
                weaponCount++;
            }

            // Medical / Food / Drink: check parent ID
            var parentId = template.Parent.ToString();
            if (MedicalParentIds.TryGetValue(parentId, out var medType))
            {
                var treats = new List<string>();

                // Extract effects from EffectsDamage dict
                double? lightBleedCost = null, heavyBleedCost = null, fractureCost = null;
                double? painDur = null, contusionDur = null;
                if (props.EffectsDamage != null)
                {
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.LightBleeding, out var lb))
                    { lightBleedCost = lb.Cost; treats.Add("LBleed"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.HeavyBleeding, out var hb))
                    { heavyBleedCost = hb.Cost; treats.Add("HBleed"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.Fracture, out var fr))
                    { fractureCost = fr.Cost; treats.Add("Fracture"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.Pain, out var pn))
                    { painDur = pn.Duration; treats.Add("Pain"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.Contusion, out var cn))
                    { contusionDur = cn.Duration; treats.Add("Contusion"); }
                }

                // Extract health effects
                double? energyChange = null, hydrationChange = null;
                if (props.EffectsHealth != null)
                {
                    if (props.EffectsHealth.TryGetValue(HealthFactor.Energy, out var en))
                    { energyChange = en.Value; }
                    if (props.EffectsHealth.TryGetValue(HealthFactor.Hydration, out var hy))
                    { hydrationChange = hy.Value; }
                }

                _medicalSnapshots[tpl] = new MedicalSnapshot(
                    MaxHpResource: props.MaxHpResource ?? 0,
                    HpResourceRate: props.HpResourceRate ?? 0,
                    MedUseTime: props.MedUseTime ?? 0,
                    LightBleedingCost: lightBleedCost,
                    HeavyBleedingCost: heavyBleedCost,
                    FractureCost: fractureCost,
                    PainDuration: painDur,
                    ContusionDuration: contusionDur,
                    EnergyChange: energyChange,
                    HydrationChange: hydrationChange,
                    MedType: medType,
                    StimBuffName: props.StimulatorBuffs,
                    Treats: treats
                );
                medicalCount++;
            }

            // Backpack: parent is BackpackParentId
            if (template.Parent.ToString() == BackpackParentId)
            {
                var grids = props.Grids?.ToList() ?? [];
                var totalSlots = 0;
                var layoutParts = new List<string>();
                var firstW = 0;
                var firstH = 0;
                for (var gi = 0; gi < grids.Count; gi++)
                {
                    var gp = grids[gi].Properties;
                    var cw = gp?.CellsH ?? 0;
                    var ch = gp?.CellsV ?? 0;
                    totalSlots += cw * ch;
                    layoutParts.Add($"{cw}\u00d7{ch}");
                    if (gi == 0) { firstW = cw; firstH = ch; }
                }
                var isMultiGrid = grids.Count > 1;
                _backpackSnapshots[tpl] = new BackpackSnapshot(
                    Weight: props.Weight ?? 0,
                    SpeedPenaltyPercent: props.SpeedPenaltyPercent ?? 0,
                    WeaponErgonomicPenalty: props.WeaponErgonomicPenalty ?? 0,
                    MousePenalty: props.MousePenalty ?? 0,
                    GridWidth: firstW,
                    GridHeight: firstH,
                    TotalSlots: totalSlots,
                    IsMultiGrid: isMultiGrid,
                    GridLayout: string.Join("+", layoutParts)
                );
                backpackCount++;
            }
        }

        // Snapshot stim buff definitions from globals
        var stimBuffCount = 0;
        try
        {
            var buffsDict = databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            if (buffsDict != null)
            {
                foreach (var (buffName, buffList) in buffsDict)
                {
                    if (buffList == null) continue;
                    _stimBuffSnapshots[buffName] = buffList.Select(b => new StimBuffEffectDto
                    {
                        BuffType = b.BuffType ?? "",
                        Value = b.Value,
                        Duration = b.Duration,
                        Delay = b.Delay,
                        Chance = b.Chance,
                        AbsoluteValue = b.AbsoluteValue,
                        SkillName = b.SkillName ?? "",
                    }).ToList();
                    stimBuffCount++;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to snapshot stim buffs: {ex.Message}");
        }

        _snapshotTaken = true;
        logger.Info($"[ZSlayerHQ] Game Values: snapshotted {ammoCount} ammo, {armorCount} armor, {weaponCount} weapons, {medicalCount} medical, {backpackCount} backpacks, {stimBuffCount} stim buffs");
    }

    // ═══════════════════════════════════════════════════════
    // BALANCE PRESETS (generated from snapshot data + prices)
    // ═══════════════════════════════════════════════════════

    private void GenerateBalancePresets()
    {
        try
        {
            // ── Balanced Economy: adjust outliers toward median value score ──
            var balancedAmmo = new Dictionary<string, AmmoOverride>();
            var balancedArmor = new Dictionary<string, ArmorOverride>();
            var balancedWeapons = new Dictionary<string, WeaponOverride>();

            // Ammo: value = (damage + pen) / price
            var ammoScores = new List<(string tpl, double score, AmmoSnapshot snap)>();
            foreach (var (tpl, snap) in _ammoSnapshots)
            {
                var price = handbookHelper.GetTemplatePrice(tpl);
                if (price <= 0) continue;
                ammoScores.Add((tpl, (snap.Damage + snap.PenetrationPower) / price, snap));
            }
            if (ammoScores.Count > 0)
            {
                ammoScores.Sort((a, b) => a.score.CompareTo(b.score));
                var medianScore = ammoScores[ammoScores.Count / 2].score;
                var lowThreshold = ammoScores[(int)(ammoScores.Count * 0.15)].score;
                var highThreshold = ammoScores[(int)(ammoScores.Count * 0.85)].score;
                foreach (var (tpl, score, snap) in ammoScores)
                {
                    if (score > highThreshold)
                    {
                        // Cheap but powerful: nerf damage by 10-20%
                        var factor = 1.0 - (0.10 + 0.10 * ((score - highThreshold) / (ammoScores[^1].score - highThreshold + 0.001)));
                        factor = Math.Max(factor, 0.75);
                        balancedAmmo[tpl] = new AmmoOverride { Damage = Math.Round(snap.Damage * factor, 1) };
                    }
                    else if (score < lowThreshold)
                    {
                        // Expensive but weak: buff damage by 10-25%
                        var factor = 1.0 + (0.10 + 0.15 * ((lowThreshold - score) / (lowThreshold - ammoScores[0].score + 0.001)));
                        factor = Math.Min(factor, 1.25);
                        balancedAmmo[tpl] = new AmmoOverride { Damage = Math.Round(snap.Damage * factor, 1) };
                    }
                }
            }

            // Armor: value = (class * durability) / price
            var armorScores = new List<(string tpl, double score, ArmorSnapshot snap)>();
            foreach (var (tpl, snap) in _armorSnapshots)
            {
                var price = handbookHelper.GetTemplatePrice(tpl);
                if (price <= 0 || snap.ArmorClass <= 0) continue;
                armorScores.Add((tpl, (snap.ArmorClass * snap.Durability) / price, snap));
            }
            if (armorScores.Count > 0)
            {
                armorScores.Sort((a, b) => a.score.CompareTo(b.score));
                var highThreshold = armorScores[(int)(armorScores.Count * 0.85)].score;
                var lowThreshold = armorScores[(int)(armorScores.Count * 0.15)].score;
                foreach (var (tpl, score, snap) in armorScores)
                {
                    if (score > highThreshold)
                    {
                        var factor = Math.Max(0.80, 1.0 - 0.20 * ((score - highThreshold) / (armorScores[^1].score - highThreshold + 0.001)));
                        balancedArmor[tpl] = new ArmorOverride { Durability = Math.Round(snap.Durability * factor, 1), MaxDurability = Math.Round(snap.MaxDurability * factor, 1) };
                    }
                    else if (score < lowThreshold)
                    {
                        var factor = Math.Min(1.20, 1.0 + 0.20 * ((lowThreshold - score) / (lowThreshold - armorScores[0].score + 0.001)));
                        balancedArmor[tpl] = new ArmorOverride { Durability = Math.Round(snap.Durability * factor, 1), MaxDurability = Math.Round(snap.MaxDurability * factor, 1) };
                    }
                }
            }

            _dynamicPresets["Defaults — Balanced Economy"] = new GameValuesPresetEntry
            {
                Description = "Adjusts outlier items toward median value-per-ruble. Nerfs cheap OP items, buffs overpriced weak items",
                Category = "all",
                AmmoOverrides = balancedAmmo,
                ArmorOverrides = balancedArmor,
                WeaponOverrides = new(),
                MedicalOverrides = new(),
                BackpackOverrides = new(),
            };

            // ── Budget Friendly: buff lower-tier items ──
            var budgetAmmo = new Dictionary<string, AmmoOverride>();
            foreach (var (tpl, snap) in _ammoSnapshots)
            {
                var price = handbookHelper.GetTemplatePrice(tpl);
                if (price <= 0) continue;
                // Buff cheap ammo (bottom 50% by price) damage by 15%
                if (price < 300)
                    budgetAmmo[tpl] = new AmmoOverride { Damage = Math.Round(snap.Damage * 1.15, 1) };
            }

            var budgetArmor = new Dictionary<string, ArmorOverride>();
            foreach (var (tpl, snap) in _armorSnapshots)
            {
                if (snap.ArmorClass <= 3)
                    budgetArmor[tpl] = new ArmorOverride { Durability = Math.Round(snap.Durability * 1.20, 1), MaxDurability = Math.Round(snap.MaxDurability * 1.20, 1) };
            }

            _dynamicPresets["Defaults — Budget Friendly"] = new GameValuesPresetEntry
            {
                Description = "Buffs lower-tier items to be more competitive without touching top-tier",
                Category = "all",
                AmmoOverrides = budgetAmmo,
                ArmorOverrides = budgetArmor,
                WeaponOverrides = new(),
                MedicalOverrides = new(),
                BackpackOverrides = new(),
            };

            // ── Hardcore Realism: nerfs top-tier, increases penalties ──
            var hardcoreAmmo = new Dictionary<string, AmmoOverride>();
            foreach (var (tpl, snap) in _ammoSnapshots)
            {
                // High-pen rounds (pen > 40): reduce damage by 20%
                if (snap.PenetrationPower > 40)
                    hardcoreAmmo[tpl] = new AmmoOverride { Damage = Math.Round(snap.Damage * 0.80, 1) };
            }

            var hardcoreArmor = new Dictionary<string, ArmorOverride>();
            foreach (var (tpl, snap) in _armorSnapshots)
            {
                // Top-tier armor (class 5+): reduce durability by 15%
                if (snap.ArmorClass >= 5)
                    hardcoreArmor[tpl] = new ArmorOverride { Durability = Math.Round(snap.Durability * 0.85, 1), MaxDurability = Math.Round(snap.MaxDurability * 0.85, 1) };
            }

            var hardcoreWeapons = new Dictionary<string, WeaponOverride>();
            foreach (var (tpl, snap) in _weaponSnapshots)
            {
                // All weapons: +15% recoil
                hardcoreWeapons[tpl] = new WeaponOverride
                {
                    RecoilForceUp = Math.Round(snap.RecoilForceUp * 1.15, 1),
                    RecoilForceBack = Math.Round(snap.RecoilForceBack * 1.15, 1),
                };
            }

            _dynamicPresets["Defaults — Hardcore Realism"] = new GameValuesPresetEntry
            {
                Description = "Nerfs high-pen ammo, reduces top-tier armor durability, increases all weapon recoil",
                Category = "all",
                AmmoOverrides = hardcoreAmmo,
                ArmorOverrides = hardcoreArmor,
                WeaponOverrides = hardcoreWeapons,
                MedicalOverrides = new(),
                BackpackOverrides = new(),
            };

            logger.Info($"[ZSlayerHQ] Game Values: generated 3 balance presets ({balancedAmmo.Count} balanced ammo, {budgetAmmo.Count} budget ammo, {hardcoreAmmo.Count} hardcore ammo)");
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Game Values: failed to generate balance presets: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET AMMO
    // ═══════════════════════════════════════════════════════

    public AmmoListResponse GetAmmo(string? search, string? caliber)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var items = databaseService.GetItems();
            var locales = localeService.GetLocaleDb("en");
            var config = configService.GetConfig().GameValues;
            var result = new List<AmmoDto>();
            var caliberCounts = new Dictionary<string, int>();

            foreach (var (tpl, snap) in _ammoSnapshots)
            {
                if (!items.TryGetValue(tpl, out var template)) continue;
                var props = template.Properties;
                if (props == null) continue;

                var calKey = props.Caliber ?? "";
                caliberCounts.TryGetValue(calKey, out var cnt);
                caliberCounts[calKey] = cnt + 1;

                // Caliber filter
                if (!string.IsNullOrEmpty(caliber) && !calKey.Equals(caliber, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Name lookup
                locales.TryGetValue($"{tpl} ShortName", out var shortName);
                locales.TryGetValue($"{tpl} Name", out var fullName);
                if (string.IsNullOrEmpty(fullName)) fullName = template.Name ?? tpl;
                if (string.IsNullOrEmpty(shortName)) shortName = fullName;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var q = search.ToLowerInvariant();
                    if (!fullName.ToLowerInvariant().Contains(q) &&
                        !shortName.ToLowerInvariant().Contains(q) &&
                        !tpl.ToLowerInvariant().Contains(q))
                        continue;
                }

                var isModified = config.AmmoOverrides.ContainsKey(tpl);
                CaliberDisplayNames.TryGetValue(calKey, out var calDisplay);

                result.Add(new AmmoDto
                {
                    Tpl = tpl,
                    ShortName = shortName,
                    FullName = fullName,
                    Caliber = calKey,
                    CaliberDisplay = calDisplay ?? calKey,
                    Damage = props.Damage ?? 0,
                    PenetrationPower = props.PenetrationPower ?? 0,
                    ArmorDamage = props.ArmorDamage ?? 0,
                    InitialSpeed = props.InitialSpeed ?? 0,
                    FragmentationChance = props.FragmentationChance ?? 0,
                    RicochetChance = props.RicochetChance ?? 0,
                    ProjectileCount = props.ProjectileCount ?? 0,
                    BulletMassGram = props.BulletMassGram ?? 0,
                    BallisticCoeficient = props.BallisticCoeficient ?? 0,
                    MisfireChance = props.MisfireChance ?? 0,
                    HeatFactor = props.HeatFactor ?? 0,
                    DurabilityBurnModificator = props.DurabilityBurnModificator ?? 0,
                    LightBleedingDelta = props.LightBleedingDelta ?? 0,
                    HeavyBleedingDelta = props.HeavyBleedingDelta ?? 0,
                    StaminaBurnPerDamage = props.StaminaBurnPerDamage ?? 0,
                    AmmoAccr = props.AmmoAccr ?? 0,
                    AmmoRec = props.AmmoRec ?? 0,
                    HandbookPrice = handbookHelper.GetTemplatePrice(tpl),
                    Original = new AmmoOriginalValues
                    {
                        Damage = snap.Damage,
                        PenetrationPower = snap.PenetrationPower,
                        ArmorDamage = snap.ArmorDamage,
                        InitialSpeed = snap.InitialSpeed,
                        FragmentationChance = snap.FragmentationChance,
                        RicochetChance = snap.RicochetChance,
                        ProjectileCount = snap.ProjectileCount,
                        BulletMassGram = snap.BulletMassGram,
                        BallisticCoeficient = snap.BallisticCoeficient,
                        MisfireChance = snap.MisfireChance,
                        HeatFactor = snap.HeatFactor,
                        DurabilityBurnModificator = snap.DurabilityBurnModificator,
                        LightBleedingDelta = snap.LightBleedingDelta,
                        HeavyBleedingDelta = snap.HeavyBleedingDelta,
                        StaminaBurnPerDamage = snap.StaminaBurnPerDamage,
                        AmmoAccr = snap.AmmoAccr,
                        AmmoRec = snap.AmmoRec,
                    },
                    IsModified = isModified,
                });
            }

            result.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

            var calibers = caliberCounts
                .Select(kv => new CaliberInfo
                {
                    Key = kv.Key,
                    Display = CaliberDisplayNames.GetValueOrDefault(kv.Key, kv.Key),
                    Count = kv.Value,
                })
                .OrderBy(c => c.Display)
                .ToList();

            return new AmmoListResponse
            {
                Ammo = result,
                Calibers = calibers,
                TotalModified = config.AmmoOverrides.Count,
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // UPDATE AMMO
    // ═══════════════════════════════════════════════════════

    public GameValuesApplyResult UpdateAmmo(Dictionary<string, AmmoOverride> overrides)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            foreach (var (tpl, ov) in overrides)
            {
                if (!_ammoSnapshots.ContainsKey(tpl)) continue;

                // Clamp values
                if (ov.Damage.HasValue) ov.Damage = GameValuesClamps.ClampAmmo("damage", ov.Damage.Value);
                if (ov.PenetrationPower.HasValue) ov.PenetrationPower = GameValuesClamps.ClampAmmo("penetrationPower", ov.PenetrationPower.Value);
                if (ov.ArmorDamage.HasValue) ov.ArmorDamage = GameValuesClamps.ClampAmmo("armorDamage", ov.ArmorDamage.Value);
                if (ov.InitialSpeed.HasValue) ov.InitialSpeed = GameValuesClamps.ClampAmmo("initialSpeed", ov.InitialSpeed.Value);
                if (ov.FragmentationChance.HasValue) ov.FragmentationChance = GameValuesClamps.ClampAmmo("fragmentationChance", ov.FragmentationChance.Value);
                if (ov.RicochetChance.HasValue) ov.RicochetChance = GameValuesClamps.ClampAmmo("ricochetChance", ov.RicochetChance.Value);
                if (ov.ProjectileCount.HasValue) ov.ProjectileCount = GameValuesClamps.ClampAmmo("projectileCount", ov.ProjectileCount.Value);
                if (ov.BulletMassGram.HasValue) ov.BulletMassGram = GameValuesClamps.ClampAmmo("bulletMassGram", ov.BulletMassGram.Value);
                if (ov.BallisticCoeficient.HasValue) ov.BallisticCoeficient = GameValuesClamps.ClampAmmo("ballisticCoeficient", ov.BallisticCoeficient.Value);
                if (ov.MisfireChance.HasValue) ov.MisfireChance = GameValuesClamps.ClampAmmo("misfireChance", ov.MisfireChance.Value);
                if (ov.HeatFactor.HasValue) ov.HeatFactor = GameValuesClamps.ClampAmmo("heatFactor", ov.HeatFactor.Value);
                if (ov.DurabilityBurnModificator.HasValue) ov.DurabilityBurnModificator = GameValuesClamps.ClampAmmo("durabilityBurnModificator", ov.DurabilityBurnModificator.Value);
                if (ov.LightBleedingDelta.HasValue) ov.LightBleedingDelta = GameValuesClamps.ClampAmmo("lightBleedingDelta", ov.LightBleedingDelta.Value);
                if (ov.HeavyBleedingDelta.HasValue) ov.HeavyBleedingDelta = GameValuesClamps.ClampAmmo("heavyBleedingDelta", ov.HeavyBleedingDelta.Value);
                if (ov.StaminaBurnPerDamage.HasValue) ov.StaminaBurnPerDamage = GameValuesClamps.ClampAmmo("staminaBurnPerDamage", ov.StaminaBurnPerDamage.Value);
                if (ov.AmmoAccr.HasValue) ov.AmmoAccr = GameValuesClamps.ClampAmmo("ammoAccr", ov.AmmoAccr.Value);
                if (ov.AmmoRec.HasValue) ov.AmmoRec = GameValuesClamps.ClampAmmo("ammoRec", ov.AmmoRec.Value);

                // Merge into config (only non-null fields)
                if (config.AmmoOverrides.TryGetValue(tpl, out var existing))
                {
                    config.AmmoOverrides[tpl] = MergeAmmoOverride(existing, ov);
                }
                else
                {
                    config.AmmoOverrides[tpl] = ov;
                }

                // Clean up empty overrides (all nulls)
                if (IsAmmoOverrideEmpty(config.AmmoOverrides[tpl]))
                    config.AmmoOverrides.Remove(tpl);
            }

            var count = ApplyAmmo();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = count,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Applied {count} ammo overrides",
            };
        }
    }

    public GameValuesApplyResult ResetAmmo()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var config = configService.GetConfig().GameValues;
            config.AmmoOverrides.Clear();
            var count = ApplyAmmo();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = 0,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = "All ammo values reset to defaults",
            };
        }
    }

    public GameValuesApplyResult ResetAmmoItem(string tpl)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var config = configService.GetConfig().GameValues;
            var removed = config.AmmoOverrides.Remove(tpl);
            ApplyAmmo();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = 0,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = removed ? "Ammo reset to default" : "Already at default",
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET ARMOR
    // ═══════════════════════════════════════════════════════

    public ArmorListResponse GetArmor(string? search, int? armorClass, string? material, string? kind)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var items = databaseService.GetItems();
            var locales = localeService.GetLocaleDb("en");
            var config = configService.GetConfig().GameValues;
            var result = new List<ArmorDto>();
            var classCounts = new Dictionary<int, int>();
            var materials = new HashSet<string>();
            var plateCount = 0;
            var armorCount2 = 0;

            foreach (var (tpl, snap) in _armorSnapshots)
            {
                if (!items.TryGetValue(tpl, out var template)) continue;
                var props = template.Properties;
                if (props == null) continue;

                var ac = props.ArmorClass ?? snap.ArmorClass;
                classCounts.TryGetValue(ac, out var cnt);
                classCounts[ac] = cnt + 1;
                if (!string.IsNullOrEmpty(snap.ArmorMaterial)) materials.Add(snap.ArmorMaterial);
                if (snap.IsPlate) plateCount++; else armorCount2++;

                // Filter by armor class
                if (armorClass.HasValue && ac != armorClass.Value) continue;

                // Filter by material
                if (!string.IsNullOrEmpty(material))
                {
                    var mat = (props.ArmorMaterial?.ToString() ?? snap.ArmorMaterial);
                    if (!mat.Equals(material, StringComparison.OrdinalIgnoreCase)) continue;
                }

                // Filter by kind (plate vs armor)
                if (!string.IsNullOrEmpty(kind))
                {
                    if (kind.Equals("plate", StringComparison.OrdinalIgnoreCase) && !snap.IsPlate) continue;
                    if (kind.Equals("armor", StringComparison.OrdinalIgnoreCase) && snap.IsPlate) continue;
                }

                // Name lookup
                locales.TryGetValue($"{tpl} ShortName", out var shortName);
                locales.TryGetValue($"{tpl} Name", out var fullName);
                if (string.IsNullOrEmpty(fullName)) fullName = template.Name ?? tpl;
                if (string.IsNullOrEmpty(shortName)) shortName = fullName;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var q = search.ToLowerInvariant();
                    if (!fullName.ToLowerInvariant().Contains(q) &&
                        !shortName.ToLowerInvariant().Contains(q) &&
                        !tpl.ToLowerInvariant().Contains(q))
                        continue;
                }

                var isModified = config.ArmorOverrides.ContainsKey(tpl);

                result.Add(new ArmorDto
                {
                    Tpl = tpl,
                    ShortName = shortName,
                    FullName = fullName,
                    ArmorClass = props.ArmorClass ?? snap.ArmorClass,
                    Durability = props.Durability ?? 0,
                    MaxDurability = props.MaxDurability ?? 0,
                    BluntThroughput = props.BluntThroughput ?? 0,
                    SpeedPenaltyPercent = props.SpeedPenaltyPercent ?? 0,
                    MousePenalty = props.MousePenalty ?? 0,
                    WeaponErgonomicPenalty = props.WeaponErgonomicPenalty ?? 0,
                    ArmorMaterial = props.ArmorMaterial?.ToString() ?? snap.ArmorMaterial,
                    ArmorType = props.ArmorType ?? snap.ArmorType,
                    IsPlate = snap.IsPlate,
                    HandbookPrice = handbookHelper.GetTemplatePrice(tpl),
                    Original = new ArmorOriginalValues
                    {
                        ArmorClass = snap.ArmorClass,
                        Durability = snap.Durability,
                        MaxDurability = snap.MaxDurability,
                        BluntThroughput = snap.BluntThroughput,
                        SpeedPenaltyPercent = snap.SpeedPenaltyPercent,
                        MousePenalty = snap.MousePenalty,
                        WeaponErgonomicPenalty = snap.WeaponErgonomicPenalty,
                    },
                    IsModified = isModified,
                });
            }

            result.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

            return new ArmorListResponse
            {
                Armor = result,
                ArmorClasses = classCounts
                    .Select(kv => new ArmorClassInfo { ArmorClass = kv.Key, Count = kv.Value })
                    .OrderBy(c => c.ArmorClass)
                    .ToList(),
                Materials = materials.OrderBy(m => m).ToList(),
                TotalModified = config.ArmorOverrides.Count,
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // UPDATE ARMOR
    // ═══════════════════════════════════════════════════════

    public GameValuesApplyResult UpdateArmor(Dictionary<string, ArmorOverride> overrides)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            foreach (var (tpl, ov) in overrides)
            {
                if (!_armorSnapshots.ContainsKey(tpl)) continue;

                // Clamp values
                if (ov.ArmorClass.HasValue) ov.ArmorClass = (int)GameValuesClamps.Clamp(ov.ArmorClass.Value, GameValuesClamps.ArmorClassMin, GameValuesClamps.ArmorClassMax);
                if (ov.Durability.HasValue) ov.Durability = GameValuesClamps.Clamp(ov.Durability.Value, GameValuesClamps.ArmorDurabilityMin, GameValuesClamps.ArmorDurabilityMax);
                if (ov.MaxDurability.HasValue) ov.MaxDurability = GameValuesClamps.Clamp(ov.MaxDurability.Value, GameValuesClamps.ArmorDurabilityMin, GameValuesClamps.ArmorDurabilityMax);
                if (ov.BluntThroughput.HasValue) ov.BluntThroughput = GameValuesClamps.Clamp(ov.BluntThroughput.Value, GameValuesClamps.BluntThroughputMin, GameValuesClamps.BluntThroughputMax);
                if (ov.SpeedPenaltyPercent.HasValue) ov.SpeedPenaltyPercent = GameValuesClamps.Clamp(ov.SpeedPenaltyPercent.Value, GameValuesClamps.SpeedPenaltyMin, GameValuesClamps.SpeedPenaltyMax);
                if (ov.MousePenalty.HasValue) ov.MousePenalty = GameValuesClamps.Clamp(ov.MousePenalty.Value, GameValuesClamps.MousePenaltyMin, GameValuesClamps.MousePenaltyMax);
                if (ov.WeaponErgonomicPenalty.HasValue) ov.WeaponErgonomicPenalty = GameValuesClamps.Clamp(ov.WeaponErgonomicPenalty.Value, GameValuesClamps.WeaponErgoPenaltyMin, GameValuesClamps.WeaponErgoPenaltyMax);

                if (config.ArmorOverrides.TryGetValue(tpl, out var existing))
                    config.ArmorOverrides[tpl] = MergeArmorOverride(existing, ov);
                else
                    config.ArmorOverrides[tpl] = ov;

                if (IsArmorOverrideEmpty(config.ArmorOverrides[tpl]))
                    config.ArmorOverrides.Remove(tpl);
            }

            var count = ApplyArmor();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = count,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Applied {count} armor overrides",
            };
        }
    }

    public GameValuesApplyResult ResetArmor()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            configService.GetConfig().GameValues.ArmorOverrides.Clear();
            ApplyArmor();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = "All armor values reset to defaults" };
        }
    }

    public GameValuesApplyResult ResetArmorItem(string tpl)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var removed = configService.GetConfig().GameValues.ArmorOverrides.Remove(tpl);
            ApplyArmor();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = removed ? "Armor reset to default" : "Already at default" };
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET WEAPONS
    // ═══════════════════════════════════════════════════════

    public WeaponListResponse GetWeapons(string? search, string? weapClass, string? caliber)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var items = databaseService.GetItems();
            var locales = localeService.GetLocaleDb("en");
            var config = configService.GetConfig().GameValues;
            var result = new List<WeaponDto>();
            var classCounts = new Dictionary<string, int>();
            var caliberCounts = new Dictionary<string, int>();

            foreach (var (tpl, snap) in _weaponSnapshots)
            {
                if (!items.TryGetValue(tpl, out var template)) continue;
                var props = template.Properties;
                if (props == null) continue;

                var wc = props.WeapClass ?? snap.WeapClass;
                classCounts.TryGetValue(wc, out var wcCnt);
                classCounts[wc] = wcCnt + 1;

                var cal = props.AmmoCaliber ?? snap.AmmoCaliber;
                if (!string.IsNullOrEmpty(cal))
                {
                    caliberCounts.TryGetValue(cal, out var calCnt);
                    caliberCounts[cal] = calCnt + 1;
                }

                // Filter by weapon class
                if (!string.IsNullOrEmpty(weapClass) && !wc.Equals(weapClass, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filter by caliber
                if (!string.IsNullOrEmpty(caliber) && !cal.Equals(caliber, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Name lookup
                locales.TryGetValue($"{tpl} ShortName", out var shortName);
                locales.TryGetValue($"{tpl} Name", out var fullName);
                if (string.IsNullOrEmpty(fullName)) fullName = template.Name ?? tpl;
                if (string.IsNullOrEmpty(shortName)) shortName = fullName;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var q = search.ToLowerInvariant();
                    if (!fullName.ToLowerInvariant().Contains(q) &&
                        !shortName.ToLowerInvariant().Contains(q) &&
                        !tpl.ToLowerInvariant().Contains(q))
                        continue;
                }

                var isModified = config.WeaponOverrides.ContainsKey(tpl);

                result.Add(new WeaponDto
                {
                    Tpl = tpl,
                    ShortName = shortName,
                    FullName = fullName,
                    WeapClass = wc,
                    WeapClassDisplay = WeapClassDisplayNames.GetValueOrDefault(wc, wc),
                    AmmoCaliber = cal,
                    AmmoCaliberDisplay = CaliberDisplayNames.GetValueOrDefault(cal, cal),
                    FireTypes = (props.WeapFireType?.ToList() ?? snap.FireTypes),
                    BoltAction = props.BoltAction ?? snap.BoltAction,
                    Ergonomics = props.Ergonomics ?? 0,
                    RecoilForceUp = props.RecoilForceUp ?? 0,
                    RecoilForceBack = props.RecoilForceBack ?? 0,
                    RecoilAngle = props.RecoilAngle ?? 0,
                    BFirerate = props.BFirerate ?? 0,
                    CenterOfImpact = props.CenterOfImpact ?? 0,
                    SightingRange = props.SightingRange ?? 0,
                    Durability = props.Durability ?? 0,
                    MaxDurability = props.MaxDurability ?? 0,
                    HeatFactorGun = props.HeatFactorGun ?? 0,
                    CoolFactorGun = props.CoolFactorGun ?? 0,
                    BaseMalfunctionChance = props.BaseMalfunctionChance ?? 0,
                    Velocity = props.Velocity ?? 0,
                    DeviationMax = props.DeviationMax ?? 0,
                    HandbookPrice = handbookHelper.GetTemplatePrice(tpl),
                    Original = new WeaponOriginalValues
                    {
                        Ergonomics = snap.Ergonomics,
                        RecoilForceUp = snap.RecoilForceUp,
                        RecoilForceBack = snap.RecoilForceBack,
                        RecoilAngle = snap.RecoilAngle,
                        BFirerate = snap.BFirerate,
                        CenterOfImpact = snap.CenterOfImpact,
                        SightingRange = snap.SightingRange,
                        Durability = snap.Durability,
                        MaxDurability = snap.MaxDurability,
                        HeatFactorGun = snap.HeatFactorGun,
                        CoolFactorGun = snap.CoolFactorGun,
                        BaseMalfunctionChance = snap.BaseMalfunctionChance,
                        Velocity = snap.Velocity,
                        DeviationMax = snap.DeviationMax,
                    },
                    IsModified = isModified,
                });
            }

            result.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

            return new WeaponListResponse
            {
                Weapons = result,
                WeaponClasses = classCounts
                    .Select(kv => new WeaponClassInfo
                    {
                        Key = kv.Key,
                        Display = WeapClassDisplayNames.GetValueOrDefault(kv.Key, kv.Key),
                        Count = kv.Value,
                    })
                    .OrderBy(c => c.Display)
                    .ToList(),
                Calibers = caliberCounts
                    .Select(kv => new CaliberInfo
                    {
                        Key = kv.Key,
                        Display = CaliberDisplayNames.GetValueOrDefault(kv.Key, kv.Key),
                        Count = kv.Value,
                    })
                    .OrderBy(c => c.Display)
                    .ToList(),
                TotalModified = config.WeaponOverrides.Count,
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // UPDATE WEAPONS
    // ═══════════════════════════════════════════════════════

    public GameValuesApplyResult UpdateWeapons(Dictionary<string, WeaponOverride> overrides)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            foreach (var (tpl, ov) in overrides)
            {
                if (!_weaponSnapshots.ContainsKey(tpl)) continue;

                // Clamp values
                if (ov.Ergonomics.HasValue) ov.Ergonomics = GameValuesClamps.Clamp(ov.Ergonomics.Value, GameValuesClamps.ErgonomicsMin, GameValuesClamps.ErgonomicsMax);
                if (ov.RecoilForceUp.HasValue) ov.RecoilForceUp = GameValuesClamps.Clamp(ov.RecoilForceUp.Value, GameValuesClamps.RecoilMin, GameValuesClamps.RecoilMax);
                if (ov.RecoilForceBack.HasValue) ov.RecoilForceBack = GameValuesClamps.Clamp(ov.RecoilForceBack.Value, GameValuesClamps.RecoilMin, GameValuesClamps.RecoilMax);
                if (ov.RecoilAngle.HasValue) ov.RecoilAngle = GameValuesClamps.Clamp(ov.RecoilAngle.Value, GameValuesClamps.RecoilAngleMin, GameValuesClamps.RecoilAngleMax);
                if (ov.BFirerate.HasValue) ov.BFirerate = GameValuesClamps.Clamp(ov.BFirerate.Value, GameValuesClamps.FirerateMin, GameValuesClamps.FirerateMax);
                if (ov.CenterOfImpact.HasValue) ov.CenterOfImpact = GameValuesClamps.Clamp(ov.CenterOfImpact.Value, GameValuesClamps.CenterOfImpactMin, GameValuesClamps.CenterOfImpactMax);
                if (ov.SightingRange.HasValue) ov.SightingRange = GameValuesClamps.Clamp(ov.SightingRange.Value, GameValuesClamps.SightingRangeMin, GameValuesClamps.SightingRangeMax);
                if (ov.Durability.HasValue) ov.Durability = GameValuesClamps.Clamp(ov.Durability.Value, GameValuesClamps.WeaponDurabilityMin, GameValuesClamps.WeaponDurabilityMax);
                if (ov.MaxDurability.HasValue) ov.MaxDurability = GameValuesClamps.Clamp(ov.MaxDurability.Value, GameValuesClamps.WeaponDurabilityMin, GameValuesClamps.WeaponDurabilityMax);
                if (ov.HeatFactorGun.HasValue) ov.HeatFactorGun = GameValuesClamps.Clamp(ov.HeatFactorGun.Value, GameValuesClamps.HeatFactorGunMin, GameValuesClamps.HeatFactorGunMax);
                if (ov.CoolFactorGun.HasValue) ov.CoolFactorGun = GameValuesClamps.Clamp(ov.CoolFactorGun.Value, GameValuesClamps.CoolFactorGunMin, GameValuesClamps.CoolFactorGunMax);
                if (ov.BaseMalfunctionChance.HasValue) ov.BaseMalfunctionChance = GameValuesClamps.Clamp(ov.BaseMalfunctionChance.Value, GameValuesClamps.MalfunctionChanceMin, GameValuesClamps.MalfunctionChanceMax);
                if (ov.Velocity.HasValue) ov.Velocity = GameValuesClamps.Clamp(ov.Velocity.Value, GameValuesClamps.VelocityMin, GameValuesClamps.VelocityMax);
                if (ov.DeviationMax.HasValue) ov.DeviationMax = GameValuesClamps.Clamp(ov.DeviationMax.Value, GameValuesClamps.DeviationMaxMin, GameValuesClamps.DeviationMaxMax);

                if (config.WeaponOverrides.TryGetValue(tpl, out var existing))
                    config.WeaponOverrides[tpl] = MergeWeaponOverride(existing, ov);
                else
                    config.WeaponOverrides[tpl] = ov;

                if (IsWeaponOverrideEmpty(config.WeaponOverrides[tpl]))
                    config.WeaponOverrides.Remove(tpl);
            }

            var count = ApplyWeapons();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = count,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Applied {count} weapon overrides",
            };
        }
    }

    public GameValuesApplyResult ResetWeapons()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            configService.GetConfig().GameValues.WeaponOverrides.Clear();
            ApplyWeapons();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = "All weapon values reset to defaults" };
        }
    }

    public GameValuesApplyResult ResetWeaponItem(string tpl)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var removed = configService.GetConfig().GameValues.WeaponOverrides.Remove(tpl);
            ApplyWeapons();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = removed ? "Weapon reset to default" : "Already at default" };
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET MEDICAL
    // ═══════════════════════════════════════════════════════

    public MedicalListResponse GetMedical(string? search, string? type)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var items = databaseService.GetItems();
            var locales = localeService.GetLocaleDb("en");
            var config = configService.GetConfig().GameValues;
            var result = new List<MedicalDto>();
            var typeCounts = new Dictionary<string, int>();

            foreach (var (tpl, snap) in _medicalSnapshots)
            {
                if (!items.TryGetValue(tpl, out var template)) continue;
                var props = template.Properties;
                if (props == null) continue;

                typeCounts.TryGetValue(snap.MedType, out var cnt);
                typeCounts[snap.MedType] = cnt + 1;

                // Type filter
                if (!string.IsNullOrEmpty(type) && !snap.MedType.Equals(type, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Name lookup
                locales.TryGetValue($"{tpl} ShortName", out var shortName);
                locales.TryGetValue($"{tpl} Name", out var fullName);
                if (string.IsNullOrEmpty(fullName)) fullName = template.Name ?? tpl;
                if (string.IsNullOrEmpty(shortName)) shortName = fullName;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var q = search.ToLowerInvariant();
                    if (!fullName.ToLowerInvariant().Contains(q) &&
                        !shortName.ToLowerInvariant().Contains(q) &&
                        !tpl.ToLowerInvariant().Contains(q))
                        continue;
                }

                var isModified = config.MedicalOverrides.ContainsKey(tpl);

                // Get current values from live props
                double? curLightBleedCost = null, curHeavyBleedCost = null, curFractureCost = null;
                double? curPainDur = null, curContusionDur = null;
                double? curEnergyChange = null, curHydrationChange = null;
                var curTreats = new List<string>();
                if (props.EffectsDamage != null)
                {
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.LightBleeding, out var lb))
                    { curLightBleedCost = lb.Cost; curTreats.Add("LBleed"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.HeavyBleeding, out var hb))
                    { curHeavyBleedCost = hb.Cost; curTreats.Add("HBleed"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.Fracture, out var fr))
                    { curFractureCost = fr.Cost; curTreats.Add("Fracture"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.Pain, out var pn))
                    { curPainDur = pn.Duration; curTreats.Add("Pain"); }
                    if (props.EffectsDamage.TryGetValue(DamageEffectType.Contusion, out var cn))
                    { curContusionDur = cn.Duration; curTreats.Add("Contusion"); }
                }
                if (props.EffectsHealth != null)
                {
                    if (props.EffectsHealth.TryGetValue(HealthFactor.Energy, out var en))
                        curEnergyChange = en.Value;
                    if (props.EffectsHealth.TryGetValue(HealthFactor.Hydration, out var hy))
                        curHydrationChange = hy.Value;
                }

                result.Add(new MedicalDto
                {
                    Tpl = tpl,
                    ShortName = shortName,
                    FullName = fullName,
                    MedType = snap.MedType,
                    StimBuffName = snap.StimBuffName,
                    MaxHpResource = props.MaxHpResource ?? 0,
                    HpResourceRate = props.HpResourceRate ?? 0,
                    MedUseTime = props.MedUseTime ?? 0,
                    LightBleedingCost = curLightBleedCost,
                    HeavyBleedingCost = curHeavyBleedCost,
                    FractureCost = curFractureCost,
                    PainDuration = curPainDur,
                    ContusionDuration = curContusionDur,
                    EnergyChange = curEnergyChange,
                    HydrationChange = curHydrationChange,
                    Treats = curTreats,
                    HandbookPrice = handbookHelper.GetTemplatePrice(tpl),
                    Original = new MedicalOriginalValues
                    {
                        MaxHpResource = snap.MaxHpResource,
                        HpResourceRate = snap.HpResourceRate,
                        MedUseTime = snap.MedUseTime,
                        LightBleedingCost = snap.LightBleedingCost,
                        HeavyBleedingCost = snap.HeavyBleedingCost,
                        FractureCost = snap.FractureCost,
                        PainDuration = snap.PainDuration,
                        ContusionDuration = snap.ContusionDuration,
                        EnergyChange = snap.EnergyChange,
                        HydrationChange = snap.HydrationChange,
                    },
                    IsModified = isModified,
                });
            }

            // Enrich with stim buff data
            var buffsDict = databaseService.GetGlobals().Configuration?.Health?.Effects?.Stimulator?.Buffs;
            // Build shared-count map (how many items reference each buff name)
            var buffUsageCounts = new Dictionary<string, int>();
            foreach (var snap in _medicalSnapshots.Values)
            {
                if (!string.IsNullOrEmpty(snap.StimBuffName))
                {
                    buffUsageCounts.TryGetValue(snap.StimBuffName, out var c);
                    buffUsageCounts[snap.StimBuffName] = c + 1;
                }
            }

            foreach (var dto in result)
            {
                if (string.IsNullOrEmpty(dto.StimBuffName)) continue;
                var bn = dto.StimBuffName;

                // Current live effects
                if (buffsDict != null && buffsDict.TryGetValue(bn, out var liveBuffs) && liveBuffs != null)
                {
                    dto.Effects = liveBuffs.Select(b => new StimBuffEffectDto
                    {
                        BuffType = b.BuffType ?? "",
                        Value = b.Value,
                        Duration = b.Duration,
                        Delay = b.Delay,
                        Chance = b.Chance,
                        AbsoluteValue = b.AbsoluteValue,
                        SkillName = b.SkillName ?? "",
                    }).ToList();
                }

                // Original snapshot effects
                if (_stimBuffSnapshots.TryGetValue(bn, out var origEffects))
                    dto.OriginalEffects = origEffects.Select(e => e with { }).ToList();

                dto.IsBuffModified = config.StimBuffOverrides.ContainsKey(bn);
                buffUsageCounts.TryGetValue(bn, out var shared);
                dto.StimBuffSharedCount = shared;
            }

            result.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

            return new MedicalListResponse
            {
                Medical = result,
                Types = typeCounts
                    .Select(kv => new MedicalTypeInfo { Key = kv.Key, Display = kv.Key, Count = kv.Value })
                    .OrderBy(t => t.Display)
                    .ToList(),
                TotalModified = config.MedicalOverrides.Count + config.StimBuffOverrides.Count,
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // UPDATE MEDICAL
    // ═══════════════════════════════════════════════════════

    public GameValuesApplyResult UpdateMedical(Dictionary<string, MedicalOverride> overrides)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            foreach (var (tpl, ov) in overrides)
            {
                if (!_medicalSnapshots.ContainsKey(tpl)) continue;

                // Clamp values
                if (ov.MaxHpResource.HasValue) ov.MaxHpResource = GameValuesClamps.ClampMedical("maxHpResource", ov.MaxHpResource.Value);
                if (ov.HpResourceRate.HasValue) ov.HpResourceRate = GameValuesClamps.ClampMedical("hpResourceRate", ov.HpResourceRate.Value);
                if (ov.MedUseTime.HasValue) ov.MedUseTime = GameValuesClamps.ClampMedical("medUseTime", ov.MedUseTime.Value);
                if (ov.LightBleedingCost.HasValue) ov.LightBleedingCost = GameValuesClamps.ClampMedical("lightBleedingCost", ov.LightBleedingCost.Value);
                if (ov.HeavyBleedingCost.HasValue) ov.HeavyBleedingCost = GameValuesClamps.ClampMedical("heavyBleedingCost", ov.HeavyBleedingCost.Value);
                if (ov.FractureCost.HasValue) ov.FractureCost = GameValuesClamps.ClampMedical("fractureCost", ov.FractureCost.Value);
                if (ov.PainDuration.HasValue) ov.PainDuration = GameValuesClamps.ClampMedical("painDuration", ov.PainDuration.Value);
                if (ov.ContusionDuration.HasValue) ov.ContusionDuration = GameValuesClamps.ClampMedical("contusionDuration", ov.ContusionDuration.Value);
                if (ov.EnergyChange.HasValue) ov.EnergyChange = GameValuesClamps.ClampMedical("energyChange", ov.EnergyChange.Value);
                if (ov.HydrationChange.HasValue) ov.HydrationChange = GameValuesClamps.ClampMedical("hydrationChange", ov.HydrationChange.Value);

                if (config.MedicalOverrides.TryGetValue(tpl, out var existing))
                    config.MedicalOverrides[tpl] = MergeMedicalOverride(existing, ov);
                else
                    config.MedicalOverrides[tpl] = ov;

                if (IsMedicalOverrideEmpty(config.MedicalOverrides[tpl]))
                    config.MedicalOverrides.Remove(tpl);
            }

            var count = ApplyMedical();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = count,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Applied {count} medical overrides",
            };
        }
    }

    public GameValuesApplyResult ResetMedical()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            configService.GetConfig().GameValues.MedicalOverrides.Clear();
            ApplyMedical();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = "All medical values reset to defaults" };
        }
    }

    public GameValuesApplyResult ResetMedicalItem(string tpl)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var removed = configService.GetConfig().GameValues.MedicalOverrides.Remove(tpl);
            ApplyMedical();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = removed ? "Medical item reset to default" : "Already at default" };
        }
    }

    // ═══════════════════════════════════════════════════════
    // STIM BUFF OVERRIDES
    // ═══════════════════════════════════════════════════════

    public GameValuesApplyResult UpdateStimBuffs(Dictionary<string, StimBuffOverride> overrides)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            foreach (var (buffName, ov) in overrides)
            {
                if (!_stimBuffSnapshots.ContainsKey(buffName)) continue;

                // Clamp all effect values
                var clamped = new StimBuffOverride
                {
                    Effects = ov.Effects.Select(GameValuesClamps.ClampBuffEffect).ToList()
                };

                config.StimBuffOverrides[buffName] = clamped;
            }

            var count = ApplyStimBuffs();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = count,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Applied {count} stim buff overrides",
            };
        }
    }

    public GameValuesApplyResult ResetStimBuff(string buffName)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var removed = configService.GetConfig().GameValues.StimBuffOverrides.Remove(buffName);
            ApplyStimBuffs();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = removed ? "Stim buff reset to default" : "Already at default" };
        }
    }

    public GameValuesApplyResult ResetAllStimBuffs()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            configService.GetConfig().GameValues.StimBuffOverrides.Clear();
            ApplyStimBuffs();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = "All stim buff overrides reset to defaults" };
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET BACKPACKS
    // ═══════════════════════════════════════════════════════

    public BackpackListResponse GetBackpacks(string? search)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var items = databaseService.GetItems();
            var locales = localeService.GetLocaleDb("en");
            var config = configService.GetConfig().GameValues;
            var result = new List<BackpackDto>();

            foreach (var (tpl, snap) in _backpackSnapshots)
            {
                if (!items.TryGetValue(tpl, out var template)) continue;
                var props = template.Properties;
                if (props == null) continue;

                // Name lookup
                locales.TryGetValue($"{tpl} ShortName", out var shortName);
                locales.TryGetValue($"{tpl} Name", out var fullName);
                if (string.IsNullOrEmpty(fullName)) fullName = template.Name ?? tpl;
                if (string.IsNullOrEmpty(shortName)) shortName = fullName;

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var q = search.ToLowerInvariant();
                    if (!fullName.ToLowerInvariant().Contains(q) &&
                        !shortName.ToLowerInvariant().Contains(q) &&
                        !tpl.ToLowerInvariant().Contains(q))
                        continue;
                }

                var isModified = config.BackpackOverrides.ContainsKey(tpl);

                // Current grid values
                var grids = props.Grids?.ToList() ?? [];
                var curTotalSlots = 0;
                var curLayoutParts = new List<string>();
                var curW = 0;
                var curH = 0;
                for (var gi = 0; gi < grids.Count; gi++)
                {
                    var gp = grids[gi].Properties;
                    var cw = gp?.CellsH ?? 0;
                    var ch = gp?.CellsV ?? 0;
                    curTotalSlots += cw * ch;
                    curLayoutParts.Add($"{cw}\u00d7{ch}");
                    if (gi == 0) { curW = cw; curH = ch; }
                }

                result.Add(new BackpackDto
                {
                    Tpl = tpl,
                    ShortName = shortName,
                    FullName = fullName,
                    Weight = props.Weight ?? 0,
                    SpeedPenaltyPercent = props.SpeedPenaltyPercent ?? 0,
                    WeaponErgonomicPenalty = props.WeaponErgonomicPenalty ?? 0,
                    MousePenalty = props.MousePenalty ?? 0,
                    GridWidth = curW,
                    GridHeight = curH,
                    TotalSlots = curTotalSlots,
                    IsMultiGrid = snap.IsMultiGrid,
                    GridLayout = string.Join("+", curLayoutParts),
                    HandbookPrice = handbookHelper.GetTemplatePrice(tpl),
                    Original = new BackpackOriginalValues
                    {
                        Weight = snap.Weight,
                        SpeedPenaltyPercent = snap.SpeedPenaltyPercent,
                        WeaponErgonomicPenalty = snap.WeaponErgonomicPenalty,
                        MousePenalty = snap.MousePenalty,
                        GridWidth = snap.GridWidth,
                        GridHeight = snap.GridHeight,
                        TotalSlots = snap.TotalSlots,
                    },
                    IsModified = isModified,
                });
            }

            result.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

            return new BackpackListResponse
            {
                Backpacks = result,
                TotalCount = _backpackSnapshots.Count,
                TotalModified = config.BackpackOverrides.Count,
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // UPDATE BACKPACKS
    // ═══════════════════════════════════════════════════════

    public GameValuesApplyResult UpdateBackpacks(Dictionary<string, BackpackOverride> overrides)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();
            var config = configService.GetConfig().GameValues;

            foreach (var (tpl, ov) in overrides)
            {
                if (!_backpackSnapshots.ContainsKey(tpl)) continue;

                // Clamp values
                if (ov.Weight.HasValue) ov.Weight = GameValuesClamps.ClampBackpack("weight", ov.Weight.Value);
                if (ov.SpeedPenaltyPercent.HasValue) ov.SpeedPenaltyPercent = GameValuesClamps.ClampBackpack("speedPenaltyPercent", ov.SpeedPenaltyPercent.Value);
                if (ov.WeaponErgonomicPenalty.HasValue) ov.WeaponErgonomicPenalty = GameValuesClamps.ClampBackpack("weaponErgonomicPenalty", ov.WeaponErgonomicPenalty.Value);
                if (ov.MousePenalty.HasValue) ov.MousePenalty = GameValuesClamps.ClampBackpack("mousePenalty", ov.MousePenalty.Value);
                if (ov.GridWidth.HasValue) ov.GridWidth = Math.Clamp(ov.GridWidth.Value, GameValuesClamps.BpGridMin, GameValuesClamps.BpGridMax);
                if (ov.GridHeight.HasValue) ov.GridHeight = Math.Clamp(ov.GridHeight.Value, GameValuesClamps.BpGridMin, GameValuesClamps.BpGridMax);

                // Multi-grid backpacks: strip grid overrides
                if (_backpackSnapshots[tpl].IsMultiGrid)
                {
                    ov.GridWidth = null;
                    ov.GridHeight = null;
                }

                if (config.BackpackOverrides.TryGetValue(tpl, out var existing))
                    config.BackpackOverrides[tpl] = MergeBackpackOverride(existing, ov);
                else
                    config.BackpackOverrides[tpl] = ov;

                if (IsBackpackOverrideEmpty(config.BackpackOverrides[tpl]))
                    config.BackpackOverrides.Remove(tpl);
            }

            var count = ApplyBackpacks();
            configService.SaveConfig();
            sw.Stop();

            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = count,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Applied {count} backpack overrides",
            };
        }
    }

    public GameValuesApplyResult ResetBackpacks()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            configService.GetConfig().GameValues.BackpackOverrides.Clear();
            ApplyBackpacks();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = "All backpack values reset to defaults" };
        }
    }

    public GameValuesApplyResult ResetBackpackItem(string tpl)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var removed = configService.GetConfig().GameValues.BackpackOverrides.Remove(tpl);
            ApplyBackpacks();
            configService.SaveConfig();
            sw.Stop();
            return new GameValuesApplyResult { Success = true, ApplyTimeMs = sw.ElapsedMilliseconds, Message = removed ? "Backpack reset to default" : "Already at default" };
        }
    }

    // ═══════════════════════════════════════════════════════
    // PRESETS
    // ═══════════════════════════════════════════════════════

    private readonly Dictionary<string, GameValuesPresetEntry> _dynamicPresets = new();

    private static readonly Dictionary<string, GameValuesPresetEntry> BuiltInPresets = new()
    {
        ["Defaults — All"] = new GameValuesPresetEntry
        {
            Description = "Reset all game values to vanilla defaults",
            Category = "all",
            AmmoOverrides = new(),
            ArmorOverrides = new(),
            WeaponOverrides = new(),
            MedicalOverrides = new(),
            BackpackOverrides = new(),
        },
        ["Defaults — Ammo"] = new GameValuesPresetEntry
        {
            Description = "Reset ammo values to vanilla defaults",
            Category = "ammo",
            AmmoOverrides = new(),
        },
        ["Defaults — Armor"] = new GameValuesPresetEntry
        {
            Description = "Reset armor values to vanilla defaults",
            Category = "armor",
            ArmorOverrides = new(),
        },
        ["Defaults — Weapons"] = new GameValuesPresetEntry
        {
            Description = "Reset weapon values to vanilla defaults",
            Category = "weapons",
            WeaponOverrides = new(),
        },
        ["Defaults — Medical"] = new GameValuesPresetEntry
        {
            Description = "Reset medical/food/drink values to vanilla defaults",
            Category = "medical",
            MedicalOverrides = new(),
        },
        ["Defaults — Backpacks"] = new GameValuesPresetEntry
        {
            Description = "Reset backpack values to vanilla defaults",
            Category = "backpacks",
            BackpackOverrides = new(),
        },
    };

    private bool IsBuiltIn(string name) => BuiltInPresets.ContainsKey(name) || _dynamicPresets.ContainsKey(name);

    private GameValuesPresetEntry? FindBuiltIn(string name) =>
        BuiltInPresets.GetValueOrDefault(name) ?? _dynamicPresets.GetValueOrDefault(name);

    public List<GameValuesPresetInfo> GetPresets()
    {
        var config = configService.GetConfig().GameValues;
        var result = new List<GameValuesPresetInfo>();

        foreach (var (name, preset) in BuiltInPresets)
        {
            result.Add(new GameValuesPresetInfo
            {
                Name = name,
                Description = preset.Description,
                Category = preset.Category,
                IsBuiltIn = true,
            });
        }

        foreach (var (name, preset) in _dynamicPresets)
        {
            result.Add(new GameValuesPresetInfo
            {
                Name = name,
                Description = preset.Description,
                Category = preset.Category,
                IsBuiltIn = true,
            });
        }

        foreach (var (name, preset) in config.Presets)
        {
            result.Add(new GameValuesPresetInfo
            {
                Name = name,
                Description = preset.Description,
                Category = preset.Category,
                IsBuiltIn = false,
            });
        }

        return result;
    }

    public GameValuesApplyResult SavePreset(string name, string description, string category)
    {
        lock (_lock)
        {
            if (IsBuiltIn(name))
                return new GameValuesApplyResult { Success = false, Message = "Cannot overwrite built-in preset" };

            var config = configService.GetConfig().GameValues;
            var entry = new GameValuesPresetEntry
            {
                Description = description,
                Category = category,
            };

            if (category is "ammo" or "all")
                entry.AmmoOverrides = new Dictionary<string, AmmoOverride>(config.AmmoOverrides);
            if (category is "armor" or "all")
                entry.ArmorOverrides = new Dictionary<string, ArmorOverride>(config.ArmorOverrides);
            if (category is "weapons" or "all")
                entry.WeaponOverrides = new Dictionary<string, WeaponOverride>(config.WeaponOverrides);
            if (category is "medical" or "all")
                entry.MedicalOverrides = new Dictionary<string, MedicalOverride>(config.MedicalOverrides);
            if (category is "backpacks" or "all")
                entry.BackpackOverrides = new Dictionary<string, BackpackOverride>(config.BackpackOverrides);
            if (category is "medical" or "all")
                entry.StimBuffOverrides = new Dictionary<string, StimBuffOverride>(config.StimBuffOverrides);

            config.Presets[name] = entry;
            configService.SaveConfig();

            return new GameValuesApplyResult { Success = true, Message = $"Preset '{name}' saved" };
        }
    }

    public GameValuesApplyResult LoadPreset(string name)
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            var config = configService.GetConfig().GameValues;

            // Check built-in presets (static + dynamic) first, then custom
            var preset = FindBuiltIn(name);
            if (preset == null && !config.Presets.TryGetValue(name, out preset))
            {
                return new GameValuesApplyResult { Success = false, Message = "Preset not found" };
            }

            // Per-category loading: only replace the categories this preset covers
            // A null dict means "don't touch this category"
            // An empty dict means "clear all overrides for this category"
            var cat = preset.Category ?? "all";

            if (cat is "ammo" or "all" && preset.AmmoOverrides != null)
                config.AmmoOverrides = new Dictionary<string, AmmoOverride>(preset.AmmoOverrides);
            if (cat is "armor" or "all" && preset.ArmorOverrides != null)
                config.ArmorOverrides = new Dictionary<string, ArmorOverride>(preset.ArmorOverrides);
            if (cat is "weapons" or "all" && preset.WeaponOverrides != null)
                config.WeaponOverrides = new Dictionary<string, WeaponOverride>(preset.WeaponOverrides);
            if (cat is "medical" or "all" && preset.MedicalOverrides != null)
                config.MedicalOverrides = new Dictionary<string, MedicalOverride>(preset.MedicalOverrides);
            if (cat is "backpacks" or "all" && preset.BackpackOverrides != null)
                config.BackpackOverrides = new Dictionary<string, BackpackOverride>(preset.BackpackOverrides);
            if (cat is "medical" or "all" && preset.StimBuffOverrides != null)
                config.StimBuffOverrides = new Dictionary<string, StimBuffOverride>(preset.StimBuffOverrides);

            ApplyAll();
            configService.GetConfig().ActiveGameValuesPreset = name;
            configService.SaveConfig();
            sw.Stop();

            var total = config.AmmoOverrides.Count + config.ArmorOverrides.Count + config.WeaponOverrides.Count + config.MedicalOverrides.Count + config.BackpackOverrides.Count + config.StimBuffOverrides.Count;
            return new GameValuesApplyResult
            {
                Success = true,
                ItemsModified = total,
                ApplyTimeMs = sw.ElapsedMilliseconds,
                Message = $"Loaded preset '{name}' — {total} total overrides active",
            };
        }
    }

    public GameValuesApplyResult DeletePreset(string name)
    {
        lock (_lock)
        {
            if (IsBuiltIn(name))
                return new GameValuesApplyResult { Success = false, Message = "Cannot delete built-in preset" };

            var removed = configService.GetConfig().GameValues.Presets.Remove(name);
            if (configService.GetConfig().ActiveGameValuesPreset == name)
                configService.GetConfig().ActiveGameValuesPreset = null;
            if (removed) configService.SaveConfig();
            return new GameValuesApplyResult { Success = removed, Message = removed ? $"Preset '{name}' deleted" : "Preset not found" };
        }
    }

    public string? GetActivePreset() => configService.GetConfig().ActiveGameValuesPreset;

    public void ClearActivePreset()
    {
        configService.GetConfig().ActiveGameValuesPreset = null;
        configService.SaveConfig();
    }

    public GameValuesPresetEntry? ExportPreset(string name)
    {
        var builtIn = FindBuiltIn(name);
        if (builtIn != null) return builtIn;
        var config = configService.GetConfig().GameValues;
        return config.Presets.GetValueOrDefault(name);
    }

    public GameValuesApplyResult ImportPreset(string name, GameValuesPresetEntry preset)
    {
        lock (_lock)
        {
            if (IsBuiltIn(name))
                return new GameValuesApplyResult { Success = false, Message = "Cannot overwrite built-in preset" };

            configService.GetConfig().GameValues.Presets[name] = preset;
            configService.SaveConfig();
            return new GameValuesApplyResult { Success = true, Message = $"Preset '{name}' imported" };
        }
    }

    // ═══════════════════════════════════════════════════════
    // APPLY (RESTORE ALL → APPLY OVERRIDES)
    // ═══════════════════════════════════════════════════════

    private void ApplyAll()
    {
        ApplyAmmo();
        ApplyArmor();
        ApplyWeapons();
        ApplyMedical();
        ApplyBackpacks();
        ApplyStimBuffs();
    }

    private int ApplyAmmo()
    {
        EnsureSnapshot();
        var items = databaseService.GetItems();
        var config = configService.GetConfig().GameValues;

        // Step 1: Restore ALL ammo from snapshots
        foreach (var (tpl, snap) in _ammoSnapshots)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            props.Damage = snap.Damage;
            props.PenetrationPower = (int)snap.PenetrationPower;
            props.ArmorDamage = snap.ArmorDamage;
            props.InitialSpeed = snap.InitialSpeed;
            props.FragmentationChance = snap.FragmentationChance;
            props.RicochetChance = snap.RicochetChance;
            props.ProjectileCount = snap.ProjectileCount;
            props.BulletMassGram = snap.BulletMassGram;
            props.BallisticCoeficient = snap.BallisticCoeficient;
            props.MisfireChance = snap.MisfireChance;
            props.HeatFactor = snap.HeatFactor;
            props.DurabilityBurnModificator = snap.DurabilityBurnModificator;
            props.LightBleedingDelta = snap.LightBleedingDelta;
            props.HeavyBleedingDelta = snap.HeavyBleedingDelta;
            props.StaminaBurnPerDamage = snap.StaminaBurnPerDamage;
            props.AmmoAccr = snap.AmmoAccr;
            props.AmmoRec = snap.AmmoRec;
        }

        // Step 2: Apply overrides (non-null fields only)
        var count = 0;
        foreach (var (tpl, ov) in config.AmmoOverrides)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            if (ov.Damage.HasValue) props.Damage = ov.Damage.Value;
            if (ov.PenetrationPower.HasValue) props.PenetrationPower = (int)ov.PenetrationPower.Value;
            if (ov.ArmorDamage.HasValue) props.ArmorDamage = ov.ArmorDamage.Value;
            if (ov.InitialSpeed.HasValue) props.InitialSpeed = ov.InitialSpeed.Value;
            if (ov.FragmentationChance.HasValue) props.FragmentationChance = ov.FragmentationChance.Value;
            if (ov.RicochetChance.HasValue) props.RicochetChance = ov.RicochetChance.Value;
            if (ov.ProjectileCount.HasValue) props.ProjectileCount = ov.ProjectileCount.Value;
            if (ov.BulletMassGram.HasValue) props.BulletMassGram = ov.BulletMassGram.Value;
            if (ov.BallisticCoeficient.HasValue) props.BallisticCoeficient = ov.BallisticCoeficient.Value;
            if (ov.MisfireChance.HasValue) props.MisfireChance = ov.MisfireChance.Value;
            if (ov.HeatFactor.HasValue) props.HeatFactor = ov.HeatFactor.Value;
            if (ov.DurabilityBurnModificator.HasValue) props.DurabilityBurnModificator = ov.DurabilityBurnModificator.Value;
            if (ov.LightBleedingDelta.HasValue) props.LightBleedingDelta = ov.LightBleedingDelta.Value;
            if (ov.HeavyBleedingDelta.HasValue) props.HeavyBleedingDelta = ov.HeavyBleedingDelta.Value;
            if (ov.StaminaBurnPerDamage.HasValue) props.StaminaBurnPerDamage = ov.StaminaBurnPerDamage.Value;
            if (ov.AmmoAccr.HasValue) props.AmmoAccr = ov.AmmoAccr.Value;
            if (ov.AmmoRec.HasValue) props.AmmoRec = ov.AmmoRec.Value;
            count++;
        }

        return count;
    }

    private int ApplyArmor()
    {
        EnsureSnapshot();
        var items = databaseService.GetItems();
        var config = configService.GetConfig().GameValues;

        // Step 1: Restore ALL armor from snapshots
        foreach (var (tpl, snap) in _armorSnapshots)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            props.ArmorClass = snap.ArmorClass;
            props.Durability = snap.Durability;
            props.MaxDurability = snap.MaxDurability;
            props.BluntThroughput = snap.BluntThroughput;
            props.SpeedPenaltyPercent = snap.SpeedPenaltyPercent;
            props.MousePenalty = snap.MousePenalty;
            props.WeaponErgonomicPenalty = snap.WeaponErgonomicPenalty;
        }

        // Step 2: Apply overrides
        var count = 0;
        foreach (var (tpl, ov) in config.ArmorOverrides)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            if (ov.ArmorClass.HasValue) props.ArmorClass = ov.ArmorClass.Value;
            if (ov.Durability.HasValue) props.Durability = ov.Durability.Value;
            if (ov.MaxDurability.HasValue) props.MaxDurability = ov.MaxDurability.Value;
            if (ov.BluntThroughput.HasValue) props.BluntThroughput = ov.BluntThroughput.Value;
            if (ov.SpeedPenaltyPercent.HasValue) props.SpeedPenaltyPercent = ov.SpeedPenaltyPercent.Value;
            if (ov.MousePenalty.HasValue) props.MousePenalty = ov.MousePenalty.Value;
            if (ov.WeaponErgonomicPenalty.HasValue) props.WeaponErgonomicPenalty = ov.WeaponErgonomicPenalty.Value;
            count++;
        }

        return count;
    }

    private int ApplyWeapons()
    {
        EnsureSnapshot();
        var items = databaseService.GetItems();
        var config = configService.GetConfig().GameValues;

        // Step 1: Restore ALL weapons from snapshots
        foreach (var (tpl, snap) in _weaponSnapshots)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            props.Ergonomics = snap.Ergonomics;
            props.RecoilForceUp = snap.RecoilForceUp;
            props.RecoilForceBack = snap.RecoilForceBack;
            props.RecoilAngle = snap.RecoilAngle;
            props.BFirerate = snap.BFirerate;
            props.CenterOfImpact = snap.CenterOfImpact;
            props.SightingRange = snap.SightingRange;
            props.Durability = snap.Durability;
            props.MaxDurability = snap.MaxDurability;
            props.HeatFactorGun = snap.HeatFactorGun;
            props.CoolFactorGun = snap.CoolFactorGun;
            props.BaseMalfunctionChance = snap.BaseMalfunctionChance;
            props.Velocity = snap.Velocity;
            props.DeviationMax = snap.DeviationMax;
        }

        // Step 2: Apply overrides
        var count = 0;
        foreach (var (tpl, ov) in config.WeaponOverrides)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            if (ov.Ergonomics.HasValue) props.Ergonomics = ov.Ergonomics.Value;
            if (ov.RecoilForceUp.HasValue) props.RecoilForceUp = ov.RecoilForceUp.Value;
            if (ov.RecoilForceBack.HasValue) props.RecoilForceBack = ov.RecoilForceBack.Value;
            if (ov.RecoilAngle.HasValue) props.RecoilAngle = ov.RecoilAngle.Value;
            if (ov.BFirerate.HasValue) props.BFirerate = ov.BFirerate.Value;
            if (ov.CenterOfImpact.HasValue) props.CenterOfImpact = ov.CenterOfImpact.Value;
            if (ov.SightingRange.HasValue) props.SightingRange = ov.SightingRange.Value;
            if (ov.Durability.HasValue) props.Durability = ov.Durability.Value;
            if (ov.MaxDurability.HasValue) props.MaxDurability = ov.MaxDurability.Value;
            if (ov.HeatFactorGun.HasValue) props.HeatFactorGun = ov.HeatFactorGun.Value;
            if (ov.CoolFactorGun.HasValue) props.CoolFactorGun = ov.CoolFactorGun.Value;
            if (ov.BaseMalfunctionChance.HasValue) props.BaseMalfunctionChance = ov.BaseMalfunctionChance.Value;
            if (ov.Velocity.HasValue) props.Velocity = ov.Velocity.Value;
            if (ov.DeviationMax.HasValue) props.DeviationMax = ov.DeviationMax.Value;
            count++;
        }

        return count;
    }

    private int ApplyMedical()
    {
        EnsureSnapshot();
        var items = databaseService.GetItems();
        var config = configService.GetConfig().GameValues;

        // Step 1: Restore ALL medical items from snapshots
        foreach (var (tpl, snap) in _medicalSnapshots)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            props.MaxHpResource = (int)snap.MaxHpResource;
            props.HpResourceRate = snap.HpResourceRate;
            props.MedUseTime = snap.MedUseTime;

            // Restore EffectsDamage values (only if key existed in snapshot)
            if (props.EffectsDamage != null)
            {
                if (snap.LightBleedingCost.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.LightBleeding, out var lb))
                    lb.Cost = snap.LightBleedingCost.Value;
                if (snap.HeavyBleedingCost.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.HeavyBleeding, out var hb))
                    hb.Cost = snap.HeavyBleedingCost.Value;
                if (snap.FractureCost.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.Fracture, out var fr))
                    fr.Cost = snap.FractureCost.Value;
                if (snap.PainDuration.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.Pain, out var pn))
                    pn.Duration = snap.PainDuration.Value;
                if (snap.ContusionDuration.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.Contusion, out var cn))
                    cn.Duration = snap.ContusionDuration.Value;
            }

            // Restore EffectsHealth values
            if (props.EffectsHealth != null)
            {
                if (snap.EnergyChange.HasValue && props.EffectsHealth.TryGetValue(HealthFactor.Energy, out var en))
                    en.Value = snap.EnergyChange.Value;
                if (snap.HydrationChange.HasValue && props.EffectsHealth.TryGetValue(HealthFactor.Hydration, out var hy))
                    hy.Value = snap.HydrationChange.Value;
            }
        }

        // Step 2: Apply overrides
        var count = 0;
        foreach (var (tpl, ov) in config.MedicalOverrides)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            if (ov.MaxHpResource.HasValue) props.MaxHpResource = (int)ov.MaxHpResource.Value;
            if (ov.HpResourceRate.HasValue) props.HpResourceRate = ov.HpResourceRate.Value;
            if (ov.MedUseTime.HasValue) props.MedUseTime = ov.MedUseTime.Value;

            // Apply EffectsDamage overrides (only to existing dict keys)
            if (props.EffectsDamage != null)
            {
                if (ov.LightBleedingCost.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.LightBleeding, out var lb))
                    lb.Cost = ov.LightBleedingCost.Value;
                if (ov.HeavyBleedingCost.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.HeavyBleeding, out var hb))
                    hb.Cost = ov.HeavyBleedingCost.Value;
                if (ov.FractureCost.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.Fracture, out var fr))
                    fr.Cost = ov.FractureCost.Value;
                if (ov.PainDuration.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.Pain, out var pn))
                    pn.Duration = ov.PainDuration.Value;
                if (ov.ContusionDuration.HasValue && props.EffectsDamage.TryGetValue(DamageEffectType.Contusion, out var cn))
                    cn.Duration = ov.ContusionDuration.Value;
            }

            // Apply EffectsHealth overrides (only to existing dict keys)
            if (props.EffectsHealth != null)
            {
                if (ov.EnergyChange.HasValue && props.EffectsHealth.TryGetValue(HealthFactor.Energy, out var en))
                    en.Value = ov.EnergyChange.Value;
                if (ov.HydrationChange.HasValue && props.EffectsHealth.TryGetValue(HealthFactor.Hydration, out var hy))
                    hy.Value = ov.HydrationChange.Value;
            }

            count++;
        }

        return count;
    }

    private int ApplyBackpacks()
    {
        EnsureSnapshot();
        var items = databaseService.GetItems();
        var config = configService.GetConfig().GameValues;

        // Step 1: Restore ALL backpacks from snapshots
        foreach (var (tpl, snap) in _backpackSnapshots)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            props.Weight = snap.Weight;
            props.SpeedPenaltyPercent = snap.SpeedPenaltyPercent;
            props.WeaponErgonomicPenalty = snap.WeaponErgonomicPenalty;
            props.MousePenalty = snap.MousePenalty;

            // Restore grid dimensions (first grid only, single-grid only)
            if (!snap.IsMultiGrid)
            {
                var grids = props.Grids?.ToList();
                if (grids is { Count: > 0 } && grids[0].Properties != null)
                {
                    grids[0].Properties!.CellsH = snap.GridWidth;
                    grids[0].Properties!.CellsV = snap.GridHeight;
                }
            }
        }

        // Step 2: Apply overrides
        var count = 0;
        foreach (var (tpl, ov) in config.BackpackOverrides)
        {
            if (!items.TryGetValue(tpl, out var template)) continue;
            var props = template.Properties;
            if (props == null) continue;

            if (ov.Weight.HasValue) props.Weight = ov.Weight.Value;
            if (ov.SpeedPenaltyPercent.HasValue) props.SpeedPenaltyPercent = ov.SpeedPenaltyPercent.Value;
            if (ov.WeaponErgonomicPenalty.HasValue) props.WeaponErgonomicPenalty = ov.WeaponErgonomicPenalty.Value;
            if (ov.MousePenalty.HasValue) props.MousePenalty = ov.MousePenalty.Value;

            // Grid overrides only for single-grid backpacks
            if (_backpackSnapshots.TryGetValue(tpl, out var snap) && !snap.IsMultiGrid)
            {
                var grids = props.Grids?.ToList();
                if (grids is { Count: > 0 } && grids[0].Properties != null)
                {
                    if (ov.GridWidth.HasValue) grids[0].Properties!.CellsH = ov.GridWidth.Value;
                    if (ov.GridHeight.HasValue) grids[0].Properties!.CellsV = ov.GridHeight.Value;
                }
            }

            count++;
        }

        return count;
    }

    private int ApplyStimBuffs()
    {
        EnsureSnapshot();
        var config = configService.GetConfig().GameValues;

        try
        {
            var buffsDict = databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            if (buffsDict == null) return 0;

            // Step 1: Restore ALL buffs from snapshots
            foreach (var (buffName, snapEffects) in _stimBuffSnapshots)
            {
                buffsDict[buffName] = snapEffects.Select(e => new Buff
                {
                    BuffType = e.BuffType,
                    Value = e.Value,
                    Duration = e.Duration,
                    Delay = e.Delay,
                    Chance = e.Chance,
                    AbsoluteValue = e.AbsoluteValue,
                    SkillName = e.SkillName,
                }).ToList();
            }

            // Step 2: Apply overrides
            var count = 0;
            foreach (var (buffName, ov) in config.StimBuffOverrides)
            {
                if (!_stimBuffSnapshots.ContainsKey(buffName)) continue;

                buffsDict[buffName] = ov.Effects.Select(e =>
                {
                    var clamped = GameValuesClamps.ClampBuffEffect(e);
                    return new Buff
                    {
                        BuffType = clamped.BuffType,
                        Value = clamped.Value,
                        Duration = clamped.Duration,
                        Delay = clamped.Delay,
                        Chance = clamped.Chance,
                        AbsoluteValue = clamped.AbsoluteValue,
                        SkillName = clamped.SkillName,
                    };
                }).ToList();
                count++;
            }

            return count;
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to apply stim buffs: {ex.Message}");
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════
    // MERGE HELPERS (non-null fields from `incoming` override `existing`)
    // ═══════════════════════════════════════════════════════

    private static AmmoOverride MergeAmmoOverride(AmmoOverride existing, AmmoOverride incoming) => new()
    {
        Damage = incoming.Damage ?? existing.Damage,
        PenetrationPower = incoming.PenetrationPower ?? existing.PenetrationPower,
        ArmorDamage = incoming.ArmorDamage ?? existing.ArmorDamage,
        InitialSpeed = incoming.InitialSpeed ?? existing.InitialSpeed,
        FragmentationChance = incoming.FragmentationChance ?? existing.FragmentationChance,
        RicochetChance = incoming.RicochetChance ?? existing.RicochetChance,
        ProjectileCount = incoming.ProjectileCount ?? existing.ProjectileCount,
        BulletMassGram = incoming.BulletMassGram ?? existing.BulletMassGram,
        BallisticCoeficient = incoming.BallisticCoeficient ?? existing.BallisticCoeficient,
        MisfireChance = incoming.MisfireChance ?? existing.MisfireChance,
        HeatFactor = incoming.HeatFactor ?? existing.HeatFactor,
        DurabilityBurnModificator = incoming.DurabilityBurnModificator ?? existing.DurabilityBurnModificator,
        LightBleedingDelta = incoming.LightBleedingDelta ?? existing.LightBleedingDelta,
        HeavyBleedingDelta = incoming.HeavyBleedingDelta ?? existing.HeavyBleedingDelta,
        StaminaBurnPerDamage = incoming.StaminaBurnPerDamage ?? existing.StaminaBurnPerDamage,
        AmmoAccr = incoming.AmmoAccr ?? existing.AmmoAccr,
        AmmoRec = incoming.AmmoRec ?? existing.AmmoRec,
    };

    private static bool IsAmmoOverrideEmpty(AmmoOverride o) =>
        o.Damage == null && o.PenetrationPower == null && o.ArmorDamage == null &&
        o.InitialSpeed == null && o.FragmentationChance == null && o.RicochetChance == null &&
        o.ProjectileCount == null && o.BulletMassGram == null && o.BallisticCoeficient == null &&
        o.MisfireChance == null && o.HeatFactor == null && o.DurabilityBurnModificator == null &&
        o.LightBleedingDelta == null && o.HeavyBleedingDelta == null && o.StaminaBurnPerDamage == null &&
        o.AmmoAccr == null && o.AmmoRec == null;

    private static ArmorOverride MergeArmorOverride(ArmorOverride existing, ArmorOverride incoming) => new()
    {
        ArmorClass = incoming.ArmorClass ?? existing.ArmorClass,
        Durability = incoming.Durability ?? existing.Durability,
        MaxDurability = incoming.MaxDurability ?? existing.MaxDurability,
        BluntThroughput = incoming.BluntThroughput ?? existing.BluntThroughput,
        SpeedPenaltyPercent = incoming.SpeedPenaltyPercent ?? existing.SpeedPenaltyPercent,
        MousePenalty = incoming.MousePenalty ?? existing.MousePenalty,
        WeaponErgonomicPenalty = incoming.WeaponErgonomicPenalty ?? existing.WeaponErgonomicPenalty,
    };

    private static bool IsArmorOverrideEmpty(ArmorOverride o) =>
        o.ArmorClass == null && o.Durability == null && o.MaxDurability == null &&
        o.BluntThroughput == null && o.SpeedPenaltyPercent == null && o.MousePenalty == null &&
        o.WeaponErgonomicPenalty == null;

    private static WeaponOverride MergeWeaponOverride(WeaponOverride existing, WeaponOverride incoming) => new()
    {
        Ergonomics = incoming.Ergonomics ?? existing.Ergonomics,
        RecoilForceUp = incoming.RecoilForceUp ?? existing.RecoilForceUp,
        RecoilForceBack = incoming.RecoilForceBack ?? existing.RecoilForceBack,
        RecoilAngle = incoming.RecoilAngle ?? existing.RecoilAngle,
        BFirerate = incoming.BFirerate ?? existing.BFirerate,
        CenterOfImpact = incoming.CenterOfImpact ?? existing.CenterOfImpact,
        SightingRange = incoming.SightingRange ?? existing.SightingRange,
        Durability = incoming.Durability ?? existing.Durability,
        MaxDurability = incoming.MaxDurability ?? existing.MaxDurability,
        HeatFactorGun = incoming.HeatFactorGun ?? existing.HeatFactorGun,
        CoolFactorGun = incoming.CoolFactorGun ?? existing.CoolFactorGun,
        BaseMalfunctionChance = incoming.BaseMalfunctionChance ?? existing.BaseMalfunctionChance,
        Velocity = incoming.Velocity ?? existing.Velocity,
        DeviationMax = incoming.DeviationMax ?? existing.DeviationMax,
    };

    private static bool IsWeaponOverrideEmpty(WeaponOverride o) =>
        o.Ergonomics == null && o.RecoilForceUp == null && o.RecoilForceBack == null &&
        o.RecoilAngle == null && o.BFirerate == null && o.CenterOfImpact == null &&
        o.SightingRange == null && o.Durability == null && o.MaxDurability == null &&
        o.HeatFactorGun == null && o.CoolFactorGun == null && o.BaseMalfunctionChance == null &&
        o.Velocity == null && o.DeviationMax == null;

    private static MedicalOverride MergeMedicalOverride(MedicalOverride existing, MedicalOverride incoming) => new()
    {
        MaxHpResource = incoming.MaxHpResource ?? existing.MaxHpResource,
        HpResourceRate = incoming.HpResourceRate ?? existing.HpResourceRate,
        MedUseTime = incoming.MedUseTime ?? existing.MedUseTime,
        LightBleedingCost = incoming.LightBleedingCost ?? existing.LightBleedingCost,
        HeavyBleedingCost = incoming.HeavyBleedingCost ?? existing.HeavyBleedingCost,
        FractureCost = incoming.FractureCost ?? existing.FractureCost,
        PainDuration = incoming.PainDuration ?? existing.PainDuration,
        ContusionDuration = incoming.ContusionDuration ?? existing.ContusionDuration,
        EnergyChange = incoming.EnergyChange ?? existing.EnergyChange,
        HydrationChange = incoming.HydrationChange ?? existing.HydrationChange,
    };

    private static bool IsMedicalOverrideEmpty(MedicalOverride o) =>
        o.MaxHpResource == null && o.HpResourceRate == null && o.MedUseTime == null &&
        o.LightBleedingCost == null && o.HeavyBleedingCost == null && o.FractureCost == null &&
        o.PainDuration == null && o.ContusionDuration == null &&
        o.EnergyChange == null && o.HydrationChange == null;

    private static BackpackOverride MergeBackpackOverride(BackpackOverride existing, BackpackOverride incoming) => new()
    {
        Weight = incoming.Weight ?? existing.Weight,
        SpeedPenaltyPercent = incoming.SpeedPenaltyPercent ?? existing.SpeedPenaltyPercent,
        WeaponErgonomicPenalty = incoming.WeaponErgonomicPenalty ?? existing.WeaponErgonomicPenalty,
        MousePenalty = incoming.MousePenalty ?? existing.MousePenalty,
        GridWidth = incoming.GridWidth ?? existing.GridWidth,
        GridHeight = incoming.GridHeight ?? existing.GridHeight,
    };

    private static bool IsBackpackOverrideEmpty(BackpackOverride o) =>
        o.Weight == null && o.SpeedPenaltyPercent == null && o.WeaponErgonomicPenalty == null &&
        o.MousePenalty == null && o.GridWidth == null && o.GridHeight == null;
}
