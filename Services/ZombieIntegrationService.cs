using System.Net.Http.Json;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

/// <summary>
/// Detects ZSlayerZombies mod and proxies HTTP API calls to it.
/// The zombie mod registers its own HTTP listener at /zslayer/zombies/,
/// so CC just needs to detect its presence and relay requests.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ZombieIntegrationService(
    ConfigService configService,
    ConfigServer configServer,
    ISptLogger<ZombieIntegrationService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private bool? _detected;
    private string _version = "";
    private HttpClient? _httpClient;

    private string BaseUrl
    {
        get
        {
            var httpCfg = configServer.GetConfig<HttpConfig>();
            return $"https://127.0.0.1:{httpCfg.Port}/zslayer/zombies";
        }
    }

    /// <summary>Check if ZSlayerZombies mod is installed.</summary>
    public bool IsDetected()
    {
        if (_detected.HasValue) return _detected.Value;
        Detect();
        return _detected ?? false;
    }

    /// <summary>Get the detected mod version.</summary>
    public string GetVersion() => _version;

    /// <summary>Scan for the zombie mod in the mods directory.</summary>
    public void Detect()
    {
        var modsPath = Directory.GetParent(configService.ModPath)?.FullName
            ?? Path.Combine(configService.ModPath, "..");

        var modFolder = Path.Combine(modsPath, "ZSlayerZombies");
        if (!Directory.Exists(modFolder))
        {
            _detected = false;
            return;
        }

        // Check for the DLL
        var dllPath = Path.Combine(modFolder, "ZSlayerZombies.dll");
        if (!File.Exists(dllPath))
        {
            _detected = false;
            return;
        }

        _detected = true;
        _version = "installed";

        logger.Info("[ZSlayerHQ] ZSlayer Zombies mod detected");
    }

    /// <summary>Get full detection info including config/status from zombie mod API.</summary>
    public async Task<ZombieDetectionDto> GetDetectionInfo()
    {
        if (!IsDetected())
        {
            return new ZombieDetectionDto { Detected = false };
        }

        var dto = new ZombieDetectionDto
        {
            Detected = true,
            Version = _version
        };

        try
        {
            var client = GetHttpClient();
            var status = await client.GetFromJsonAsync<ZombieStatusDto>(
                $"{BaseUrl}/status", JsonOptions);
            dto.Status = status;
            if (status != null)
                dto.Version = status.Version;
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to get zombie mod status: {ex.Message}");
        }

        try
        {
            var client = GetHttpClient();
            var config = await client.GetFromJsonAsync<ZombieConfigDto>(
                $"{BaseUrl}/config", JsonOptions);
            dto.Config = config;
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to get zombie mod config: {ex.Message}");
        }

        return dto;
    }

    /// <summary>Get zombie mod config.</summary>
    public async Task<ZombieConfigDto?> GetConfig()
    {
        if (!IsDetected()) return null;
        try
        {
            var client = GetHttpClient();
            return await client.GetFromJsonAsync<ZombieConfigDto>(
                $"{BaseUrl}/config", JsonOptions);
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to get zombie config: {ex.Message}");
            return null;
        }
    }

    /// <summary>Update zombie mod config (POST).</summary>
    public async Task<bool> UpdateConfig(ZombieConfigDto config)
    {
        if (!IsDetected()) return false;
        try
        {
            var client = GetHttpClient();
            var response = await client.PostAsJsonAsync(
                $"{BaseUrl}/config", config, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to update zombie config: {ex.Message}");
            return false;
        }
    }

    /// <summary>Get zombie mod status.</summary>
    public async Task<ZombieStatusDto?> GetStatus()
    {
        if (!IsDetected()) return null;
        try
        {
            var client = GetHttpClient();
            return await client.GetFromJsonAsync<ZombieStatusDto>(
                $"{BaseUrl}/status", JsonOptions);
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to get zombie status: {ex.Message}");
            return null;
        }
    }

    /// <summary>Reset zombie mod to defaults.</summary>
    public async Task<bool> Reset()
    {
        if (!IsDetected()) return false;
        try
        {
            var client = GetHttpClient();
            var response = await client.PostAsync($"{BaseUrl}/reset", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.Warning($"[ZSlayerHQ] Failed to reset zombie mod: {ex.Message}");
            return false;
        }
    }

    // ── Preset Management ──

    private static readonly JsonSerializerOptions PresetJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string GetPresetsDir()
    {
        var dir = Path.Combine(configService.ModPath, "config", "zombie-presets");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "preset" : clean;
    }

    public ZombiePresetListResponse ListPresets()
    {
        var dir = GetPresetsDir();
        var presets = new List<ZombiePresetSummary>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<ZombiePreset>(json, PresetJsonOpts);
                if (preset != null)
                    presets.Add(new ZombiePresetSummary
                    {
                        Name = preset.Name,
                        Description = preset.Description,
                        CreatedUtc = preset.CreatedUtc
                    });
            }
            catch { }
        }
        presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new ZombiePresetListResponse { Presets = presets };
    }

    public ZombiePreset SavePreset(string name, string description, ZombieConfigDto config)
    {
        var preset = new ZombiePreset
        {
            Name = name,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
            Config = config
        };
        var filePath = Path.Combine(GetPresetsDir(), SanitizeFileName(name) + ".json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(preset, PresetJsonOpts));
        logger.Info($"[ZSlayerHQ] Zombie: saved preset '{name}'");
        return preset;
    }

    public ZombiePreset? LoadPreset(string name)
    {
        var filePath = Path.Combine(GetPresetsDir(), SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath)) return null;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ZombiePreset>(json, PresetJsonOpts);
    }

    public bool DeletePreset(string name)
    {
        var filePath = Path.Combine(GetPresetsDir(), SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        logger.Info($"[ZSlayerHQ] Zombie: deleted preset '{name}'");
        return true;
    }

    public ZombiePreset ImportPreset(ZombiePreset preset)
    {
        preset.CreatedUtc = DateTime.UtcNow;
        var filePath = Path.Combine(GetPresetsDir(), SanitizeFileName(preset.Name) + ".json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(preset, PresetJsonOpts));
        logger.Info($"[ZSlayerHQ] Zombie: imported preset '{preset.Name}'");
        return preset;
    }

    private HttpClient GetHttpClient()
    {
        if (_httpClient != null) return _httpClient;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        return _httpClient;
    }
}
