using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class TraderPriceService(
    DatabaseService databaseService,
    TraderDiscoveryService discoveryService,
    ISptLogger<TraderPriceService> logger)
{
    /// <summary>
    /// Apply buy price multipliers to all traders' barter schemes.
    /// Only modifies currency-based costs (not barter items).
    /// Assumes data has already been restored from snapshot.
    /// </summary>
    public int ApplyBuyMultipliers(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return 0;

        var modifiedCount = 0;

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Assort?.BarterScheme == null) continue;
            var id = traderId.ToString();

            var hasTraderOverride = false;
            var traderBuyMult = config.GlobalBuyMultiplier;
            TraderOverride? traderOverride = null;
            if (config.TraderOverrides.TryGetValue(id, out var ov) && ov.Enabled)
            {
                traderOverride = ov;
                hasTraderOverride = true;
                traderBuyMult = ov.BuyMultiplier; // Override replaces global
            }

            foreach (var (itemId, paymentOptions) in trader.Assort.BarterScheme)
            {
                var itemIdStr = itemId.ToString();

                // Check per-item override (replaces trader-level multiplier)
                var effectiveMult = traderBuyMult;
                if (traderOverride?.ItemOverrides.TryGetValue(itemIdStr, out var itemOv) == true)
                    effectiveMult = itemOv.BuyMultiplier;
                else
                {
                    var rootItem = trader.Assort.Items?.FirstOrDefault(i => i.Id.ToString() == itemIdStr);
                    if (rootItem != null)
                    {
                        var tplStr = rootItem.Template.ToString();
                        if (traderOverride?.ItemOverrides.TryGetValue(tplStr, out var tplOv) == true)
                            effectiveMult = tplOv.BuyMultiplier;
                    }
                }
                if (Math.Abs(effectiveMult - 1.0) < 0.001) continue;

                foreach (var paymentOption in paymentOptions)
                {
                    foreach (var cost in paymentOption)
                    {
                        if (cost.Count == null) continue;
                        var tpl = cost.Template.ToString();

                        // Only multiply currency items
                        if (!TraderDiscoveryService.IsCurrencyTemplate(tpl)) continue;

                        var newCount = Math.Max(1.0, Math.Round(cost.Count.Value * effectiveMult));

                        // Apply min/max price clamping (convert to roubles for comparison)
                        var rateToRoubles = TraderDiscoveryService.GetExchangeRateToRoubles(tpl);
                        var priceInRoubles = newCount * rateToRoubles;
                        if (priceInRoubles < config.MinPriceRoubles)
                            newCount = Math.Max(1.0, Math.Ceiling(config.MinPriceRoubles / rateToRoubles));
                        else if (priceInRoubles > config.MaxPriceRoubles)
                            newCount = Math.Floor(config.MaxPriceRoubles / rateToRoubles);

                        cost.Count = newCount;
                        modifiedCount++;
                    }
                }
            }
        }

        return modifiedCount;
    }

    /// <summary>
    /// Apply sell multipliers by modifying BuyPriceCoefficient on trader loyalty levels.
    /// A higher coef means the player gets MORE money when selling.
    /// Assumes loyalty level coefs have been restored from snapshot.
    /// </summary>
    public void ApplySellMultipliers(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return;

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Base?.LoyaltyLevels == null) continue;
            var id = traderId.ToString();
            var snapshot = discoveryService.GetSnapshot(id);
            if (snapshot == null) continue;

            var effectiveSellMult = config.GlobalSellMultiplier;
            if (config.TraderOverrides.TryGetValue(id, out var ov) && ov.Enabled)
                effectiveSellMult = ov.SellMultiplier; // Override replaces global
            if (Math.Abs(effectiveSellMult - 1.0) < 0.001) continue;

            for (var i = 0; i < trader.Base.LoyaltyLevels.Count && i < snapshot.BuyPriceCoefs.Count; i++)
            {
                var originalCoef = snapshot.BuyPriceCoefs[i];
                // BuyPriceCoefficient is a reduction factor: sell price = basePrice * (1 - coef/100)
                // e.g. coef=52 → player gets 48% of base. To increase sell value, DIVIDE the coef.
                // coef=52 / 2.0 = 26 → player gets 74% of base (higher sell mult = more money)
                var newCoef = originalCoef / effectiveSellMult;
                trader.Base.LoyaltyLevels[i].BuyPriceCoefficient = Math.Max(0.01, Math.Round(newCoef, 2));
            }
        }
    }

    /// <summary>
    /// Apply currency override — replace currency templates in barter schemes.
    /// Converts amounts using exchange rates.
    /// </summary>
    public void ApplyCurrencyOverride(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return;

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Assort?.BarterScheme == null) continue;
            var id = traderId.ToString();

            // Determine target currency: per-trader override > global > null (no change)
            string? targetCurrency = null;
            if (config.TraderOverrides.TryGetValue(id, out var ov) && ov.Enabled && ov.ForceCurrency != null)
                targetCurrency = ov.ForceCurrency;
            else if (config.ForceCurrency != null)
                targetCurrency = config.ForceCurrency;

            if (targetCurrency == null) continue;

            var targetTpl = TraderDiscoveryService.CurrencyToTemplateId(targetCurrency);

            foreach (var (_, paymentOptions) in trader.Assort.BarterScheme)
            {
                foreach (var paymentOption in paymentOptions)
                {
                    foreach (var cost in paymentOption)
                    {
                        if (cost.Count == null) continue;
                        var sourceTpl = cost.Template.ToString();

                        // Only convert currency items, skip if already target currency
                        if (!TraderDiscoveryService.IsCurrencyTemplate(sourceTpl)) continue;
                        if (sourceTpl == targetTpl) continue;

                        // Convert: source amount → roubles → target currency
                        var sourceRate = TraderDiscoveryService.GetExchangeRateToRoubles(sourceTpl);
                        var targetRate = TraderDiscoveryService.GetExchangeRateToRoubles(targetTpl);
                        var amountInRoubles = cost.Count.Value * sourceRate;
                        var newAmount = Math.Max(1.0, Math.Round(amountInRoubles / targetRate));

                        cost.Template = targetTpl;
                        cost.Count = newAmount;
                    }
                }
            }

            // Also update the trader's base currency
            if (Enum.TryParse<CurrencyType>(targetCurrency, out var currencyEnum))
                trader.Base.Currency = currencyEnum;
        }
    }
}
