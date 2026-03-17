using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ItemStackService(
    DatabaseService databaseService,
    LocaleService localeService,
    ConfigService configService,
    ISptLogger<ItemStackService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ═══════════════════════════════════════════════════════
    // CONSTANTS
    // ═══════════════════════════════════════════════════════

    private const string AmmoParentId = "5485a8684bdc2da71d8b4567";
    private const string MoneyParentId = "543be5dd4bdc2deb348b4569";
    private const string KeyParentId = "5c99f98d86f7745c314214b3";
    private const string KeycardParentId = "5c164d2286f774194c5e69fa";
    private const string KeyMiscParentId = "543be5e94bdc2df1348b4568";
    private const string PocketParentId = "557596e64bdc2dc2118b4571";
    private const string InventoryParentId = "55d720f24bdc2d88028b456d";

    // Caliber category patterns (matched against item Name, same as SVM)
    private static readonly string[] PistolPatterns = ["9x19", "9x18pm", "9x21", "762x25tt", "46x30", "57x28", "1143x23", "127x33", "9x33r", "10MM", "40SW", "357SIG", "9MM", "45ACP", "50AE", "380AUTO"];
    private static readonly string[] RiflePatterns = ["762x39", "545x39", "556x45", "9x39", "366", "762x35", "300blk", "ATL15", "GRENDEL", "50WLF", "KURZ"];
    private static readonly string[] ShotgunPatterns = ["12x70", "20x70", "23x75"];
    private static readonly string[] MarksmanPatterns = ["762x51", "68x51", "762x54R", "762x54", "762x54r", "277"];
    private static readonly string[] LargeCaliberPatterns = ["127x55", "127x99", "86x70", "BMG"];

    // Currency item IDs
    private const string RoubleId = "5449016a4bdc2d6f028b456f";
    private const string DollarId = "5696686a4bdc2da3298b456a";
    private const string EuroId = "569668774bdc2da2298b4568";
    private const string GpCoinId = "5d235b4d86f7742e017bc88a";

    // D. Key/Keycard parent IDs
    private static readonly HashSet<string> MarkedKeyIds =
    [
        "5780cf7f2459777de4559322", "5d80c60f86f77440373c4ece", "5d80c62a86f7744036212b3f",
        "5ede7a8229445733cb4c18e2", "63a3a93f8a56922e82001f5d", "64ccc25f95763a1ae376e447",
        "62987dfc402c7f69bf010923"
    ];
    private const string AccessKeycardId = "5c94bbff86f7747ee735c08f";
    private const string ResidentialKeycardId = "5c1e495a86f7743109743dfb";

    // E. Weapon parent IDs
    private static readonly HashSet<string> WeaponParentIds =
    [
        "5447b5cf4bdc2d65278b4567", // Pistol
        "5447b6254bdc2dc3278b4568", // AR
        "5447b5f14bdc2d61278b4567", // SMG
        "5447bed64bdc2d97278b4568", // Sniper
        "5447b6094bdc2dc3278b4567", // Shotgun
        "5447b5e04bdc2d62278b4567", // MG
        "5447b6194bdc2d67278b4567"  // GL
    ];
    private const string AmmoItemParentId = "5661632d4bdc2d903d8b456b";
    private const string MagazineParentId = "5448bc234bdc2d3c308b4569";

    // F. Gear parent IDs
    private const string BackpackParentId = "5448e53e4bdc2d60728b4567";
    private const string SecureContainerParentId = "5448bf274bdc2dfc2f8b456a";
    private const string BossContainerId = "5c093ca986f7740a1867ab12"; // Kappa — SVM excludes from filter removal

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT STORAGE
    // ═══════════════════════════════════════════════════════

    // A. Ammo stacks: itemId → original StackMaxSize
    private readonly Dictionary<string, int?> _ammoStackSnapshots = new();
    private readonly Dictionary<string, string> _ammoCaliberCategory = new(); // itemId → "pistol"|"rifle"|etc.

    // B. Currency stacks: itemId → original StackMaxSize
    private readonly Dictionary<string, int?> _currencyStackSnapshots = new();

    // C. Global properties
    private readonly Dictionary<string, double?> _weightSnapshots = new();
    private readonly Dictionary<string, double?> _examineTimeSnapshots = new();
    private readonly Dictionary<string, bool?> _examinedByDefaultSnapshots = new();
    private readonly Dictionary<string, int?> _lootXpSnapshots = new();
    private readonly Dictionary<string, int?> _examineXpSnapshots = new();
    private readonly List<(string Id, double? Price)> _handbookPriceSnapshots = [];
    private double _baseLoadTimeSnapshot;
    private double _baseUnloadTimeSnapshot;
    private int _maxBackpackInsertingSnapshot;

    // Default ammo stack ranges (computed from snapshot)
    private int _defaultPistolStack, _defaultRifleStack, _defaultShotgunStack, _defaultMarksmanStack, _defaultLargeCaliberStack, _defaultOtherStack;
    // Default currency stacks (from snapshot)
    private int _defaultRoubleStack, _defaultDollarStack, _defaultEuroStack, _defaultGpCoinStack;

    // D. Key/Keycard durability: itemId → original MaximumNumberOfUsage (double? in SPT)
    private readonly Dictionary<string, double?> _keyUsageSnapshots = new();
    private readonly Dictionary<string, double?> _keycardUsageSnapshots = new();

    // E. Weapon mechanics
    private readonly Dictionary<string, double?> _baseMalfunctionSnapshots = new(); // weapon items
    private readonly Dictionary<string, double?> _magMalfunctionSnapshots = new();  // magazine items
    private readonly Dictionary<string, double?> _misfireSnapshots = new();         // ammo items (MisfireChance)
    private readonly Dictionary<string, double?> _malfMisfireSnapshots = new();     // ammo items (MalfMisfireChance)
    private readonly Dictionary<string, double?> _fragmentationSnapshots = new();   // all items with FragmentationChance
    private readonly Dictionary<string, double?> _heatFactorSnapshots = new();      // all items with HeatFactor
    private readonly Dictionary<string, double?> _heatFactorByShotSnapshots = new();
    private readonly Dictionary<string, bool?> _allowOverheatSnapshots = new();

    // F. Gear toggles
    private readonly Dictionary<string, double?> _mousePenaltySnapshots = new();
    private readonly Dictionary<string, double?> _speedPenaltySnapshots = new();
    private readonly Dictionary<string, double?> _ergopenaltySnapshots = new();
    private readonly Dictionary<string, bool?> _blocksArmorVestSnapshots = new();
    private readonly Dictionary<string, double?> _discardLimitSnapshots = new();
    // Grid filter snapshots: itemId → list of (Filter set, ExcludedFilter set) per grid
    private readonly Dictionary<string, List<(HashSet<MongoId> Filter, HashSet<MongoId> Excluded)>> _backpackFilterSnapshots = new();
    private readonly Dictionary<string, List<(HashSet<MongoId> Filter, HashSet<MongoId> Excluded)>> _secConFilterSnapshots = new();

    // G. Secure container sizes: itemId → original (CellsH, CellsV) of first grid
    private readonly Dictionary<string, (int W, int H)> _secConSizeSnapshots = new();

    // H. Case sizes: itemId → original (CellsH, CellsV) of first grid
    private readonly Dictionary<string, (int W, int H)> _caseSizeSnapshots = new();
    private readonly Dictionary<string, List<(HashSet<MongoId> Filter, HashSet<MongoId> Excluded)>> _caseFilterSnapshots = new();

    // Parent IDs to EXCLUDE from case discovery (things with grids that are NOT cases)
    private static readonly HashSet<string> NonCaseParentIds =
    [
        "5448e53e4bdc2d60728b4567", // Backpack
        "5448bf274bdc2dfc2f8b456a", // SecureContainer
        "557596e64bdc2dc2118b4571", // Pockets
        "55d720f24bdc2d88028b456d", // Inventory (stash)
        "5447e1d04bdc2d4148037350", // SpecialSlot
        "5448e5284bdc2dcb718b4567", // Vest/Rig
        "5448e54d4bdc2dcc718b4568", // Armor
        "5447b5cf4bdc2d65278b4567", // Pistol
        "5447b6254bdc2dc3278b4568", // AR
        "5447b5f14bdc2d61278b4567", // SMG
        "5447bed64bdc2d97278b4568", // Sniper
        "5447b6094bdc2dc3278b4567", // Shotgun
        "5447b5e04bdc2d62278b4567", // MG
        "5447b6194bdc2d67278b4567", // GL
        "5448bc234bdc2d3c308b4569", // Magazine
    ];

    // Category metadata for frontend
    private readonly List<AmmoCategoryInfo> _ammoCategoryInfo = [];
    private int _raidRestrictionCount;
    private int _totalItemCount;

    // ═══════════════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var count = ApplyAll();
            if (count > 0)
                logger.Success($"[ZSlayerHQ] Items & Stacks: applied {count} change(s)");
            else
                logger.Info("[ZSlayerHQ] Items & Stacks: initialized (no overrides)");
        }
    }

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var items = databaseService.GetItems();
        var globals = databaseService.GetGlobals();
        var handbook = databaseService.GetHandbook();

        foreach (var item in items.Values)
        {
            var id = item.Id.ToString();
            var parentId = item.Parent.ToString();

            // A. Ammo stacks
            if (parentId == AmmoParentId)
            {
                _ammoStackSnapshots[id] = item.Properties.StackMaxSize;
                var name = item.Name ?? "";
                if (MatchesAny(name, PistolPatterns)) _ammoCaliberCategory[id] = "pistol";
                else if (MatchesAny(name, RiflePatterns)) _ammoCaliberCategory[id] = "rifle";
                else if (MatchesAny(name, ShotgunPatterns)) _ammoCaliberCategory[id] = "shotgun";
                else if (MatchesAny(name, MarksmanPatterns)) _ammoCaliberCategory[id] = "marksman";
                else if (MatchesAny(name, LargeCaliberPatterns)) _ammoCaliberCategory[id] = "largeCaliber";
                else _ammoCaliberCategory[id] = "other";
            }

            // B. Currency stacks
            if (parentId == MoneyParentId)
            {
                _currencyStackSnapshots[id] = item.Properties.StackMaxSize;
            }

            // C. Global properties — only snapshot items that have these fields
            if (item.Type == "Item" && parentId != PocketParentId && parentId != InventoryParentId)
            {
                _weightSnapshots[id] = item.Properties.Weight;
            }

            if (item.Type == "Item" && item.Properties.ExamineTime != null)
            {
                _examineTimeSnapshots[id] = item.Properties.ExamineTime;
            }

            if (item.Properties.ExaminedByDefault != null)
            {
                _examinedByDefaultSnapshots[id] = item.Properties.ExaminedByDefault;
            }

            if (item.Properties.LootExperience != null)
            {
                _lootXpSnapshots[id] = item.Properties.LootExperience;
            }

            if (item.Properties.ExamineExperience != null)
            {
                _examineXpSnapshots[id] = item.Properties.ExamineExperience;
            }

            // D. Key/Keycard durability
            if (parentId == KeyParentId && item.Properties.MaximumNumberOfUsage != null)
            {
                _keyUsageSnapshots[id] = item.Properties.MaximumNumberOfUsage;
            }
            if (parentId == KeycardParentId && item.Properties.MaximumNumberOfUsage != null)
            {
                _keycardUsageSnapshots[id] = item.Properties.MaximumNumberOfUsage;
            }

            // E. Weapon mechanics
            if (WeaponParentIds.Contains(parentId) && item.Properties.BaseMalfunctionChance != null)
            {
                _baseMalfunctionSnapshots[id] = item.Properties.BaseMalfunctionChance;
            }
            if (parentId == MagazineParentId && item.Properties.MalfunctionChance != null)
            {
                _magMalfunctionSnapshots[id] = item.Properties.MalfunctionChance;
            }
            if (parentId == AmmoItemParentId)
            {
                if (item.Properties.MisfireChance != null) _misfireSnapshots[id] = item.Properties.MisfireChance;
                if (item.Properties.MalfMisfireChance != null) _malfMisfireSnapshots[id] = item.Properties.MalfMisfireChance;
            }
            if (item.Properties.FragmentationChance != null)
            {
                _fragmentationSnapshots[id] = item.Properties.FragmentationChance;
            }
            if (item.Properties.HeatFactor != null)
            {
                _heatFactorSnapshots[id] = item.Properties.HeatFactor;
            }
            if (item.Properties.HeatFactorByShot != null)
            {
                _heatFactorByShotSnapshots[id] = item.Properties.HeatFactorByShot;
            }
            if (item.Properties.AllowOverheat != null)
            {
                _allowOverheatSnapshots[id] = item.Properties.AllowOverheat;
            }

            // F. Gear toggles
            if (item.Properties.MousePenalty != null)
                _mousePenaltySnapshots[id] = item.Properties.MousePenalty;
            if (item.Properties.SpeedPenaltyPercent != null)
                _speedPenaltySnapshots[id] = item.Properties.SpeedPenaltyPercent;
            if (item.Properties.WeaponErgonomicPenalty != null)
                _ergopenaltySnapshots[id] = item.Properties.WeaponErgonomicPenalty;
            if (item.Properties.BlocksArmorVest != null)
                _blocksArmorVestSnapshots[id] = item.Properties.BlocksArmorVest;
            if (item.Type == "Item" && item.Properties.DiscardLimit != null)
                _discardLimitSnapshots[id] = item.Properties.DiscardLimit;

            // Backpack grid filter snapshots
            if (parentId == BackpackParentId && item.Properties.Grids != null)
            {
                var gridFilters = new List<(HashSet<MongoId> Filter, HashSet<MongoId> Excluded)>();
                foreach (var grid in item.Properties.Grids)
                {
                    if (grid.Properties?.Filters != null)
                    {
                        var filterList = grid.Properties.Filters.ToList();
                        if (filterList.Count > 0)
                        {
                            gridFilters.Add((
                                filterList[0].Filter?.ToHashSet() ?? [],
                                filterList[0].ExcludedFilter?.ToHashSet() ?? []
                            ));
                        }
                        else gridFilters.Add(([], []));
                    }
                    else gridFilters.Add(([], []));
                }
                _backpackFilterSnapshots[id] = gridFilters;
            }

            // Secure container grid filter + size snapshots
            if (parentId == SecureContainerParentId && item.Properties.Grids != null)
            {
                var gridFilters = new List<(HashSet<MongoId> Filter, HashSet<MongoId> Excluded)>();
                foreach (var grid in item.Properties.Grids)
                {
                    if (grid.Properties?.Filters != null)
                    {
                        var filterList = grid.Properties.Filters.ToList();
                        if (filterList.Count > 0)
                        {
                            gridFilters.Add((
                                filterList[0].Filter?.ToHashSet() ?? [],
                                filterList[0].ExcludedFilter?.ToHashSet() ?? []
                            ));
                        }
                        else gridFilters.Add(([], []));
                    }
                    else gridFilters.Add(([], []));
                }
                _secConFilterSnapshots[id] = gridFilters;

                // G. Snapshot first grid size
                var secGrids = item.Properties.Grids.ToList();
                if (secGrids.Count > 0 && secGrids[0].Properties != null)
                    _secConSizeSnapshots[id] = ((int)(secGrids[0].Properties.CellsH ?? 0), (int)(secGrids[0].Properties.CellsV ?? 0));
            }

            // H. Case grid size + filter snapshots (items with grids that aren't in the exclude list)
            if (!NonCaseParentIds.Contains(parentId) && parentId != SecureContainerParentId
                && parentId != BackpackParentId && item.Properties.Grids != null)
            {
                var caseGrids = item.Properties.Grids.ToList();
                if (caseGrids.Count > 0 && caseGrids[0].Properties != null)
                {
                    _caseSizeSnapshots[id] = ((int)(caseGrids[0].Properties.CellsH ?? 0), (int)(caseGrids[0].Properties.CellsV ?? 0));

                    // Also snapshot filters for cases
                    var caseFilters = new List<(HashSet<MongoId> Filter, HashSet<MongoId> Excluded)>();
                    foreach (var grid in caseGrids)
                    {
                        if (grid.Properties?.Filters != null)
                        {
                            var filterList = grid.Properties.Filters.ToList();
                            if (filterList.Count > 0)
                            {
                                caseFilters.Add((
                                    filterList[0].Filter?.ToHashSet() ?? [],
                                    filterList[0].ExcludedFilter?.ToHashSet() ?? []
                                ));
                            }
                            else caseFilters.Add(([], []));
                        }
                        else caseFilters.Add(([], []));
                    }
                    _caseFilterSnapshots[id] = caseFilters;
                }
            }
        }

        // Handbook price snapshots
        if (handbook?.Items != null)
        {
            foreach (var entry in handbook.Items)
            {
                var entryId = entry.Id.ToString();
                if (entryId != "5b5f78b786f77447ed5636af" && entry.Price != null)
                    _handbookPriceSnapshots.Add((entryId, entry.Price));
            }
        }

        // Globals snapshots
        _baseLoadTimeSnapshot = globals.Configuration.BaseLoadTime;
        _baseUnloadTimeSnapshot = globals.Configuration.BaseUnloadTime;
        if (globals.Configuration.ItemsCommonSettings != null)
            _maxBackpackInsertingSnapshot = (int)globals.Configuration.ItemsCommonSettings.MaxBackpackInserting;

        // Raid restrictions snapshot
        _raidRestrictionCount = globals.Configuration.RestrictionsInRaid?.Count() ?? 0;
        _totalItemCount = _weightSnapshots.Count;

        // Compute default stack values from snapshots (use median/representative values)
        _defaultPistolStack = ComputeMedianStack(_ammoStackSnapshots, _ammoCaliberCategory, "pistol");
        _defaultRifleStack = ComputeMedianStack(_ammoStackSnapshots, _ammoCaliberCategory, "rifle");
        _defaultShotgunStack = ComputeMedianStack(_ammoStackSnapshots, _ammoCaliberCategory, "shotgun");
        _defaultMarksmanStack = ComputeMedianStack(_ammoStackSnapshots, _ammoCaliberCategory, "marksman");
        _defaultLargeCaliberStack = ComputeMedianStack(_ammoStackSnapshots, _ammoCaliberCategory, "largeCaliber");
        _defaultOtherStack = ComputeMedianStack(_ammoStackSnapshots, _ammoCaliberCategory, "other");

        _defaultRoubleStack = _currencyStackSnapshots.GetValueOrDefault(RoubleId) ?? 500_000;
        _defaultDollarStack = _currencyStackSnapshots.GetValueOrDefault(DollarId) ?? 50_000;
        _defaultEuroStack = _currencyStackSnapshots.GetValueOrDefault(EuroId) ?? 50_000;
        _defaultGpCoinStack = _currencyStackSnapshots.GetValueOrDefault(GpCoinId) ?? 100;

        // Build category metadata for frontend
        var locale = localeService.GetLocaleDb("en");
        var categoryDefs = new (string Key, string Label, int Default)[]
        {
            ("pistol", "Pistol", _defaultPistolStack),
            ("rifle", "Assault Rifle", _defaultRifleStack),
            ("shotgun", "Shotgun", _defaultShotgunStack),
            ("marksman", "Marksman Rifle", _defaultMarksmanStack),
            ("largeCaliber", "Large Caliber", _defaultLargeCaliberStack),
            ("other", "Other", _defaultOtherStack),
        };
        foreach (var (catKey, catLabel, catDefault) in categoryDefs)
        {
            var catIds = _ammoCaliberCategory.Where(kv => kv.Value == catKey).Select(kv => kv.Key).ToList();
            var examples = new List<string>();
            foreach (var catId in catIds.Take(4))
            {
                if (items.TryGetValue(new MongoId(catId), out var ammoItem))
                {
                    var localeName = locale.TryGetValue($"{catId} ShortName", out var ln) ? ln : ammoItem.Name ?? catId;
                    examples.Add(localeName);
                }
            }
            _ammoCategoryInfo.Add(new AmmoCategoryInfo
            {
                Category = catKey,
                Label = catLabel,
                Count = catIds.Count,
                DefaultStack = catDefault,
                Examples = examples
            });
        }

        _snapshotTaken = true;
        var uncategorized = _ammoStackSnapshots.Count - _ammoCaliberCategory.Count;
        logger.Info($"[ZSlayerHQ] Items & Stacks: snapshot taken — {_ammoStackSnapshots.Count} ammo ({_ammoCaliberCategory.Count} categorized), {_currencyStackSnapshots.Count} currencies, {_weightSnapshots.Count} weights, {_handbookPriceSnapshots.Count} handbook prices");
    }

    // ═══════════════════════════════════════════════════════
    // APPLY
    // ═══════════════════════════════════════════════════════

    private int ApplyAll()
    {
        var config = configService.GetConfig().ItemStacks;
        var items = databaseService.GetItems();
        var globals = databaseService.GetGlobals();
        var handbook = databaseService.GetHandbook();
        int changes = 0;

        // ── Restore from snapshot first (prevents compounding) ──

        // A. Restore ammo stacks
        foreach (var (id, original) in _ammoStackSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.StackMaxSize = original;
        }

        // B. Restore currency stacks
        foreach (var (id, original) in _currencyStackSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.StackMaxSize = original;
        }

        // C. Restore global properties
        foreach (var (id, original) in _weightSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.Weight = original;
        }
        foreach (var (id, original) in _examineTimeSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.ExamineTime = original;
        }
        foreach (var (id, original) in _examinedByDefaultSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.ExaminedByDefault = original;
        }
        foreach (var (id, original) in _lootXpSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.LootExperience = original;
        }
        foreach (var (id, original) in _examineXpSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.ExamineExperience = original;
        }

        // Restore handbook prices
        if (handbook?.Items != null)
        {
            var priceMap = new Dictionary<string, double?>();
            foreach (var (snapId, snapPrice) in _handbookPriceSnapshots)
                priceMap[snapId] = snapPrice;
            foreach (var entry in handbook.Items)
            {
                var entryId = entry.Id.ToString();
                if (priceMap.TryGetValue(entryId, out var origPrice))
                    entry.Price = origPrice;
            }
        }

        // Restore globals
        globals.Configuration.BaseLoadTime = _baseLoadTimeSnapshot;
        globals.Configuration.BaseUnloadTime = _baseUnloadTimeSnapshot;
        if (globals.Configuration.ItemsCommonSettings != null)
            globals.Configuration.ItemsCommonSettings.MaxBackpackInserting = _maxBackpackInsertingSnapshot;

        // D. Restore key/keycard durability
        foreach (var (id, original) in _keyUsageSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.MaximumNumberOfUsage = (int?)original;
        }
        foreach (var (id, original) in _keycardUsageSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.MaximumNumberOfUsage = (int?)original;
        }

        // E. Restore weapon mechanics
        foreach (var (id, original) in _baseMalfunctionSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.BaseMalfunctionChance = original;
        }
        foreach (var (id, original) in _magMalfunctionSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.MalfunctionChance = original;
        }
        foreach (var (id, original) in _misfireSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.MisfireChance = original;
        }
        foreach (var (id, original) in _malfMisfireSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.MalfMisfireChance = original;
        }
        foreach (var (id, original) in _fragmentationSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.FragmentationChance = original;
        }
        foreach (var (id, original) in _heatFactorSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.HeatFactor = original;
        }
        foreach (var (id, original) in _heatFactorByShotSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.HeatFactorByShot = original;
        }
        foreach (var (id, original) in _allowOverheatSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.AllowOverheat = original;
        }

        // F. Restore gear toggles
        foreach (var (id, original) in _mousePenaltySnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.MousePenalty = original;
        }
        foreach (var (id, original) in _speedPenaltySnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.SpeedPenaltyPercent = original;
        }
        foreach (var (id, original) in _ergopenaltySnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.WeaponErgonomicPenalty = original;
        }
        foreach (var (id, original) in _blocksArmorVestSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.BlocksArmorVest = original;
        }
        foreach (var (id, original) in _discardLimitSnapshots)
        {
            if (items.TryGetValue(new MongoId(id), out var item))
                item.Properties.DiscardLimit = original;
        }
        // Restore backpack grid filters
        foreach (var (id, gridSnaps) in _backpackFilterSnapshots)
        {
            if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
            var grids = item.Properties.Grids.ToList();
            for (int i = 0; i < grids.Count && i < gridSnaps.Count; i++)
            {
                if (grids[i].Properties?.Filters != null)
                {
                    var filters = grids[i].Properties.Filters.ToList();
                    if (filters.Count > 0)
                    {
                        filters[0].Filter = gridSnaps[i].Filter.ToHashSet();
                        filters[0].ExcludedFilter = gridSnaps[i].Excluded.ToHashSet();
                        grids[i].Properties.Filters = filters;
                    }
                }
            }
            item.Properties.Grids = grids;
        }
        // Restore secure container grid filters
        foreach (var (id, gridSnaps) in _secConFilterSnapshots)
        {
            if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
            var grids = item.Properties.Grids.ToList();
            for (int i = 0; i < grids.Count && i < gridSnaps.Count; i++)
            {
                if (grids[i].Properties?.Filters != null)
                {
                    var filters = grids[i].Properties.Filters.ToList();
                    if (filters.Count > 0)
                    {
                        filters[0].Filter = gridSnaps[i].Filter.ToHashSet();
                        filters[0].ExcludedFilter = gridSnaps[i].Excluded.ToHashSet();
                        grids[i].Properties.Filters = filters;
                    }
                }
            }
            item.Properties.Grids = grids;
        }

        // G. Restore secure container sizes
        foreach (var (id, (origW, origH)) in _secConSizeSnapshots)
        {
            if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
            var grids = item.Properties.Grids.ToList();
            if (grids.Count > 0 && grids[0].Properties != null)
            {
                grids[0].Properties.CellsH = origW;
                grids[0].Properties.CellsV = origH;
                item.Properties.Grids = grids;
            }
        }

        // H. Restore case sizes + filters
        foreach (var (id, (origW, origH)) in _caseSizeSnapshots)
        {
            if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
            var grids = item.Properties.Grids.ToList();
            if (grids.Count > 0 && grids[0].Properties != null)
            {
                grids[0].Properties.CellsH = origW;
                grids[0].Properties.CellsV = origH;
                item.Properties.Grids = grids;
            }
        }
        foreach (var (id, gridSnaps) in _caseFilterSnapshots)
        {
            if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
            var grids = item.Properties.Grids.ToList();
            for (int i = 0; i < grids.Count && i < gridSnaps.Count; i++)
            {
                if (grids[i].Properties?.Filters != null)
                {
                    var filters = grids[i].Properties.Filters.ToList();
                    if (filters.Count > 0)
                    {
                        filters[0].Filter = gridSnaps[i].Filter.ToHashSet();
                        filters[0].ExcludedFilter = gridSnaps[i].Excluded.ToHashSet();
                        grids[i].Properties.Filters = filters;
                    }
                }
            }
            item.Properties.Grids = grids;
        }

        // ── Now apply fresh from config ──

        // A. Ammo stacks (category-based)
        if (config.EnableAmmoStacks)
        {
            foreach (var (id, category) in _ammoCaliberCategory)
            {
                if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                var stackSize = category switch
                {
                    "pistol" => config.AmmoStacks.Pistol,
                    "rifle" => config.AmmoStacks.Rifle,
                    "shotgun" => config.AmmoStacks.Shotgun,
                    "marksman" => config.AmmoStacks.Marksman,
                    "largeCaliber" => config.AmmoStacks.LargeCaliber,
                    "other" => config.AmmoStacks.Other,
                    _ => (int?)null
                };
                if (stackSize is > 0)
                {
                    item.Properties.StackMaxSize = stackSize;
                    changes++;
                }
            }
        }

        // Per-item stack overrides (applies on top of category, works for any ammo/currency item)
        if (config.PerItemStackOverrides.Count > 0)
        {
            foreach (var (itemId, overrideStack) in config.PerItemStackOverrides)
            {
                if (overrideStack > 0 && items.TryGetValue(new MongoId(itemId), out var item))
                {
                    item.Properties.StackMaxSize = overrideStack;
                    changes++;
                }
            }
        }

        // B. Currency stacks
        if (config.EnableCurrencyStacks)
        {
            void SetCurrency(string itemId, int value)
            {
                if (items.TryGetValue(new MongoId(itemId), out var item))
                {
                    item.Properties.StackMaxSize = value;
                    changes++;
                }
            }
            SetCurrency(RoubleId, config.CurrencyStacks.Roubles);
            SetCurrency(DollarId, config.CurrencyStacks.Dollars);
            SetCurrency(EuroId, config.CurrencyStacks.Euros);
            SetCurrency(GpCoinId, config.CurrencyStacks.GpCoins);
        }

        // C. Global properties
        if (config.Weightless)
        {
            foreach (var (id, _) in _weightSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                {
                    item.Properties.Weight = 0;
                    changes++;
                }
            }
        }
        else if (Math.Abs(config.WeightMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _weightSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.Weight != null)
                {
                    item.Properties.Weight *= config.WeightMult;
                    changes++;
                }
            }
        }

        if (config.ExamineTimeOverride is { } examTime)
        {
            foreach (var (id, _) in _examineTimeSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                {
                    item.Properties.ExamineTime = examTime;
                    changes++;
                }
            }
        }

        if (config.AutoExamineAll)
        {
            foreach (var (id, _) in _examinedByDefaultSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                {
                    item.Properties.ExaminedByDefault = true;
                    changes++;
                }
            }
        }
        else if (config.AutoExamineKeysOnly)
        {
            // Only examine keys and keycards
            foreach (var (id, _) in _examinedByDefaultSnapshots)
            {
                if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                var parentId = item.Parent.ToString();
                if (parentId == KeyParentId || parentId == KeycardParentId || parentId == KeyMiscParentId)
                {
                    item.Properties.ExaminedByDefault = true;
                    changes++;
                }
            }
        }

        if (Math.Abs(config.HandbookPriceMult - 1.0) > 0.001 && handbook?.Items != null)
        {
            foreach (var entry in handbook.Items)
            {
                var entryId = entry.Id.ToString();
                if (entryId != "5b5f78b786f77447ed5636af" && entry.Price != null)
                {
                    entry.Price *= config.HandbookPriceMult;
                    changes++;
                }
            }
        }

        if (Math.Abs(config.LootXpMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _lootXpSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.LootExperience != null)
                {
                    item.Properties.LootExperience = Math.Max((int)(item.Properties.LootExperience * config.LootXpMult), 1);
                    changes++;
                }
            }
        }

        if (Math.Abs(config.ExamineXpMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _examineXpSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.ExamineExperience != null)
                {
                    item.Properties.ExamineExperience = Math.Max((int)(item.Properties.ExamineExperience * config.ExamineXpMult), 1);
                    changes++;
                }
            }
        }

        if (Math.Abs(config.AmmoLoadSpeedMult - 1.0) > 0.001)
        {
            globals.Configuration.BaseLoadTime *= config.AmmoLoadSpeedMult;
            globals.Configuration.BaseUnloadTime *= config.AmmoLoadSpeedMult;
            changes++;
        }

        if (config.RemoveRaidRestrictions)
        {
            globals.Configuration.RestrictionsInRaid = [];
            changes++;
        }

        if (config.BackpackStackingLimit is { } bpLimit && globals.Configuration.ItemsCommonSettings != null)
        {
            globals.Configuration.ItemsCommonSettings.MaxBackpackInserting = bpLimit;
            changes++;
        }

        // ── D. Key & Keycard Durability ──
        if (config.EnableKeyChanges)
        {
            // Keys — infinite
            if (config.InfiniteKeys)
            {
                foreach (var (id, original) in _keyUsageSnapshots)
                {
                    if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                    var usage = (int)(original ?? 0);
                    if (config.ExcludeSingleUseKeys && usage == 1) continue;
                    if (config.ExcludeMarkedKeys && MarkedKeyIds.Contains(id)) continue;
                    item.Properties.MaximumNumberOfUsage = 0;
                    changes++;
                }
            }

            // Keys — multiplier + threshold (for non-infinite keys)
            if (Math.Abs(config.KeyUseMult - 1.0) > 0.001 || config.KeyDurabilityThreshold < 100)
            {
                foreach (var (id, original) in _keyUsageSnapshots)
                {
                    if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                    if (item.Properties.MaximumNumberOfUsage == 0) continue; // already infinite
                    var val = Math.Max((int)((original ?? 1) * config.KeyUseMult), 1);
                    if (val > config.KeyDurabilityThreshold)
                        val = config.KeyDurabilityThreshold;
                    item.Properties.MaximumNumberOfUsage = val;
                    changes++;
                }
            }

            // Keycards — infinite
            if (config.InfiniteKeycards)
            {
                foreach (var (id, original) in _keycardUsageSnapshots)
                {
                    if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                    var usage = (int)(original ?? 0);
                    if (config.ExcludeSingleUseKeycards && usage == 1) continue;
                    if (config.ExcludeAccessKeycard && id == AccessKeycardId) continue;
                    if (config.ExcludeResidentialKeycard && id == ResidentialKeycardId) continue;
                    item.Properties.MaximumNumberOfUsage = 0;
                    changes++;
                }
            }

            // Keycards — multiplier + threshold
            if (Math.Abs(config.KeycardUseMult - 1.0) > 0.001 || config.KeycardDurabilityThreshold < 100)
            {
                foreach (var (id, original) in _keycardUsageSnapshots)
                {
                    if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                    if (item.Properties.MaximumNumberOfUsage == 0) continue; // already infinite
                    if (config.ExcludeAccessKeycard && id == AccessKeycardId) continue;
                    var val = Math.Max((int)((original ?? 1) * config.KeycardUseMult), 1);
                    if (val > config.KeycardDurabilityThreshold)
                        val = config.KeycardDurabilityThreshold;
                    item.Properties.MaximumNumberOfUsage = val;
                    changes++;
                }
            }
        }

        // ── E. Weapon Mechanics ──
        if (Math.Abs(config.MalfunctionMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _baseMalfunctionSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.BaseMalfunctionChance != null)
                {
                    item.Properties.BaseMalfunctionChance = Math.Round((double)(item.Properties.BaseMalfunctionChance * config.MalfunctionMult), 4);
                    changes++;
                }
            }
            foreach (var (id, _) in _magMalfunctionSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.MalfunctionChance != null)
                {
                    item.Properties.MalfunctionChance *= config.MalfunctionMult;
                    changes++;
                }
            }
        }

        if (Math.Abs(config.MisfireMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _misfireSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.MisfireChance != null)
                {
                    item.Properties.MisfireChance *= config.MisfireMult;
                    changes++;
                }
            }
            foreach (var (id, _) in _malfMisfireSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.MalfMisfireChance != null)
                {
                    item.Properties.MalfMisfireChance *= config.MisfireMult;
                    changes++;
                }
            }
        }

        if (Math.Abs(config.FragmentationMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _fragmentationSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.FragmentationChance != null)
                {
                    item.Properties.FragmentationChance *= config.FragmentationMult;
                    changes++;
                }
            }
        }

        if (Math.Abs(config.HeatFactorMult - 1.0) > 0.001)
        {
            foreach (var (id, _) in _heatFactorSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.HeatFactor != null)
                {
                    // SVM pattern: subtract multiplier offset for values > 1 (where 1 = 0% heat)
                    if (item.Properties.HeatFactor > 1)
                        item.Properties.HeatFactor = Math.Round(((double)item.Properties.HeatFactor * config.HeatFactorMult) - (config.HeatFactorMult - 1), 4);
                    changes++;
                }
            }
            foreach (var (id, _) in _heatFactorByShotSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item) && item.Properties.HeatFactorByShot != null)
                {
                    item.Properties.HeatFactorByShot *= config.HeatFactorMult;
                    changes++;
                }
            }
        }

        if (config.DisableOverheat)
        {
            foreach (var (id, _) in _allowOverheatSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                {
                    item.Properties.AllowOverheat = false;
                    changes++;
                }
            }
        }

        // ── F. Gear & Restriction Toggles ──
        if (config.RemoveGearPenalties)
        {
            foreach (var (id, _) in _mousePenaltySnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                    item.Properties.MousePenalty = 0;
            }
            foreach (var (id, _) in _speedPenaltySnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                    item.Properties.SpeedPenaltyPercent = 0;
            }
            foreach (var (id, _) in _ergopenaltySnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                    item.Properties.WeaponErgonomicPenalty = 0;
            }
            changes++;
        }

        if (config.AllowRigWithArmor)
        {
            foreach (var (id, _) in _blocksArmorVestSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                    item.Properties.BlocksArmorVest = false;
            }
            changes++;
        }

        if (config.RemoveDiscardLimits)
        {
            foreach (var (id, _) in _discardLimitSnapshots)
            {
                if (items.TryGetValue(new MongoId(id), out var item))
                    item.Properties.DiscardLimit = -1;
            }
            changes++;
        }

        if (config.RemoveBackpackRestrictions)
        {
            foreach (var (id, _) in _backpackFilterSnapshots)
            {
                if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
                var grids = item.Properties.Grids.ToList();
                foreach (var grid in grids)
                {
                    if (grid.Properties?.Filters == null) continue;
                    var filters = grid.Properties.Filters.ToList();
                    if (filters.Count > 0)
                    {
                        filters[0].Filter = [new MongoId("54009119af1c881c07000029")];
                        filters[0].ExcludedFilter = new HashSet<MongoId>();
                        grid.Properties.Filters = filters;
                    }
                }
                item.Properties.Grids = grids;
            }
            changes++;
        }

        if (config.RemoveSecureContainerFilters)
        {
            foreach (var (id, _) in _secConFilterSnapshots)
            {
                if (id == BossContainerId) continue; // SVM excludes Boss container
                if (!items.TryGetValue(new MongoId(id), out var item) || item.Properties.Grids == null) continue;
                var grids = item.Properties.Grids.ToList();
                foreach (var grid in grids)
                {
                    if (grid.Properties?.Filters == null) continue;
                    var filters = grid.Properties.Filters.ToList();
                    if (filters.Count > 0)
                    {
                        filters[0].Filter = [new MongoId("54009119af1c881c07000029")];
                        filters[0].ExcludedFilter = [];
                        grid.Properties.Filters = filters;
                    }
                }
                item.Properties.Grids = grids;
            }
            changes++;
        }

        // ── G. Secure Container Sizes ──
        if (config.EnableSecureContainerSizes && config.SecureContainerSizes.Count > 0)
        {
            foreach (var (containerId, sizeConfig) in config.SecureContainerSizes)
            {
                if (!items.TryGetValue(new MongoId(containerId), out var item) || item.Properties.Grids == null) continue;
                var grids = item.Properties.Grids.ToList();
                if (grids.Count == 0 || grids[0].Properties == null) continue;

                if (sizeConfig.W > 0) grids[0].Properties.CellsH = sizeConfig.W;
                if (sizeConfig.H > 0) grids[0].Properties.CellsV = sizeConfig.H;

                if (sizeConfig.RemoveFilter)
                {
                    if (containerId != BossContainerId) // exclude Kappa from filter removal
                    {
                        var filters = grids[0].Properties.Filters?.ToList();
                        if (filters is { Count: > 0 })
                        {
                            filters[0].Filter = [new MongoId("54009119af1c881c07000029")];
                            filters[0].ExcludedFilter = [];
                            grids[0].Properties.Filters = filters;
                        }
                    }
                }

                item.Properties.Grids = grids;
                changes++;
            }
        }

        // ── H. Case Sizes ──
        if (config.EnableCaseSizes && config.CaseSizes.Count > 0)
        {
            foreach (var (caseId, sizeConfig) in config.CaseSizes)
            {
                if (!items.TryGetValue(new MongoId(caseId), out var item) || item.Properties.Grids == null) continue;
                var grids = item.Properties.Grids.ToList();
                if (grids.Count == 0 || grids[0].Properties == null) continue;

                if (sizeConfig.W > 0) grids[0].Properties.CellsH = sizeConfig.W;
                if (sizeConfig.H > 0) grids[0].Properties.CellsV = sizeConfig.H;

                if (sizeConfig.RemoveFilter)
                {
                    var filters = grids[0].Properties.Filters?.ToList();
                    if (filters is { Count: > 0 })
                    {
                        filters[0].Filter = [new MongoId("54009119af1c881c07000029")];
                        filters[0].ExcludedFilter = [];
                        grids[0].Properties.Filters = filters;
                    }
                }

                item.Properties.Grids = grids;
                changes++;
            }
        }

        return changes;
    }

    // ═══════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════

    public ItemStackConfigResponse GetConfig()
    {
        lock (_lock)
        {
            var config = configService.GetConfig().ItemStacks;
            var globals = databaseService.GetGlobals();

            return new ItemStackConfigResponse
            {
                Config = config,
                Defaults = new ItemStackDefaults
                {
                    AmmoStacks = new AmmoStackConfig
                    {
                        Pistol = _defaultPistolStack,
                        Rifle = _defaultRifleStack,
                        Shotgun = _defaultShotgunStack,
                        Marksman = _defaultMarksmanStack,
                        LargeCaliber = _defaultLargeCaliberStack,
                        Other = _defaultOtherStack
                    },
                    AmmoCategories = _ammoCategoryInfo,
                    CurrencyStacks = new CurrencyStackConfig
                    {
                        Roubles = _defaultRoubleStack,
                        Dollars = _defaultDollarStack,
                        Euros = _defaultEuroStack,
                        GpCoins = _defaultGpCoinStack
                    },
                    BaseLoadTime = _baseLoadTimeSnapshot,
                    BaseUnloadTime = _baseUnloadTimeSnapshot,
                    MaxBackpackInserting = _maxBackpackInsertingSnapshot,
                    RaidRestrictionCount = _raidRestrictionCount,
                    TotalItemCount = _totalItemCount
                }
            };
        }
    }

    public int ApplyConfig(ItemStackConfig incoming)
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.ItemStacks = incoming;
            configService.SaveConfig();

            var count = ApplyAll();
            logger.Success($"[ZSlayerHQ] Items & Stacks: applied {count} change(s)");
            return count;
        }
    }

    public int ResetToDefaults()
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.ItemStacks = new ItemStackConfig();
            configService.SaveConfig();

            var count = ApplyAll(); // restores all from snapshot, then applies empty config (no-op)
            logger.Success("[ZSlayerHQ] Items & Stacks: reset to defaults");
            return count;
        }
    }

    public ContainersResponse GetContainers()
    {
        lock (_lock)
        {
            var items = databaseService.GetItems();
            var locale = localeService.GetLocaleDb("en");
            var config = configService.GetConfig().ItemStacks;

            var secureContainers = new List<ContainerDto>();
            foreach (var (id, (defaultW, defaultH)) in _secConSizeSnapshots)
            {
                if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                var name = locale.TryGetValue($"{id} Name", out var ln) ? ln : item.Name ?? id;

                // Current size = whatever's in the live DB right now
                var grids = item.Properties.Grids?.ToList();
                int currentW = defaultW, currentH = defaultH;
                if (grids is { Count: > 0 } && grids[0].Properties != null)
                {
                    currentW = (int)(grids[0].Properties.CellsH ?? defaultW);
                    currentH = (int)(grids[0].Properties.CellsV ?? defaultH);
                }

                secureContainers.Add(new ContainerDto
                {
                    Id = id, Name = name,
                    CurrentW = currentW, CurrentH = currentH,
                    DefaultW = defaultW, DefaultH = defaultH
                });
            }

            var cases = new List<ContainerDto>();
            foreach (var (id, (defaultW, defaultH)) in _caseSizeSnapshots)
            {
                if (!items.TryGetValue(new MongoId(id), out var item)) continue;
                var name = locale.TryGetValue($"{id} Name", out var ln) ? ln : item.Name ?? id;

                var grids = item.Properties.Grids?.ToList();
                int currentW = defaultW, currentH = defaultH;
                if (grids is { Count: > 0 } && grids[0].Properties != null)
                {
                    currentW = (int)(grids[0].Properties.CellsH ?? defaultW);
                    currentH = (int)(grids[0].Properties.CellsV ?? defaultH);
                }

                cases.Add(new ContainerDto
                {
                    Id = id, Name = name,
                    CurrentW = currentW, CurrentH = currentH,
                    DefaultW = defaultW, DefaultH = defaultH
                });
            }

            // Sort by name for consistent display
            secureContainers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            cases.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            return new ContainersResponse
            {
                SecureContainers = secureContainers,
                Cases = cases
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════

    private static bool MatchesAny(string name, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static int ComputeMedianStack(
        Dictionary<string, int?> stackSnapshots,
        Dictionary<string, string> caliberCategories,
        string category)
    {
        var values = new List<int>();
        foreach (var (id, cat) in caliberCategories)
        {
            if (cat == category && stackSnapshots.TryGetValue(id, out var stack) && stack is > 0)
                values.Add(stack.Value);
        }
        if (values.Count == 0) return 0;
        values.Sort();
        return values[values.Count / 2]; // median
    }
}
