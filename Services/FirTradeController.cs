using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

/// <summary>
/// Extends SPT's TradeController to enforce FIR requirements, sell penalties, and purchase limits.
/// Registered via TypeOverride so it replaces TradeController in the DI container.
/// </summary>
[Injectable(InjectionType.Singleton, typeOverride: typeof(TradeController))]
public class FirTradeController(
    ISptLogger<TradeController> logger,
    DatabaseService databaseService,
    EventOutputHolder eventOutputHolder,
    TradeHelper tradeHelper,
    TimeUtil timeUtil,
    RandomUtil randomUtil,
    ItemHelper itemHelper,
    RagfairOfferHelper ragfairOfferHelper,
    RagfairServer ragfairServer,
    HttpResponseUtil httpResponseUtil,
    ServerLocalisationService serverLocalisationService,
    MailSendService mailSendService,
    ConfigServer configServer,
    ConfigService configService)
    : TradeController(logger, databaseService, eventOutputHolder, tradeHelper, timeUtil, randomUtil,
        itemHelper, ragfairOfferHelper, ragfairServer, httpResponseUtil, serverLocalisationService,
        mailSendService, configServer)
{
    public override ItemEventRouterResponse ConfirmTrading(PmcData pmcData, ProcessBaseTradeRequestData request, MongoId sessionID)
    {
        var fir = configService.GetConfig().Fir;

        // ── Barter FIR validation ──
        if (fir.BarterItemsMustBeFir && request.Type == "buy_from_trader")
        {
            var buyRequest = (ProcessBuyTradeRequestData)request;
            if (buyRequest.SchemeItems != null)
            {
                foreach (var schemeItem in buyRequest.SchemeItems)
                {
                    var invItem = pmcData.Inventory.Items?.FirstOrDefault(i => i.Id == schemeItem.Id);
                    if (invItem == null) continue;

                    // Currency items don't need FIR
                    if (TraderDiscoveryService.IsCurrencyTemplate(invItem.Template.ToString())) continue;

                    if (invItem.Upd?.SpawnedInSession != true)
                    {
                        var output = eventOutputHolder.GetOutput(sessionID);
                        return httpResponseUtil.AppendErrorToOutput(output,
                            "Barter items must be Found in Raid",
                            BackendErrorCodes.UnknownTradingError);
                    }
                }
            }
        }

        // ── Sell-to-trader FIR validation ──
        if (request.Type == "sell_to_trader")
        {
            var sellRequest = (ProcessSellTradeRequestData)request;

            // Block non-FIR sells entirely if enabled
            if (fir.SellToTraderRequiresFir && sellRequest.Items != null)
            {
                foreach (var soldItem in sellRequest.Items)
                {
                    var invItem = pmcData.Inventory.Items?.FirstOrDefault(i => i.Id == soldItem.Id);
                    if (invItem == null) continue;

                    if (invItem.Upd?.SpawnedInSession != true)
                    {
                        var output = eventOutputHolder.GetOutput(sessionID);
                        return httpResponseUtil.AppendErrorToOutput(output,
                            "You can only sell Found in Raid items to traders",
                            BackendErrorCodes.UnknownTradingError);
                    }
                }
            }

            // Apply non-FIR sell penalty (reduce price for non-FIR items)
            if (fir.NonFirSellPenaltyPercent > 0 && !fir.SellToTraderRequiresFir
                && sellRequest.Price != null && sellRequest.Items != null)
            {
                var hasNonFir = sellRequest.Items.Any(soldItem =>
                {
                    var invItem = pmcData.Inventory.Items?.FirstOrDefault(i => i.Id == soldItem.Id);
                    return invItem != null && invItem.Upd?.SpawnedInSession != true;
                });

                if (hasNonFir)
                {
                    var penalty = Math.Clamp(fir.NonFirSellPenaltyPercent, 0, 100);
                    var multiplier = (100.0 - penalty) / 100.0;
                    sellRequest.Price = Math.Max(1.0, Math.Round(sellRequest.Price.Value * multiplier));
                }
            }
        }

        return base.ConfirmTrading(pmcData, request, sessionID);
    }
}
