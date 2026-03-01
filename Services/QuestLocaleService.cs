using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class QuestLocaleService(
    LocaleService localeService,
    ISptLogger<QuestLocaleService> logger)
{
    /// <summary>
    /// Restore all locale entries from snapshots to original values.
    /// </summary>
    public void RestoreLocales(Dictionary<string, QuestSnapshot> snapshots)
    {
        var locale = localeService.GetLocaleDb("en");
        var restored = 0;

        foreach (var (_, snapshot) in snapshots)
        {
            foreach (var (key, originalValue) in snapshot.LocaleEntries)
            {
                locale[key] = originalValue;
                restored++;
            }
        }

        if (restored > 0)
            logger.Info($"[ZSlayerHQ] Quests: Restored {restored} locale entries from snapshots");
    }

    /// <summary>
    /// Apply locale overrides from quest config.
    /// 1. Explicit locale overrides from config (highest priority)
    /// 2. Auto-update objective text when values change (best-effort)
    /// </summary>
    public int ApplyLocaleOverrides(QuestEditorConfig config, Dictionary<string, QuestSnapshot> snapshots)
    {
        var locale = localeService.GetLocaleDb("en");
        var modified = 0;

        foreach (var (questId, questOverride) in config.QuestOverrides)
        {
            if (!snapshots.TryGetValue(questId, out var snapshot)) continue;

            // 1. Explicit locale overrides (highest priority)
            foreach (var (key, text) in questOverride.LocaleOverrides)
            {
                locale[key] = text;
                modified++;
            }

            // 2. Auto-update condition text when values change
            foreach (var (conditionId, condOverride) in questOverride.ConditionOverrides)
            {
                // Skip if there's an explicit locale override for this condition
                if (questOverride.LocaleOverrides.ContainsKey(conditionId))
                    continue;

                if (condOverride.ValueOverride == null) continue;
                if (!snapshot.Conditions.TryGetValue(conditionId, out var condSnap)) continue;
                if (condSnap.Value == null) continue;

                var originalValue = (int)condSnap.Value.Value;
                var newValue = (int)condOverride.ValueOverride.Value;

                if (originalValue == newValue) continue;

                // Try to auto-update the locale string
                if (snapshot.LocaleEntries.TryGetValue(conditionId, out var originalText) && !string.IsNullOrEmpty(originalText))
                {
                    var updated = AutoUpdateLocaleNumber(originalText, originalValue, newValue);
                    if (updated != originalText)
                    {
                        locale[conditionId] = updated;
                        modified++;
                    }
                }
            }
        }

        return modified;
    }

    /// <summary>
    /// Best-effort replacement of a number in locale text.
    /// Replaces first occurrence of originalValue with newValue.
    /// Example: "Kill 15 Scavs on Customs" → "Kill 5 Scavs on Customs"
    /// </summary>
    private static string AutoUpdateLocaleNumber(string text, int originalValue, int newValue)
    {
        // Try to find and replace the exact number
        // Use word boundary regex to avoid partial matches (e.g., "15" in "150")
        var pattern = $@"\b{originalValue}\b";
        var replaced = Regex.Replace(text, pattern, newValue.ToString(), RegexOptions.None, TimeSpan.FromMilliseconds(100));
        return replaced;
    }
}
