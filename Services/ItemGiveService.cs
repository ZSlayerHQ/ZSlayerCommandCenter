using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ItemGiveService(
    DatabaseService databaseService,
    MailSendService mailSendService,
    ItemHelper itemHelper,
    ConfigService configService,
    ActivityLogService activityLogService,
    ISptLogger<ItemGiveService> logger)
{
    public GiveResponse GiveItems(string sessionId, List<GiveRequestItem> requestItems)
    {
        var items = databaseService.GetItems();
        var itemsToSend = new List<Item>();
        var failedItems = new List<string>();
        var totalGiven = 0;

        foreach (var req in requestItems)
        {
            if (!items.TryGetValue(req.Tpl, out var template))
            {
                logger.Warning($"ZSlayerCommandCenter: Template not found: {req.Tpl}");
                failedItems.Add(req.Tpl);
                continue;
            }

            var stackMax = template.Properties?.StackMaxSize ?? 1;
            var remaining = req.Count;

            if (stackMax <= 1)
            {
                // Non-stackable: create individual instances
                for (var i = 0; i < remaining; i++)
                {
                    itemsToSend.Add(new Item
                    {
                        Id = new MongoId(),
                        Template = req.Tpl,
                        Upd = new Upd { StackObjectsCount = 1 }
                    });
                }
            }
            else
            {
                // Stackable: split into full stacks
                while (remaining > 0)
                {
                    var stackSize = Math.Min(remaining, stackMax);
                    itemsToSend.Add(new Item
                    {
                        Id = new MongoId(),
                        Template = req.Tpl,
                        Upd = new Upd { StackObjectsCount = stackSize }
                    });
                    remaining -= stackSize;
                }
            }

            totalGiven += req.Count;
        }

        if (itemsToSend.Count == 0)
        {
            return new GiveResponse
            {
                Success = false,
                ItemsGiven = 0,
                Error = "No valid items to send",
                FailedItems = failedItems
            };
        }

        itemHelper.SetFoundInRaid(itemsToSend);
        mailSendService.SendSystemMessageToPlayer(sessionId, "ZSlayer Command Center", itemsToSend);

        var config = configService.GetConfig();
        if (config.Logging.LogGiveEvents)
        {
            logger.Info($"ZSlayerCommandCenter: Sent {totalGiven} items ({itemsToSend.Count} stacks) to session {sessionId}");
        }

        activityLogService.LogAction(Models.ActionType.ItemGive, sessionId, $"{totalGiven} items ({itemsToSend.Count} stacks)");

        return new GiveResponse
        {
            Success = true,
            ItemsGiven = totalGiven,
            FailedItems = failedItems
        };
    }

    public PresetGiveResponse GivePreset(string sessionId, string presetId)
    {
        var config = configService.GetConfig();
        var preset = config.Items.Presets.FirstOrDefault(p => p.Id == presetId);

        if (preset is null)
        {
            return new PresetGiveResponse
            {
                Success = false,
                Error = $"Preset not found: {presetId}"
            };
        }

        var requestItems = preset.Items.Select(i => new GiveRequestItem
        {
            Tpl = i.Tpl,
            Count = i.Count
        }).ToList();

        var result = GiveItems(sessionId, requestItems);

        if (result.Success)
            activityLogService.LogAction(Models.ActionType.PresetGive, sessionId, preset.Name);

        return new PresetGiveResponse
        {
            Success = result.Success,
            PresetName = preset.Name,
            ItemsGiven = result.ItemsGiven,
            Error = result.Error
        };
    }
}
