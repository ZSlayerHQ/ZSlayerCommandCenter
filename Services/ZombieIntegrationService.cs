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
