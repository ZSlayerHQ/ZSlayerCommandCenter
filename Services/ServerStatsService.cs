using System.Diagnostics;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ServerStatsService(
    Watermark watermark,
    LauncherController launcherController)
{
    private readonly DateTime _startTime = DateTime.UtcNow;

    public ServerStatusDto GetStatus()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var mods = launcherController.GetLoadedServerMods();

        var modList = mods.Select(kvp => new ModInfoDto
        {
            Name = kvp.Value.Name ?? kvp.Key,
            Version = kvp.Value.Version?.ToString() ?? "?",
            Author = kvp.Value.Author ?? ""
        }).OrderBy(m => m.Name).ToList();

        var process = Process.GetCurrentProcess();

        return new ServerStatusDto
        {
            Uptime = FormatUptime(uptime),
            UptimeSeconds = (long)uptime.TotalSeconds,
            SptVersion = watermark.GetVersionTag(),
            CcVersion = ModMetadata.StaticVersion,
            ModCount = modList.Count,
            Mods = modList,
            MemoryMb = GC.GetTotalMemory(false) / (1024 * 1024),
            WorkingSetMb = process.WorkingSet64 / (1024 * 1024)
        };
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
    }
}
