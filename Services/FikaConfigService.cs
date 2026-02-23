using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class FikaConfigService(
    ConfigService configService,
    ISptLogger<FikaConfigService> logger)
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private string FikaConfigPath => Path.GetFullPath(
        Path.Combine(configService.ModPath, "..", "fika-server", "assets", "configs", "fika.jsonc"));

    public bool IsAvailable => File.Exists(FikaConfigPath);

    public FikaConfigDto GetFikaSettings()
    {
        if (!IsAvailable)
            return new FikaConfigDto { Available = false };

        try
        {
            var json = File.ReadAllText(FikaConfigPath);
            var root = JsonNode.Parse(json);
            if (root is not JsonObject obj)
                return new FikaConfigDto { Available = false };

            return new FikaConfigDto
            {
                Available = true,
                // headless
                RestartAfterAmountOfRaids = obj["headless"]?["restartAfterAmountOfRaids"]?.GetValue<int>() ?? 0,
                SetLevelToAverageOfLobby = obj["headless"]?["setLevelToAverageOfLobby"]?.GetValue<bool>() ?? false,
                // client
                FriendlyFire = obj["client"]?["friendlyFire"]?.GetValue<bool>() ?? true,
                UseInertia = obj["client"]?["useInertia"]?.GetValue<bool>() ?? true,
                SharedQuestProgression = obj["client"]?["sharedQuestProgression"]?.GetValue<bool>() ?? false,
                DynamicVExfils = obj["client"]?["dynamicVExfils"]?.GetValue<bool>() ?? false,
                EnableTransits = obj["client"]?["enableTransits"]?.GetValue<bool>() ?? true,
                AnyoneCanStartRaid = obj["client"]?["anyoneCanStartRaid"]?.GetValue<bool>() ?? false,
                // server
                SessionTimeout = obj["server"]?["sessionTimeout"]?.GetValue<int>() ?? 5,
                AllowItemSending = obj["server"]?["allowItemSending"]?.GetValue<bool>() ?? true,
                SentItemsLoseFIR = obj["server"]?["sentItemsLoseFIR"]?.GetValue<bool>() ?? true,
                LauncherListAllProfiles = obj["server"]?["launcherListAllProfiles"]?.GetValue<bool>() ?? false
            };
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Failed to read fika.jsonc: {ex.Message}");
            return new FikaConfigDto { Available = false };
        }
    }

    public FikaConfigDto UpdateFikaSettings(FikaConfigDto dto)
    {
        if (!IsAvailable)
            return new FikaConfigDto { Available = false };

        try
        {
            var json = File.ReadAllText(FikaConfigPath);
            var root = JsonNode.Parse(json);
            if (root is not JsonObject obj)
                return new FikaConfigDto { Available = false };

            // Ensure sections exist
            obj["headless"] ??= new JsonObject();
            obj["client"] ??= new JsonObject();
            obj["server"] ??= new JsonObject();

            // Headless
            obj["headless"]!["restartAfterAmountOfRaids"] = dto.RestartAfterAmountOfRaids;
            obj["headless"]!["setLevelToAverageOfLobby"] = dto.SetLevelToAverageOfLobby;

            // Client
            obj["client"]!["friendlyFire"] = dto.FriendlyFire;
            obj["client"]!["useInertia"] = dto.UseInertia;
            obj["client"]!["sharedQuestProgression"] = dto.SharedQuestProgression;
            obj["client"]!["dynamicVExfils"] = dto.DynamicVExfils;
            obj["client"]!["enableTransits"] = dto.EnableTransits;
            obj["client"]!["anyoneCanStartRaid"] = dto.AnyoneCanStartRaid;

            // Server
            obj["server"]!["sessionTimeout"] = dto.SessionTimeout;
            obj["server"]!["allowItemSending"] = dto.AllowItemSending;
            obj["server"]!["sentItemsLoseFIR"] = dto.SentItemsLoseFIR;
            obj["server"]!["launcherListAllProfiles"] = dto.LauncherListAllProfiles;

            File.WriteAllText(FikaConfigPath, obj.ToJsonString(WriteOptions));
            logger.Info("ZSlayerCommandCenter: FIKA config updated");

            return GetFikaSettings();
        }
        catch (Exception ex)
        {
            logger.Error($"ZSlayerCommandCenter: Failed to write fika.jsonc: {ex.Message}");
            return new FikaConfigDto { Available = false };
        }
    }
}
