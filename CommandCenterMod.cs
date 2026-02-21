using System.Net;
using System.Net.Sockets;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
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
    ConfigServer configServer,
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

        // Detect bound IP and build URLs based on bind mode
        var httpCfg = configServer.GetConfig<HttpConfig>();
        var boundIp = httpCfg.Ip;
        var port = httpCfg.Port;
        string MakeUrl(string ip) => $"https://{ip}:{port}/zslayer/cc/";

        var urlLines = new List<string>();
        var mailLines = new List<string> { "ZSlayer Command Center URLs:" };
        string? publicIp = null;
        string footer;

        if (boundIp == "0.0.0.0")
        {
            // Default bind — accepts connections from anywhere
            var lanIp = GetLanIp();
            publicIp = GetPublicIp();
            urlLines.Add($"Local:   {MakeUrl("127.0.0.1")}");
            mailLines.Add($"  Local: {MakeUrl("127.0.0.1")}");
            if (lanIp != null) { urlLines.Add($"LAN:     {MakeUrl(lanIp)}"); mailLines.Add($"  LAN: {MakeUrl(lanIp)}"); }
            if (publicIp != null) { urlLines.Add($"Public:  {MakeUrl(publicIp)}"); mailLines.Add($"  Public: {MakeUrl(publicIp)}"); }
            else mailLines.Add("  Public IP unknown — ask the server host for the public IP.");
            footer = "Share the LAN or Public URL with players to access the Command Center";
        }
        else if (IsPrivateIp(boundIp))
        {
            // Bound to a LAN IP — loopback won't work
            publicIp = GetPublicIp();
            urlLines.Add($"LAN:     {MakeUrl(boundIp)}");
            mailLines.Add($"  LAN: {MakeUrl(boundIp)}");
            if (publicIp != null) { urlLines.Add($"Public:  {MakeUrl(publicIp)}"); mailLines.Add($"  Public: {MakeUrl(publicIp)}"); }
            else mailLines.Add("  Public IP unknown — ask the server host for the public IP.");
            footer = "Share the Public URL with remote players, or use the LAN URL on your local network";
        }
        else if (boundIp == "127.0.0.1")
        {
            // Localhost only
            urlLines.Add($"Local:   {MakeUrl("127.0.0.1")}");
            mailLines.Add($"  Local: {MakeUrl("127.0.0.1")}");
            footer = "Server bound to localhost — only accessible from this machine";
        }
        else
        {
            // Specific IP (VPN, etc.)
            urlLines.Add($"Connect: {MakeUrl(boundIp)}");
            mailLines.Add($"  Connect: {MakeUrl(boundIp)}");
            footer = "Share this URL with players connected to the same network";
        }

        PrintStartupBanner(urlLines, publicIp, footer);
        ServerUrls = string.Join("\n", mailLines);

        // Start headless auto-start timer (if configured)
        headlessProcessService.StartAutoStartTimer();

        return Task.CompletedTask;
    }

    private void PrintStartupBanner(List<string> urlLines, string? publicIp, string footer)
    {
        const string cyan = "\x1b[96m";
        const string gold = "\x1b[93m";
        const string dim = "\x1b[90m";
        const string reset = "\x1b[0m";
        const string green = "\x1b[92m";
        const string white = "\x1b[97m";

        var title = $"⚡ ZSlayerHQ Command Center v{ModMetadata.StaticVersion} ⚡";
        var subtitle = "Open in your browser to manage your server:";
        var quote = $"\"{StartupQuotes[Random.Shared.Next(StartupQuotes.Length)]}\"";

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

        // Pre-compute flea table layout for width calculation and rendering
        var fd = offerRegenerationService.StartupDisplay;
        var fleaTableWidthLines = new List<string>();
        var fleaDataRows = new List<(string LP, string LC, string RP, string RC)>();
        var fleaSepPos = 0;
        string fleaHdrRP = "", fleaHdrRC = "";

        if (fd is { HasModifiers: true })
        {
            // Buy price: single combined line with category breakdown if applicable
            string buyVP, buyVC;
            if (fd.ExampleMultSource == "global×category" && fd.ExampleBasePrice > 0)
            {
                var catMult = fd.ExampleEffectiveMult / fd.BuyMultiplier;
                buyVP = $"{fd.ExampleEffectiveMult:F2}x ({fd.BuyMultiplier:F2}x global × {catMult:F2}x category)";
                buyVC = $"{green}{fd.ExampleEffectiveMult:F2}x {dim}({white}{fd.BuyMultiplier:F2}x {dim}global × {white}{catMult:F2}x {dim}category){reset}";
            }
            else if (fd.ExampleMultSource == "item" && fd.ExampleBasePrice > 0)
            {
                buyVP = $"{fd.ExampleEffectiveMult:F2}x (per-item override)";
                buyVC = $"{green}{fd.ExampleEffectiveMult:F2}x {dim}(per-item override){reset}";
            }
            else
            {
                buyVP = $"{fd.BuyMultiplier:F2}x";
                buyVC = $"{cyan}{fd.BuyMultiplier:F2}x{reset}";
            }

            // Right-side price/tax examples with aligned arrows
            string prRP = "", prRC = "", txRP = "", txRC = "";
            if (fd.ExampleBasePrice > 0)
            {
                var bpStr = $"{fd.ExampleBasePrice:N0}";
                var btStr = fd.ExampleBaseTax > 0 ? $"{fd.ExampleBaseTax:N0}" : "";
                var maxBW = Math.Max(bpStr.Length, btStr.Length);
                prRP = $"₽ {bpStr.PadRight(maxBW)}  →  ₽ {fd.ExampleModifiedPrice:N0}";
                prRC = $"{white}₽ {bpStr.PadRight(maxBW)}  {dim}→  {cyan}₽ {fd.ExampleModifiedPrice:N0}{reset}";
                if (fd.ExampleBaseTax > 0)
                {
                    txRP = $"₽ {btStr.PadRight(maxBW)}  →  ₽ {fd.ExampleModifiedTax:N0}";
                    txRC = $"{white}₽ {btStr.PadRight(maxBW)}  {dim}→  {cyan}₽ {fd.ExampleModifiedTax:N0}{reset}";
                }
            }

            // Example header
            if (fd.ExampleName.Length > 0)
            {
                fleaHdrRP = $"Example: {fd.ExampleName}";
                fleaHdrRC = $"{cyan}Example: {fd.ExampleName}{reset}";
            }

            // Row data: label, value plain/colored, right plain/colored
            var rows = new (string Lbl, string VP, string VC, string RP, string RC)[]
            {
                ("Buy Price:", buyVP, buyVC, prRP, prRC),
                ("Tax Multiplier:", $"{fd.TaxMultiplier:F2}x", $"{cyan}{fd.TaxMultiplier:F2}x{reset}", txRP, txRC),
                ("Max Player Offers:", $"{fd.MaxOffers}", $"{cyan}{fd.MaxOffers}{reset}", "", ""),
                ("Listing Duration:", $"{fd.DurationHours}h", $"{cyan}{fd.DurationHours}h{reset}", "", ""),
                ("Barter Offers:", $"{fd.BarterPercent}%", $"{cyan}{fd.BarterPercent}%{reset}", "", ""),
                ("Prices Modified:", $"{fd.ModifiedPrices:N0}/{fd.TotalPrices:N0}", $"{cyan}{fd.ModifiedPrices:N0}/{fd.TotalPrices:N0}{reset}", "", ""),
            };

            const int lblW = 19; // "Max Player Offers:" is longest
            var maxVLen = rows.Max(r => r.VP.Length);
            fleaSepPos = 3 + lblW + 1 + maxVLen + 2;

            foreach (var (lbl, vp, vc, rp, rc) in rows)
            {
                var lp = $"   {lbl.PadRight(lblW)} {vp}";
                var lc = $"   {white}{lbl.PadRight(lblW)}{reset} {vc}";
                fleaDataRows.Add((lp, lc, rp, rc));
            }

            // Plain text lines for box width calculation
            var hdr = "   Changes".PadRight(fleaSepPos) + "│" + (fleaHdrRP.Length > 0 ? $"  {fleaHdrRP}  " : "  ");
            fleaTableWidthLines.Add(hdr);
            foreach (var (lp, _, rp, _) in fleaDataRows)
            {
                var line = lp.PadRight(fleaSepPos) + "│" + (rp.Length > 0 ? $"  {rp}  " : "  ");
                fleaTableWidthLines.Add(line);
            }
        }

        // Determine box inner width from ALL content lines
        var allContent = new List<string> { title, subtitle, footer, quote };
        allContent.AddRange(urlLines);
        if (headlessLine != null) allContent.Add(headlessLine);
        if (noPublicLine != null) allContent.Add(noPublicLine);
        allContent.AddRange(fleaTableWidthLines);

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

        var maxUrlLen = urlLines.Max(u => u.Length);
        var urlLeftPad = (innerWidth - maxUrlLen) / 2;
        foreach (var url in urlLines)
        {
            var urlRightPad = innerWidth - urlLeftPad - url.Length;
            logger.Info($"{gold}║{reset}{cyan}{new string(' ', urlLeftPad)}{url}{new string(' ', Math.Max(0, urlRightPad))}{reset}{gold}║{reset}");
        }

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

        // ── Helpers ──
        void BlankLine() =>
            logger.Info($"{gold}║{reset}{new string(' ', innerWidth)}{gold}║{reset}");

        void StatusLine(string plainText, string coloredText)
        {
            var pad = Math.Max(0, innerWidth - 3 - plainText.Length);
            logger.Info($"{gold}║{reset}   {coloredText}{new string(' ', pad)}{reset}{gold}║{reset}");
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
        if (fd is { HasModifiers: true })
        {
            logger.Info($"{gold}╠{bar}╣{reset}");
            BlankLine();

            // Title + underline (centered)
            var fleaTitle = "Flea Modifiers Applied";
            var ftlp = (innerWidth - fleaTitle.Length) / 2;
            var ftrp = innerWidth - fleaTitle.Length - ftlp;
            logger.Info($"{gold}║{reset}{new string(' ', ftlp)}{green}{fleaTitle}{reset}{new string(' ', ftrp)}{gold}║{reset}");
            logger.Info($"{gold}║{reset}{new string(' ', ftlp)}{dim}{new string('─', fleaTitle.Length)}{reset}{new string(' ', ftrp)}{gold}║{reset}");
            BlankLine();

            // Table header
            var hLeftPad = Math.Max(0, fleaSepPos - "   Changes".Length);
            var hRP = fleaHdrRP.Length > 0 ? "  " + fleaHdrRP : "";
            var hRC = fleaHdrRC.Length > 0 ? "  " + fleaHdrRC : "";
            var hRightPad = Math.Max(0, innerWidth - fleaSepPos - 1 - hRP.Length);
            logger.Info($"{gold}║{reset}   {cyan}Changes{reset}{new string(' ', hLeftPad)}{dim}│{reset}{hRC}{new string(' ', hRightPad)}{gold}║{reset}");

            // Separator
            var rightDash = Math.Max(0, innerWidth - fleaSepPos - 1);
            logger.Info($"{gold}║{reset}{dim}{new string('─', fleaSepPos)}┼{new string('─', rightDash)}{reset}{gold}║{reset}");

            // Data rows
            foreach (var (lp, lc, rp, rc) in fleaDataRows)
            {
                var leftPad = Math.Max(0, fleaSepPos - lp.Length);
                var rPlain = rp.Length > 0 ? "  " + rp : "";
                var rColor = rc.Length > 0 ? "  " + rc : "";
                var rightPad = Math.Max(0, innerWidth - fleaSepPos - 1 - rPlain.Length);
                logger.Info($"{gold}║{reset}{lc}{new string(' ', leftPad)}{dim}│{reset}{rColor}{new string(' ', rightPad)}{gold}║{reset}");
            }

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

    private static bool IsPrivateIp(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return false;
        var bytes = addr.GetAddressBytes();
        if (bytes.Length != 4) return false;
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
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
