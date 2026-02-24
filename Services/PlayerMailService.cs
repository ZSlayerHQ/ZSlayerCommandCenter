using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class PlayerMailService(
    MailSendService mailSendService,
    DatabaseService databaseService,
    ItemHelper itemHelper,
    ItemGiveService itemGiveService,
    SaveServer saveServer,
    ActivityLogService activityLogService,
    ISptLogger<PlayerMailService> logger)
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string DollarsTpl = "5696686a4bdc2da3298b456a";
    private const string EurosTpl = "569668774bdc2da2298b4568";

    public PlayerActionResponse SendMail(string adminSessionId, string recipientSessionId, PlayerMailRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return new PlayerActionResponse { Success = false, Error = "Message is required" };

            var itemsToSend = BuildItemList(request.Items, request.Roubles, request.Dollars, request.Euros);

            if (itemsToSend.Count > 0)
                itemHelper.SetFoundInRaid(itemsToSend);

            mailSendService.SendSystemMessageToPlayer(
                recipientSessionId,
                $"[Admin] {request.Message}",
                itemsToSend.Count > 0 ? itemsToSend : null);

            var recipientName = GetProfileName(recipientSessionId);
            var details = $"Mail to {recipientName}: \"{request.Message}\"";
            if (itemsToSend.Count > 0)
                details += $" + {itemsToSend.Count} item(s)";

            activityLogService.LogAction(ActionType.PlayerMail, adminSessionId, details);

            return new PlayerActionResponse
            {
                Success = true,
                Message = $"Mail sent to {recipientName}"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error sending mail to {recipientSessionId}: {ex.Message}");
            return new PlayerActionResponse { Success = false, Error = ex.Message };
        }
    }

    public PlayerActionResponse BroadcastMail(string adminSessionId, BroadcastMailRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return new PlayerActionResponse { Success = false, Error = "Message is required" };

            var profiles = saveServer.GetProfiles();
            var sent = 0;

            foreach (var (sid, _) in profiles)
            {
                try
                {
                    var itemsToSend = BuildItemList(request.Items, request.Roubles, request.Dollars, request.Euros);
                    if (itemsToSend.Count > 0)
                        itemHelper.SetFoundInRaid(itemsToSend);

                    mailSendService.SendSystemMessageToPlayer(
                        sid.ToString(),
                        $"[Broadcast] {request.Message}",
                        itemsToSend.Count > 0 ? itemsToSend : null);
                    sent++;
                }
                catch (Exception ex)
                {
                    logger.Debug($"ZSlayerCommandCenter: Failed broadcast send to session {sid}: {ex.Message}");
                }
            }

            activityLogService.LogAction(ActionType.BroadcastMail, adminSessionId,
                $"Broadcast to {sent} players: \"{request.Message}\"");

            return new PlayerActionResponse
            {
                Success = true,
                Message = $"Broadcast sent to {sent} players"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error broadcasting mail: {ex.Message}");
            return new PlayerActionResponse { Success = false, Error = ex.Message };
        }
    }

    public PlayerActionResponse GiveToPlayer(string adminSessionId, string targetSessionId, List<GiveRequestItem> items)
    {
        try
        {
            if (items.Count == 0)
                return new PlayerActionResponse { Success = false, Error = "No items specified" };

            var result = itemGiveService.GiveItems(targetSessionId, items);
            if (!result.Success)
                return new PlayerActionResponse { Success = false, Error = result.Error };

            var targetName = GetProfileName(targetSessionId);
            activityLogService.LogAction(ActionType.PlayerGive, adminSessionId,
                $"Gave {result.ItemsGiven} items to {targetName}");

            return new PlayerActionResponse
            {
                Success = true,
                Message = $"Gave {result.ItemsGiven} items to {targetName}"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error giving items to {targetSessionId}: {ex.Message}");
            return new PlayerActionResponse { Success = false, Error = ex.Message };
        }
    }

    public PlayerActionResponse GiveToAll(string adminSessionId, List<GiveRequestItem> items)
    {
        try
        {
            if (items.Count == 0)
                return new PlayerActionResponse { Success = false, Error = "No items specified" };

            var profiles = saveServer.GetProfiles();
            var sent = 0;

            foreach (var (sid, _) in profiles)
            {
                try
                {
                    var result = itemGiveService.GiveItems(sid.ToString(), items);
                    if (result.Success) sent++;
                }
                catch (Exception ex)
                {
                    logger.Debug($"ZSlayerCommandCenter: Failed give-all send to session {sid}: {ex.Message}");
                }
            }

            activityLogService.LogAction(ActionType.PlayerGiveAll, adminSessionId,
                $"Gave items to {sent} players");

            return new PlayerActionResponse
            {
                Success = true,
                Message = $"Items sent to {sent} players"
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Error giving items to all: {ex.Message}");
            return new PlayerActionResponse { Success = false, Error = ex.Message };
        }
    }

    private List<Item> BuildItemList(List<GiveRequestItem>? requestItems, int roubles, int dollars, int euros)
    {
        var items = new List<Item>();
        var itemDb = databaseService.GetItems();

        // Add requested items
        if (requestItems != null)
        {
            foreach (var req in requestItems)
            {
                if (!itemDb.TryGetValue(req.Tpl, out var template))
                    continue;

                var stackMax = template.Properties?.StackMaxSize ?? 1;
                var remaining = req.Count;

                if (stackMax <= 1)
                {
                    for (var i = 0; i < remaining; i++)
                    {
                        items.Add(new Item
                        {
                            Id = new MongoId(),
                            Template = req.Tpl,
                            Upd = new Upd { StackObjectsCount = 1 }
                        });
                    }
                }
                else
                {
                    while (remaining > 0)
                    {
                        var stackSize = Math.Min(remaining, stackMax);
                        items.Add(new Item
                        {
                            Id = new MongoId(),
                            Template = req.Tpl,
                            Upd = new Upd { StackObjectsCount = stackSize }
                        });
                        remaining -= stackSize;
                    }
                }
            }
        }

        // Add currency
        if (roubles > 0)
            items.Add(new Item { Id = new MongoId(), Template = RoublesTpl, Upd = new Upd { StackObjectsCount = roubles } });
        if (dollars > 0)
            items.Add(new Item { Id = new MongoId(), Template = DollarsTpl, Upd = new Upd { StackObjectsCount = dollars } });
        if (euros > 0)
            items.Add(new Item { Id = new MongoId(), Template = EurosTpl, Upd = new Upd { StackObjectsCount = euros } });

        return items;
    }

    private string GetProfileName(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (profiles.TryGetValue(sessionId, out var profile))
            return profile.CharacterData?.PmcData?.Info?.Nickname ?? "Unknown";
        return "Unknown";
    }
}
