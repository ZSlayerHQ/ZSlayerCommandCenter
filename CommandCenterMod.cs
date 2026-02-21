using System.Net;
using System.Net.Sockets;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;
using ZSlayerCommandCenter.Services;

namespace ZSlayerCommandCenter;

[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]
public class CommandCenterMod(
    ConfigService configService,
    ConsoleBufferService consoleBufferService,
    HeadlessLogService headlessLogService,
    HeadlessProcessService headlessProcessService,
    ActivityLogService activityLogService,
    OfferRegenerationService offerRegenerationService,
    ISptLogger<CommandCenterMod> logger) : IOnLoad
{
    /// <summary>Detected server URLs, available for mail/API use.</summary>
    public static string ServerUrls { get; private set; } = "";

    private static readonly string[] StartupQuotes =
    [
        "It's dangerous to go alone! Take this Toz.",
        "I used to be a PMC, then I took a buckshot to the knee.",
        "Would you kindly... adjust the XP multiplier?",
        "Snake? Snake?! SNAAAAKE! ...oh wait, wrong game.",
        "Remember: no Russian. Unless it's Prapor.",
        "The Labs weren't built in a day. But this mod was.",
        "Thank you PMC! But your loot is in another stash.",
        "Kept you waiting, huh? — Peacekeeper, probably",
        "Do you get to the Labs very often? Oh, what am I saying, of course you don't.",
        "A man chooses, a Scav obeys.",
        "The right man in the wrong raid can make all the difference.",
        "Protocol 16-23: Always check your corners. And your server config.",
        "Mission failed. We'll get 'em next wipe.",
        "Had to be me. Someone else might have gotten the loot wrong.",
        "Ah shit, here we go again.",
        "I need a weapon. — Master Chief, browsing the Items tab",
        "Boy! ...bring me my Red Rebel. — Kratos, probably",
        "Nothing is true, everything is permitted. Especially with admin tools.",
        "Praise the sun! And the XP multiplier.",
        "You died. But at least your server is configured properly.",
        "Rise and shine, Mr. Admin. Rise and... shine.",
        "The Scavs are coming. And they're not happy about the loot tables."
    ];

    public Task OnLoad()
    {
        configService.LoadConfig();
        var config = configService.GetConfig();

        // Configure and install console interceptor
        consoleBufferService.Configure(config.Dashboard.ConsoleBufferSize);
        consoleBufferService.InstallConsoleInterceptor();

        // Configure headless log reader + process manager
        headlessLogService.Configure(config.Dashboard.HeadlessLogPath, configService.ModPath);
        headlessProcessService.Configure(configService.ModPath);

        // Apply flea market settings (globals, prices, tax) on startup
        offerRegenerationService.ApplyGlobalsAndConfig();

        // Clean up old activity logs
        activityLogService.CleanupOldLogs();
        activityLogService.LogAction(ActionType.ServerStart, "", "Server started");

        // Detect IPs and log startup banner
        var lanIp = GetLanIp();
        var publicIp = GetPublicIp();
        PrintStartupBanner(lanIp, publicIp);

        // Store for mail/API use
        var mailLines = new List<string> { "ZSlayer Command Center URLs:" };
        mailLines.Add($"  Local: https://127.0.0.1:6969/zslayer/cc/");
        if (lanIp != null) mailLines.Add($"  LAN: https://{lanIp}:6969/zslayer/cc/");
        if (publicIp != null) mailLines.Add($"  Public: https://{publicIp}:6969/zslayer/cc/");
        else mailLines.Add("  Public IP unknown — ask the server host for the public IP.");
        ServerUrls = string.Join("\n", mailLines);

        // Start headless auto-start timer (if configured)
        headlessProcessService.StartAutoStartTimer();

        return Task.CompletedTask;
    }

    private void PrintStartupBanner(string? lanIp, string? publicIp)
    {
        const string cyan = "\x1b[96m";
        const string gold = "\x1b[93m";
        const string dim = "\x1b[90m";
        const string reset = "\x1b[0m";

        var title = $"⚡ ZSlayerHQ Command Center v{ModMetadata.StaticVersion} ⚡";
        var subtitle = "Open in your browser to manage your server:";
        var footer = "Share the LAN or Public URL with players for remote access";
        var quote = $"\"{StartupQuotes[Random.Shared.Next(StartupQuotes.Length)]}\"";

        var urlLines = new List<string>
        {
            $"Local:   https://127.0.0.1:6969/zslayer/cc/"
        };
        if (lanIp != null) urlLines.Add($"LAN:     https://{lanIp}:6969/zslayer/cc/");
        if (publicIp != null) urlLines.Add($"Public:  https://{publicIp}:6969/zslayer/cc/");

        // Build headless info line (if available)
        string? headlessLine = null;
        var headlessStatus = headlessProcessService.GetStatus();
        if (headlessStatus.Available)
        {
            var hlConfig = configService.GetConfig().Headless;
            if (hlConfig.AutoStart && !string.IsNullOrEmpty(hlConfig.ProfileId))
                headlessLine = $"Headless client will auto-start in {hlConfig.AutoStartDelaySec}s";
            else if (string.IsNullOrEmpty(hlConfig.ProfileId))
                headlessLine = "Headless client detected — set Profile ID to enable";
            else
                headlessLine = "Headless client detected — auto-start is disabled";
        }

        string? noPublicLine = publicIp == null
            ? "Public IP not detected — Google 'what is my IP' to find it"
            : null;

        // Determine box inner width from ALL content lines
        var allContent = new List<string> { title, subtitle, footer, quote };
        allContent.AddRange(urlLines);
        if (headlessLine != null) allContent.Add(headlessLine);
        if (noPublicLine != null) allContent.Add(noPublicLine);

        // Include flea table lines in width calc so box auto-sizes
        var fleaPreview = offerRegenerationService.StartupDisplay;
        if (fleaPreview is { HasModifiers: true, ExampleBasePrice: > 0 })
        {
            // Check widest rows: effective multiplier + price, and header + example name
            var effLabel = fleaPreview.ExampleMultSource == "global" ? "" : $" ({fleaPreview.ExampleMultSource})";
            var effLeft = $"   Effective:         {fleaPreview.ExampleEffectiveMult:F2}x{effLabel}";
            var effRight = $"  Base: {fleaPreview.ExampleBasePrice:N0}  →  Mod: {fleaPreview.ExampleModifiedPrice:N0}";
            allContent.Add(effLeft.PadRight(31) + "│" + effRight);
            var headerRight = $"  Example: {fleaPreview.ExampleName}";
            allContent.Add("   Changes".PadRight(31) + "│" + headerRight);
        }

        var innerWidth = allContent.Max(s => s.Length) + 6;

        string Center(string text) =>
            text.PadLeft((innerWidth + text.Length) / 2).PadRight(innerWidth);
        string LeftAlign(string text) =>
            ("   " + text).PadRight(innerWidth);

        var bar = new string('═', innerWidth);

        // Print box
        logger.Info($"{gold}╔{bar}╗{reset}");
        // ⚡ emojis render as 2 columns each in console but count as 1 in string.Length — subtract 2
        var titlePad = Center(title);
        titlePad = titlePad.Length >= 2 ? titlePad[..^2] : titlePad;
        logger.Info($"{gold}║{reset}{cyan}{titlePad}{reset}{gold}║{reset}");
        logger.Info($"{gold}╠{bar}╣{reset}");
        logger.Info($"{gold}║{reset}{Center("")}{gold}║{reset}");
        logger.Info($"{gold}║{reset}{Center(subtitle)}{gold}║{reset}");
        logger.Info($"{gold}║{reset}{Center("")}{gold}║{reset}");

        foreach (var url in urlLines)
            logger.Info($"{gold}║{reset}{cyan}{Center(url)}{reset}{gold}║{reset}");

        logger.Info($"{gold}║{reset}{Center("")}{gold}║{reset}");

        if (noPublicLine != null)
        {
            logger.Info($"{gold}║{reset}\x1b[33m{Center(noPublicLine)}{reset}{gold}║{reset}");
            logger.Info($"{gold}║{reset}{Center("")}{gold}║{reset}");
        }

        logger.Info($"{gold}║{reset}{Center(footer)}{gold}║{reset}");
        logger.Info($"{gold}║{reset}{Center("")}{gold}║{reset}");

        if (headlessLine != null)
        {
            var centered = Center(headlessLine);
            // Color the seconds value red in the auto-start line
            var hlConfig2 = configService.GetConfig().Headless;
            if (hlConfig2.AutoStart && !string.IsNullOrEmpty(hlConfig2.ProfileId))
            {
                var secText = $"{hlConfig2.AutoStartDelaySec}s";
                const string red = "\x1b[91m";
                centered = centered.Replace(secText, $"{red}{secText}{cyan}");
            }
            logger.Info($"{gold}║{reset}{cyan}{centered}{reset}{gold}║{reset}");
            logger.Info($"{gold}║{reset}{Center("")}{gold}║{reset}");
        }

        logger.Info($"{gold}╠{bar}╣{reset}");
        logger.Info($"{gold}║{reset}{dim}{Center(quote)}{reset}{gold}║{reset}");

        // ── Helpers for status & flea sections ──
        const string green = "\x1b[92m";
        const string white = "\x1b[97m";

        void BlankLine() =>
            logger.Info($"{gold}║{reset}{new string(' ', innerWidth)}{gold}║{reset}");

        // Print a left-aligned line (plain for width calc, colored for display)
        void StatusLine(string plainText, string coloredText)
        {
            var pad = Math.Max(0, innerWidth - 3 - plainText.Length);
            logger.Info($"{gold}║{reset}   {coloredText}{new string(' ', pad)}{reset}{gold}║{reset}");
        }

        // Two-column table row — separator │ at fixed position
        var sepPos = Math.Min(31, innerWidth - 12);
        void TableRow(string leftPlain, string leftColored, string rightPlain = "", string rightColored = "")
        {
            var lp = leftPlain.PadRight(sepPos);
            var rp = rightPlain.Length > 0 ? "  " + rightPlain : "";
            var totalPad = Math.Max(0, innerWidth - lp.Length - 1 - rp.Length);
            var leftPadCount = Math.Max(0, sepPos - leftPlain.Length);
            var rc = rightColored.Length > 0 ? "  " + rightColored : "";
            logger.Info($"{gold}║{reset}{leftColored}{new string(' ', leftPadCount)}{dim}│{reset}{rc}{new string(' ', totalPad)}{reset}{gold}║{reset}");
        }

        void TableSep()
        {
            var rightDash = Math.Max(0, innerWidth - sepPos - 1);
            logger.Info($"{gold}║{reset}{dim}{new string('─', sepPos)}┼{new string('─', rightDash)}{reset}{gold}║{reset}");
        }

        string TruncatePath(string path, int maxLen)
        {
            if (path.Length <= maxLen) return path;
            return "..." + path[^(maxLen - 3)..];
        }

        // ═══ Config status line ═══
        logger.Info($"{gold}╠{bar}╣{reset}");
        BlankLine();

        var hlStatusInfo = headlessProcessService.GetStatus();
        var plainParts = new List<string> { "Config loaded" };
        var colorParts = new List<string> { $"{green}Config {white}loaded" };
        if (hlStatusInfo.Available)
        {
            plainParts.Add("Headless Client EXE found");
            colorParts.Add($"{green}Headless Client EXE {white}found");
        }
        // Center using plain text length for padding
        var configPlain = string.Join("  |  ", plainParts);
        var configColored = string.Join($"  {dim}│{reset}  ", colorParts);
        var configLeftPad = (innerWidth - configPlain.Length) / 2;
        var configRightPad = innerWidth - configPlain.Length - configLeftPad;
        logger.Info($"{gold}║{reset}{new string(' ', configLeftPad)}{configColored}{new string(' ', configRightPad)}{reset}{gold}║{reset}");

        BlankLine();

        // ═══ Flea Modifiers Section ═══
        var fd = offerRegenerationService.StartupDisplay;
        if (fd is { HasModifiers: true })
        {
            logger.Info($"{gold}╠{bar}╣{reset}");
            BlankLine();

            // Title + underline (centered)
            var fleaTitle = "Flea Modifiers Applied";
            var fleaTitleLeftPad = (innerWidth - fleaTitle.Length) / 2;
            var fleaTitleRightPad = innerWidth - fleaTitle.Length - fleaTitleLeftPad;
            logger.Info($"{gold}║{reset}{new string(' ', fleaTitleLeftPad)}{green}{fleaTitle}{reset}{new string(' ', fleaTitleRightPad)}{gold}║{reset}");
            var ul = new string('─', fleaTitle.Length);
            logger.Info($"{gold}║{reset}{new string(' ', fleaTitleLeftPad)}{dim}{ul}{reset}{new string(' ', fleaTitleRightPad)}{gold}║{reset}");
            BlankLine();

            // Table header
            var maxRight = innerWidth - sepPos - 3;
            var exLabel = fd.ExampleName.Length > 0
                ? (fd.ExampleName.Length > maxRight - 10
                    ? "Example: " + fd.ExampleName[..(maxRight - 13)] + "..."
                    : "Example: " + fd.ExampleName)
                : "";
            TableRow(
                "   Changes", $"   {cyan}Changes{reset}",
                exLabel, exLabel.Length > 0 ? $"{cyan}{exLabel}{reset}" : "");
            TableSep();

            // Global multiplier
            TableRow(
                $"   Global Multiplier: {fd.BuyMultiplier:F2}x",
                $"   {white}Global Multiplier: {cyan}{fd.BuyMultiplier:F2}x{reset}");

            // Categories (show count + stacking note)
            if (fd.CategoryCount > 0)
                TableRow(
                    $"   + Categories:      {fd.CategoryCount} (stacks)",
                    $"   {white}+ Categories:      {cyan}{fd.CategoryCount}{dim} (stacks){reset}");

            // Effective multiplier + price example on right
            if (fd.ExampleBasePrice > 0)
            {
                var effLabel = fd.ExampleMultSource == "global" ? "" : $" ({fd.ExampleMultSource})";
                TableRow(
                    $"   Effective:         {fd.ExampleEffectiveMult:F2}x{effLabel}",
                    $"   {white}Effective:         {green}{fd.ExampleEffectiveMult:F2}x{dim}{effLabel}{reset}",
                    $"Base: {fd.ExampleBasePrice:N0}  →  Mod: {fd.ExampleModifiedPrice:N0}",
                    $"{dim}Base: {white}{fd.ExampleBasePrice:N0}  {dim}→  {cyan}Mod: {fd.ExampleModifiedPrice:N0}{reset}");
            }

            // Tax multiplier
            if (fd.ExampleBaseTax > 0)
                TableRow(
                    $"   Tax Multiplier:    {fd.TaxMultiplier:F2}x",
                    $"   {white}Tax Multiplier:    {cyan}{fd.TaxMultiplier:F2}x{reset}",
                    $"Tax:  {fd.ExampleBaseTax:N0}  →  Mod: {fd.ExampleModifiedTax:N0}",
                    $"{dim}Tax:  {white}{fd.ExampleBaseTax:N0}  {dim}→  {cyan}Mod: {fd.ExampleModifiedTax:N0}{reset}");
            else
                TableRow(
                    $"   Tax Multiplier:    {fd.TaxMultiplier:F2}x",
                    $"   {white}Tax Multiplier:    {cyan}{fd.TaxMultiplier:F2}x{reset}");

            // Remaining settings
            TableRow($"   Max Player Offers: {fd.MaxOffers}", $"   {white}Max Player Offers: {cyan}{fd.MaxOffers}{reset}");
            TableRow($"   Listing Duration:  {fd.DurationHours}h", $"   {white}Listing Duration:  {cyan}{fd.DurationHours}h{reset}");
            TableRow($"   Barter Offers:     {fd.BarterPercent}%", $"   {white}Barter Offers:     {cyan}{fd.BarterPercent}%{reset}");
            TableRow($"   Prices Modified:   {fd.ModifiedPrices}/{fd.TotalPrices}",
                     $"   {white}Prices Modified:   {cyan}{fd.ModifiedPrices}/{fd.TotalPrices}{reset}");

            BlankLine();
        }
        else if (fd != null)
        {
            logger.Info($"{gold}╠{bar}╣{reset}");
            BlankLine();
            StatusLine("Flea: Default settings (no modifiers)", $"{dim}Flea: Default settings (no modifiers)");
            BlankLine();
        }

        logger.Info($"{gold}╚{bar}╝{reset}");
    }

    private static string? GetLanIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch { return null; }
    }

    private static string? GetPublicIp()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            return client.GetStringAsync("https://api.ipify.org").Result.Trim();
        }
        catch { return null; }
    }
}
