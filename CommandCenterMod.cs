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

        // Build service status lines for the bottom section
        var statusLines = new List<string>();
        statusLines.Add("Config loaded");
        if (headlessLogService.IsConfigured)
            statusLines.Add($"Headless logs: {headlessLogService.BasePath}");
        var hlStatusInfo = headlessProcessService.GetStatus();
        if (hlStatusInfo.Available)
            statusLines.Add($"Headless EXE: {hlStatusInfo.ExePath}");
        if (offerRegenerationService.SnapshotSummary != null)
            statusLines.Add($"Flea: {offerRegenerationService.SnapshotSummary}");
        if (offerRegenerationService.AppliedSummary != null)
            statusLines.Add($"Flea: {offerRegenerationService.AppliedSummary}");

        // Determine box inner width from ALL content lines
        var allContent = new List<string> { title, subtitle, footer, quote };
        allContent.AddRange(urlLines);
        if (headlessLine != null) allContent.Add(headlessLine);
        if (noPublicLine != null) allContent.Add(noPublicLine);
        allContent.AddRange(statusLines.Select(s => "   " + s));
        var innerWidth = allContent.Max(s => s.Length) + 6;

        string Center(string text) =>
            text.PadLeft((innerWidth + text.Length) / 2).PadRight(innerWidth);
        string LeftAlign(string text) =>
            ("   " + text).PadRight(innerWidth);

        var bar = new string('═', innerWidth);

        // Print box
        logger.Info($"{gold}╔{bar}╗{reset}");
        logger.Info($"{gold}║{reset}{cyan}{Center(title)}{reset}{gold}║{reset}");
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
        if (statusLines.Count > 0)
        {
            logger.Info($"{gold}╠{bar}╣{reset}");
            foreach (var line in statusLines)
                logger.Info($"{gold}║{reset}{dim}{LeftAlign(line)}{reset}{gold}║{reset}");
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
