using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class SkillEditorService(
    DatabaseService databaseService,
    ConfigService configService,
    SaveServer saveServer,
    ISptLogger<SkillEditorService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // Snapshot of all skill bonus field values (skillName.fieldName → value)
    private readonly Dictionary<string, double> _bonusSnapshots = new();

    // Typed skill property names on SkillsSettings (excludes IEnumerable<object> stubs)
    private static readonly HashSet<string> TypedSkillNames =
    [
        "Endurance", "Strength", "Vitality", "Health", "Metabolism",
        "StressResistance", "Immunity", "Perception", "Intellect", "Attention",
        "Charisma", "Memory", "Surgery", "CovertMovement", "Search",
        "MagDrills", "HideoutManagement", "Crafting", "Throwing",
        "RecoilControl", "WeaponTreatment", "AimDrills", "TroubleShooting",
        "LightVests", "HeavyVests"
    ];

    private static readonly HashSet<string> WeaponSkillNames =
    [
        "Pistol", "Revolver", "Assault", "Shotgun", "Sniper", "DMR", "Melee"
    ];

    // Vanilla skill IDs from SkillTypes enum (used for modded detection)
    private static readonly HashSet<string> VanillaSkillIds =
    [
        "Endurance", "Strength", "Vitality", "Health", "StressResistance",
        "Metabolism", "Immunity", "Perception", "Intellect", "Attention",
        "Charisma", "Memory", "MagDrills", "Pistol", "Revolver", "SMG",
        "Assault", "Shotgun", "Sniper", "LMG", "HMG", "Launcher",
        "AttachedLauncher", "Throwing", "Misc", "Melee", "DMR",
        "DrawMaster", "AimMaster", "RecoilControl", "TroubleShooting",
        "Sniping", "CovertMovement", "ProneMovement", "FirstAid",
        "FieldMedicine", "Surgery", "LightVests", "HeavyVests",
        "WeaponModding", "AdvancedModding", "NightOps", "SilentOps",
        "Lockpicking", "Search", "WeaponTreatment", "Freetrading",
        "Auctions", "Cleanoperations", "Barter", "Shadowconnections",
        "Taskperformance", "BearAssaultoperations", "BearAuthority",
        "BearAksystems", "BearHeavycaliber", "BearRawpower",
        "UsecArsystems", "UsecDeepweaponmodding", "UsecLongrangeoptics",
        "UsecNegotiations", "UsecTactics", "BotReload", "BotSound",
        "AimDrills", "HideoutManagement", "Crafting"
    ];

    // Human-readable display names
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["Endurance"] = "Endurance",
        ["Strength"] = "Strength",
        ["Vitality"] = "Vitality",
        ["Health"] = "Health",
        ["Metabolism"] = "Metabolism",
        ["StressResistance"] = "Stress Resistance",
        ["Immunity"] = "Immunity",
        ["Perception"] = "Perception",
        ["Intellect"] = "Intellect",
        ["Attention"] = "Attention",
        ["Charisma"] = "Charisma",
        ["Memory"] = "Memory",
        ["Surgery"] = "Surgery",
        ["CovertMovement"] = "Covert Movement",
        ["Search"] = "Search",
        ["MagDrills"] = "Mag Drills",
        ["HideoutManagement"] = "Hideout Management",
        ["Crafting"] = "Crafting",
        ["Throwing"] = "Throwing",
        ["RecoilControl"] = "Recoil Control",
        ["WeaponTreatment"] = "Weapon Maintenance",
        ["AimDrills"] = "Aiming",
        ["TroubleShooting"] = "Troubleshooting",
        ["LightVests"] = "Light Vests",
        ["HeavyVests"] = "Heavy Vests",
        ["Pistol"] = "Pistol",
        ["Revolver"] = "Revolver",
        ["Assault"] = "Assault",
        ["Shotgun"] = "Shotgun",
        ["Sniper"] = "Sniper",
        ["DMR"] = "DMR",
        ["Melee"] = "Melee"
    };

    // XP per level constant (used for level calculation)
    private const double SkillExpPerLevel = 100.0;

    // ═══════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var globals = databaseService.GetGlobals();
        var ss = globals.Configuration.SkillsSettings;

        // Snapshot all double properties from typed skill sub-objects
        foreach (var skillName in TypedSkillNames.Concat(WeaponSkillNames))
        {
            var skillObj = GetSkillObject(ss, skillName);
            if (skillObj == null) continue;

            foreach (var prop in GetDoubleProperties(skillObj))
            {
                var key = $"{skillName}.{prop.Name}";
                var val = (double)(prop.GetValue(skillObj) ?? 0);
                _bonusSnapshots[key] = val;
            }
        }

        _snapshotTaken = true;
        logger.Info($"[ZSlayerHQ] Skills: Snapshotted {_bonusSnapshots.Count} bonus fields across {TypedSkillNames.Count + WeaponSkillNames.Count} skills");
    }

    // ═══════════════════════════════════════════════════════════════
    //  SKILL LIST (for multiplier grid)
    // ═══════════════════════════════════════════════════════════════

    public SkillListResponse GetSkillList()
    {
        var config = configService.GetConfig().Progression;
        var perSkill = config.Skills.PerSkillMultipliers;
        var globalMult = config.Skills.GlobalSkillSpeedMultiplier;

        var skills = new List<SkillInfo>();

        // Typed skills
        foreach (var name in TypedSkillNames.Order())
        {
            var perMult = perSkill.GetValueOrDefault(name, 1.0);
            skills.Add(new SkillInfo
            {
                Name = DisplayNames.GetValueOrDefault(name, name),
                InternalName = name,
                EffectiveMultiplier = globalMult * perMult,
                HasOverride = Math.Abs(perMult - 1.0) > 0.001,
                IsWeapon = false,
                IsModded = false
            });
        }

        // Weapon skills
        foreach (var name in WeaponSkillNames.Order())
        {
            var perMult = perSkill.GetValueOrDefault(name, 1.0);
            skills.Add(new SkillInfo
            {
                Name = DisplayNames.GetValueOrDefault(name, name),
                InternalName = name,
                EffectiveMultiplier = globalMult * perMult,
                HasOverride = Math.Abs(perMult - 1.0) > 0.001,
                IsWeapon = true,
                IsModded = false
            });
        }

        // Detect modded skills from profiles
        var moddedSkillNames = DetectModdedSkills();
        foreach (var name in moddedSkillNames)
        {
            var perMult = perSkill.GetValueOrDefault(name, 1.0);
            skills.Add(new SkillInfo
            {
                Name = name,
                InternalName = name,
                EffectiveMultiplier = globalMult * perMult,
                HasOverride = Math.Abs(perMult - 1.0) > 0.001,
                IsWeapon = false,
                IsModded = true
            });
        }

        return new SkillListResponse
        {
            Skills = skills,
            GlobalMultiplier = globalMult,
            WeaponMultiplier = config.Skills.GlobalSkillSpeedMultiplier, // same global for now
            FatigueMultiplier = config.Skills.SkillFatigueMultiplier
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  SKILL BONUS EDITING
    // ═══════════════════════════════════════════════════════════════

    public SkillBonusResponse GetAllBonusConfigs()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var globals = databaseService.GetGlobals();
            var ss = globals.Configuration.SkillsSettings;
            var result = new SkillBonusResponse();

            foreach (var skillName in TypedSkillNames.Concat(WeaponSkillNames).Order())
            {
                var skillObj = GetSkillObject(ss, skillName);
                if (skillObj == null) continue;

                var config = new SkillBonusConfig { SkillName = skillName };

                foreach (var prop in GetDoubleProperties(skillObj))
                {
                    var key = $"{skillName}.{prop.Name}";
                    var currentVal = (double)(prop.GetValue(skillObj) ?? 0);
                    var defaultVal = _bonusSnapshots.GetValueOrDefault(key, currentVal);

                    config.Fields.Add(new SkillBonusField
                    {
                        FieldName = prop.Name,
                        DisplayName = HumanizeFieldName(prop.Name),
                        DefaultValue = defaultVal,
                        CurrentValue = currentVal
                    });
                }

                if (config.Fields.Count > 0)
                    result.Skills.Add(config);
            }

            return result;
        }
    }

    public SkillBonusConfig? GetBonusConfig(string skillName)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var globals = databaseService.GetGlobals();
            var ss = globals.Configuration.SkillsSettings;

            var skillObj = GetSkillObject(ss, skillName);
            if (skillObj == null) return null;

            var config = new SkillBonusConfig { SkillName = skillName };

            foreach (var prop in GetDoubleProperties(skillObj))
            {
                var key = $"{skillName}.{prop.Name}";
                var currentVal = (double)(prop.GetValue(skillObj) ?? 0);
                var defaultVal = _bonusSnapshots.GetValueOrDefault(key, currentVal);

                config.Fields.Add(new SkillBonusField
                {
                    FieldName = prop.Name,
                    DisplayName = HumanizeFieldName(prop.Name),
                    DefaultValue = defaultVal,
                    CurrentValue = currentVal
                });
            }

            return config;
        }
    }

    public bool UpdateBonusValues(SkillBonusUpdateRequest request)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var globals = databaseService.GetGlobals();
            var ss = globals.Configuration.SkillsSettings;

            var skillObj = GetSkillObject(ss, request.SkillName);
            if (skillObj == null) return false;

            var type = skillObj.GetType();
            int updated = 0;

            foreach (var (fieldName, value) in request.Fields)
            {
                var prop = type.GetProperty(fieldName);
                if (prop == null || prop.PropertyType != typeof(double)) continue;

                prop.SetValue(skillObj, value);
                updated++;
            }

            logger.Info($"[ZSlayerHQ] Skills: Updated {updated} bonus fields for {request.SkillName}");
            return updated > 0;
        }
    }

    public int ResetBonusValues(string? skillName = null)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var globals = databaseService.GetGlobals();
            var ss = globals.Configuration.SkillsSettings;
            int restored = 0;

            var targets = skillName != null
                ? [skillName]
                : TypedSkillNames.Concat(WeaponSkillNames).ToArray();

            foreach (var name in targets)
            {
                var skillObj = GetSkillObject(ss, name);
                if (skillObj == null) continue;

                foreach (var prop in GetDoubleProperties(skillObj))
                {
                    var key = $"{name}.{prop.Name}";
                    if (_bonusSnapshots.TryGetValue(key, out var origVal))
                    {
                        prop.SetValue(skillObj, origVal);
                        restored++;
                    }
                }
            }

            logger.Info($"[ZSlayerHQ] Skills: Reset {restored} bonus fields" + (skillName != null ? $" for {skillName}" : ""));
            return restored;
        }
    }

    public Dictionary<string, double> GetDefaults()
    {
        EnsureSnapshot();
        return new Dictionary<string, double>(_bonusSnapshots);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PER-PLAYER SKILL EDITING
    // ═══════════════════════════════════════════════════════════════

    public PlayerSkillsResponse? GetPlayerSkills(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return null;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Skills?.Common == null) return null;

        var entries = new List<PlayerSkillEntry>();
        foreach (var skill in pmc.Skills.Common)
        {
            var id = skill.Id.ToString();
            var progress = skill.Progress;
            var level = (int)(progress / SkillExpPerLevel);
            entries.Add(new PlayerSkillEntry
            {
                SkillId = id,
                DisplayName = DisplayNames.GetValueOrDefault(id, id),
                Progress = progress,
                Level = Math.Min(51, level),
                IsElite = level >= 51,
                IsModded = !VanillaSkillIds.Contains(id)
            });
        }

        return new PlayerSkillsResponse
        {
            SessionId = sessionId,
            Nickname = pmc.Info?.Nickname ?? "Unknown",
            Skills = entries.OrderBy(s => s.IsModded).ThenBy(s => s.DisplayName).ToList()
        };
    }

    public bool SetPlayerSkills(string sessionId, PlayerSkillUpdateRequest request)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return false;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Skills?.Common == null) return false;

        var skillMap = new Dictionary<string, PlayerSkillUpdate>();
        foreach (var update in request.Skills)
            skillMap[update.SkillId] = update;

        int updated = 0;
        foreach (var skill in pmc.Skills.Common)
        {
            var id = skill.Id.ToString();
            if (!skillMap.TryGetValue(id, out var update)) continue;

            var targetLevel = Math.Clamp(update.Level, 0, 51);
            skill.Progress = targetLevel * SkillExpPerLevel;
            updated++;
        }

        logger.Info($"[ZSlayerHQ] Skills: Updated {updated} skill levels for player {sessionId}");
        return updated > 0;
    }

    public int SetAllPlayerSkills(string sessionId, int level)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return 0;

        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Skills?.Common == null) return 0;

        var targetLevel = Math.Clamp(level, 0, 51);
        int count = 0;
        foreach (var skill in pmc.Skills.Common)
        {
            skill.Progress = targetLevel * SkillExpPerLevel;
            count++;
        }

        logger.Info($"[ZSlayerHQ] Skills: Set all {count} skills to level {targetLevel} for player {sessionId}");
        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  MODDED SKILL DETECTION
    // ═══════════════════════════════════════════════════════════════

    private HashSet<string> DetectModdedSkills()
    {
        var modded = new HashSet<string>();
        var profiles = saveServer.GetProfiles();

        foreach (var (_, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Skills?.Common == null) continue;

            foreach (var skill in pmc.Skills.Common)
            {
                var id = skill.Id.ToString();
                if (!VanillaSkillIds.Contains(id) && !modded.Contains(id))
                    modded.Add(id);
            }
        }

        return modded;
    }

    // ═══════════════════════════════════════════════════════════════
    //  REFLECTION HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static object? GetSkillObject(object skillsSettings, string skillName)
    {
        var prop = skillsSettings.GetType().GetProperty(skillName);
        return prop?.GetValue(skillsSettings);
    }

    private static IEnumerable<PropertyInfo> GetDoubleProperties(object obj)
    {
        return obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(double) && p.CanRead && p.CanWrite)
            .OrderBy(p => p.Name);
    }

    private static string HumanizeFieldName(string name)
    {
        // Insert spaces before capitals: "MovementAction" → "Movement Action"
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                result.Append(' ');
            result.Append(name[i]);
        }
        return result.ToString();
    }
}
