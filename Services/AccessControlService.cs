using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class AccessControlService(
    ConfigService configService,
    SaveServer saveServer,
    ISptLogger<AccessControlService> logger)
{
    public bool IsAuthorized(string sessionId)
    {
        // Check ban list first — banned players are always denied
        if (IsBanned(sessionId))
        {
            logger.Warning($"ZSlayerCommandCenter: Banned session denied access: {sessionId}");
            return false;
        }

        var profiles = saveServer.GetProfiles();
        if (!profiles.ContainsKey(sessionId))
        {
            logger.Warning($"ZSlayerCommandCenter: Unknown session ID: {sessionId}");
            return false;
        }

        var config = configService.GetConfig().Access;

        if (config.Mode == "whitelist")
        {
            if (config.Whitelist.Count == 0 && config.AllowAllWhenEmpty)
                return true;

            return config.Whitelist.Contains(sessionId);
        }

        if (config.Mode == "blacklist")
        {
            return !config.Blacklist.Contains(sessionId);
        }

        return true;
    }

    public string GetProfileName(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile))
            return "Unknown";

        return profile.CharacterData?.PmcData?.Info?.Nickname ?? "Unknown";
    }

    public bool IsBanned(string sessionId)
    {
        var config = configService.GetConfig().Access;
        return config.BanList.Any(b => b.SessionId == sessionId);
    }

    public void BanPlayer(string sessionId, string reason)
    {
        var config = configService.GetConfig();

        // Don't add duplicate bans
        if (config.Access.BanList.Any(b => b.SessionId == sessionId))
            return;

        config.Access.BanList.Add(new BanEntryConfig
        {
            SessionId = sessionId,
            Reason = reason,
            BannedAt = DateTime.UtcNow
        });

        configService.SaveConfig();
        logger.Info($"ZSlayerCommandCenter: Banned player {sessionId}: {reason}");
    }

    public void UnbanPlayer(string sessionId)
    {
        var config = configService.GetConfig();
        var removed = config.Access.BanList.RemoveAll(b => b.SessionId == sessionId);

        if (removed > 0)
        {
            configService.SaveConfig();
            logger.Info($"ZSlayerCommandCenter: Unbanned player {sessionId}");
        }
    }

    public BanListResponse GetBanList()
    {
        var config = configService.GetConfig().Access;
        var entries = config.BanList.Select(b => new BanEntry
        {
            SessionId = b.SessionId,
            Nickname = GetProfileName(b.SessionId),
            Reason = b.Reason,
            BannedAt = b.BannedAt
        }).ToList();

        return new BanListResponse { Entries = entries };
    }
}
