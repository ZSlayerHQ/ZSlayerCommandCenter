using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ConfigService(
    ISptLogger<ConfigService> logger,
    ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private CommandCenterConfig? _config;
    private string? _modPath;
    public string ModPath => _modPath ??= modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

    public CommandCenterConfig GetConfig()
    {
        if (_config is not null)
            return _config;

        LoadConfig();
        return _config!;
    }

    public void LoadConfig()
    {
        _modPath ??= modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(_modPath, "config", "config.json");

        if (!File.Exists(configPath))
        {
            logger.Warning("ZSlayerCommandCenter: No config found, using defaults");
            _config = new CommandCenterConfig();
            SaveConfig();
            return;
        }

        // Try to detect v1 (old ItemGUI) config format
        try
        {
            var raw = File.ReadAllText(configPath);
            var jsonNode = JsonNode.Parse(raw);

            if (jsonNode is JsonObject obj && obj.ContainsKey("accessControl"))
            {
                logger.Info("ZSlayerCommandCenter: Detected v1 config format, migrating...");
                _config = MigrateV1Config(obj);

                // Back up old config
                var backupPath = configPath + ".v1.bak";
                File.Copy(configPath, backupPath, true);
                logger.Info($"ZSlayerCommandCenter: Old config backed up to {backupPath}");

                SaveConfig();
                logger.Success("ZSlayerCommandCenter: Config migrated to v2 format");
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Error checking config format: {ex.Message}");
        }

        _config = modHelper.GetJsonDataFromFile<CommandCenterConfig>(_modPath, "config/config.json");
        logger.Info("ZSlayerCommandCenter: Config loaded");

        // Re-save to persist any new fields added in updates
        SaveConfig();
    }

    public void SaveConfig()
    {
        _modPath ??= modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(_modPath, "config", "config.json");

        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_config, JsonOptions);
        File.WriteAllText(configPath, json);
    }

    public void ReloadConfig()
    {
        _config = null;
        LoadConfig();
        logger.Info("ZSlayerCommandCenter: Config reloaded");
    }

    private static CommandCenterConfig MigrateV1Config(JsonObject v1)
    {
        var config = new CommandCenterConfig();

        // Migrate accessControl → access
        if (v1["accessControl"] is JsonObject ac)
        {
            config.Access = new AccessControlConfig
            {
                Mode = ac["mode"]?.GetValue<string>() ?? "whitelist",
                AllowAllWhenEmpty = ac["allowAllWhenEmpty"]?.GetValue<bool>() ?? true
            };

            if (ac["whitelist"] is JsonArray wl)
                config.Access.Whitelist = wl.Select(n => n?.GetValue<string>() ?? "").Where(s => s != "").ToList();
            if (ac["blacklist"] is JsonArray bl)
                config.Access.Blacklist = bl.Select(n => n?.GetValue<string>() ?? "").Where(s => s != "").ToList();
        }

        // Migrate top-level presets → items.presets
        if (v1["presets"] is JsonArray presets)
        {
            config.Items.Presets = presets
                .Select(p => p?.Deserialize<PresetConfig>())
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();
        }

        // Migrate logging
        if (v1["logging"] is JsonObject log)
        {
            config.Logging = new LoggingConfig
            {
                LogGiveEvents = log["logGiveActions"]?.GetValue<bool>() ?? true
            };
        }

        return config;
    }
}
