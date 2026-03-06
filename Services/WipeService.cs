using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class WipeService(
    SaveServer saveServer,
    ProfileBackupService backupService,
    ConfigService configService,
    ISptLogger<WipeService> logger)
{
    private string ProfilesDir
    {
        get
        {
            var modPath = configService.ModPath;
            var sptRoot = Path.GetFullPath(Path.Combine(modPath, "..", "..", ".."));
            return Path.Combine(sptRoot, "user", "profiles");
        }
    }

    public (bool success, string message) FullWipe(string confirmText)
    {
        if (!confirmText.Equals("WIPE", StringComparison.Ordinal))
            return (false, "Confirmation text must be exactly 'WIPE'");

        // Mandatory backup before wipe
        try
        {
            backupService.CreateProfileBackup("Pre-wipe safety backup");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to create pre-wipe backup: {ex.Message}");
        }

        var profileFiles = Directory.GetFiles(ProfilesDir, "*.json");
        var wipedCount = 0;

        foreach (var file in profileFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonNode.Parse(json);
                if (doc == null) continue;

                var pmc = doc["characters"]?["pmc"];
                if (pmc == null) continue;

                // Preserve account info
                var info = pmc["Info"];
                var nickname = info?["Nickname"]?.GetValue<string>() ?? "";
                var side = info?["Side"]?.GetValue<string>() ?? "Usec";

                // Reset level/XP
                if (info != null)
                {
                    info["Level"] = 1;
                    info["Experience"] = 0;
                }

                // Clear inventory items (keep only equipment/quest containers)
                ResetInventory(pmc);

                // Reset skills
                ResetSkills(pmc);

                // Reset quests
                pmc["Quests"] = new JsonArray();

                // Reset trader standings
                ResetTraders(pmc);

                // Reset hideout
                ResetHideout(pmc);

                File.WriteAllText(file, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                wipedCount++;
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Failed to wipe profile {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        logger.Warning($"[ZSlayerHQ] FULL SERVER WIPE completed — {wipedCount} profiles reset");
        return (true, $"Full wipe complete. {wipedCount} profiles reset. Server restart required.");
    }

    public (bool success, string message) SelectiveWipe(List<string> categories, string confirmText)
    {
        if (!confirmText.Equals("WIPE", StringComparison.Ordinal))
            return (false, "Confirmation text must be exactly 'WIPE'");

        if (categories.Count == 0)
            return (false, "No categories selected");

        // Mandatory backup before wipe
        try
        {
            backupService.CreateProfileBackup("Pre-selective-wipe safety backup");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to create pre-wipe backup: {ex.Message}");
        }

        var profileFiles = Directory.GetFiles(ProfilesDir, "*.json");
        var wipedCount = 0;

        foreach (var file in profileFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonNode.Parse(json);
                if (doc == null) continue;

                var pmc = doc["characters"]?["pmc"];
                if (pmc == null) continue;

                foreach (var cat in categories)
                {
                    switch (cat.ToLower())
                    {
                        case "inventory": ResetInventory(pmc); break;
                        case "skills": ResetSkills(pmc); break;
                        case "quests": pmc["Quests"] = new JsonArray(); break;
                        case "traders": ResetTraders(pmc); break;
                        case "hideout": ResetHideout(pmc); break;
                        case "money": ResetMoney(pmc); break;
                    }
                }

                File.WriteAllText(file, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                wipedCount++;
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Failed to selective wipe {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        logger.Warning($"[ZSlayerHQ] SELECTIVE WIPE completed — {wipedCount} profiles, categories: {string.Join(", ", categories)}");
        return (true, $"Selective wipe complete. {wipedCount} profiles affected ({string.Join(", ", categories)}). Server restart required.");
    }

    private static void ResetInventory(JsonNode pmc)
    {
        var items = pmc["Inventory"]?["items"]?.AsArray();
        if (items == null) return;

        // Keep equipment container, stash container, quest raid items container, sorting table
        var equipment = pmc["Inventory"]?["equipment"]?.GetValue<string>() ?? "";
        var stash = pmc["Inventory"]?["stash"]?.GetValue<string>() ?? "";
        var questRaidItems = pmc["Inventory"]?["questRaidItems"]?.GetValue<string>() ?? "";
        var questStashItems = pmc["Inventory"]?["questStashItems"]?.GetValue<string>() ?? "";
        var sortingTable = pmc["Inventory"]?["sortingTable"]?.GetValue<string>() ?? "";

        var keepIds = new HashSet<string> { equipment, stash, questRaidItems, questStashItems, sortingTable };
        keepIds.Remove(""); // remove empty strings

        var toRemove = new List<JsonNode>();
        foreach (var item in items)
        {
            var id = item?["_id"]?.GetValue<string>() ?? "";
            if (!keepIds.Contains(id))
                toRemove.Add(item!);
        }
        foreach (var item in toRemove)
            items.Remove(item);
    }

    private static void ResetSkills(JsonNode pmc)
    {
        var skills = pmc["Skills"]?["Common"]?.AsArray();
        if (skills == null) return;
        foreach (var skill in skills)
        {
            if (skill == null) continue;
            skill["Progress"] = 0;
        }
    }

    private static void ResetTraders(JsonNode pmc)
    {
        var traders = pmc["TradersInfo"]?.AsObject();
        if (traders == null) return;
        foreach (var kvp in traders)
        {
            var trader = kvp.Value;
            if (trader == null) continue;
            trader["loyaltyLevel"] = 1;
            trader["salesSum"] = 0;
            trader["standing"] = 0;
        }
    }

    private static void ResetHideout(JsonNode pmc)
    {
        var areas = pmc["Hideout"]?["Areas"]?.AsArray();
        if (areas == null) return;
        foreach (var area in areas)
        {
            if (area == null) continue;
            area["level"] = 0;
            area["constructing"] = false;
        }
    }

    private static void ResetMoney(JsonNode pmc)
    {
        var items = pmc["Inventory"]?["items"]?.AsArray();
        if (items == null) return;

        var moneyTpls = new HashSet<string>
        {
            "5449016a4bdc2d6f028b456f", // Roubles
            "5696686a4bdc2da3298b456a", // Dollars
            "569668774bdc2da2298b4568"  // Euros
        };

        var toRemove = new List<JsonNode>();
        foreach (var item in items)
        {
            var tpl = item?["_tpl"]?.GetValue<string>() ?? "";
            if (moneyTpls.Contains(tpl))
                toRemove.Add(item!);
        }
        foreach (var item in toRemove)
            items.Remove(item);
    }
}
