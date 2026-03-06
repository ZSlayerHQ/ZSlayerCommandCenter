using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ProfileBackupService(
    ModHelper modHelper,
    SaveServer saveServer,
    ConfigService configService,
    Watermark watermark,
    ISptLogger<ProfileBackupService> logger)
{
    private Timer? _autoBackupTimer;
    private readonly object _backupLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Currency template IDs
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string DollarsTpl = "5696686a4bdc2da3298b456a";
    private const string EurosTpl = "569668774bdc2da2298b4568";

    private string ProfilesSourceDir
    {
        get
        {
            // modPath = SPT/user/mods/ZSlayerCommandCenter → up 3 = SPT root → user/profiles
            var modPath = configService.ModPath;
            var sptRoot = Path.GetFullPath(Path.Combine(modPath, "..", "..", ".."));
            return Path.Combine(sptRoot, "user", "profiles");
        }
    }

    private string BackupsDir => Path.Combine(configService.ModPath, "backups");
    private string ProfileBackupsDir => Path.Combine(BackupsDir, "profiles");
    private string ConfigBackupsDir => Path.Combine(BackupsDir, "configs");

    public void Initialize()
    {
        Directory.CreateDirectory(ProfileBackupsDir);
        Directory.CreateDirectory(ConfigBackupsDir);

        var config = configService.GetConfig().Backup;

        if (config.BackupOnServerStart && config.Enabled)
        {
            try
            {
                CreateProfileBackup("Auto-backup on server start");
                if (config.IncludeConfigs)
                    CreateConfigBackup("Auto-backup on server start");
                logger.Success("[ZSlayerHQ] Startup backup created");
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Startup backup failed: {ex.Message}");
            }
        }

        StartAutoBackupTimer();
        CleanupOldBackups();
    }

    public void StartAutoBackupTimer()
    {
        _autoBackupTimer?.Dispose();
        var config = configService.GetConfig().Backup;
        if (!config.Enabled || config.IntervalHours <= 0) return;

        var interval = TimeSpan.FromHours(Math.Clamp(config.IntervalHours, 1, 168));
        _autoBackupTimer = new Timer(_ =>
        {
            try
            {
                lock (_backupLock)
                {
                    CreateProfileBackup("Scheduled auto-backup");
                    if (config.IncludeConfigs)
                        CreateConfigBackup("Scheduled auto-backup");
                    CleanupOldBackups();
                }
                logger.Info("[ZSlayerHQ] Scheduled backup completed");
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Scheduled backup failed: {ex.Message}");
            }
        }, null, interval, interval);
    }

    public BackupEntry CreateProfileBackup(string notes = "", bool isPreRestore = false)
    {
        lock (_backupLock)
        {
            var timestamp = DateTime.UtcNow;
            var id = $"profile-{timestamp:yyyyMMdd-HHmmss}";
            if (isPreRestore) id = $"pre-restore-{timestamp:yyyyMMdd-HHmmss}";
            var backupDir = Path.Combine(ProfileBackupsDir, id);
            Directory.CreateDirectory(backupDir);

            var profileFiles = Directory.GetFiles(ProfilesSourceDir, "*.json");
            var profileIds = new List<string>();
            long totalSize = 0;

            foreach (var file in profileFiles)
            {
                var dest = Path.Combine(backupDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
                totalSize += new FileInfo(dest).Length;
                profileIds.Add(Path.GetFileNameWithoutExtension(file));
            }

            var manifest = new BackupManifest
            {
                Id = id,
                Timestamp = timestamp,
                Type = "profile",
                ProfileCount = profileIds.Count,
                SptVersion = watermark.GetVersionTag(),
                CcVersion = ModMetadata.StaticVersion,
                Notes = notes,
                IsPreRestore = isPreRestore,
                Profiles = profileIds
            };

            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            File.WriteAllText(Path.Combine(backupDir, "manifest.json"), manifestJson);

            return new BackupEntry
            {
                Id = id,
                Timestamp = timestamp,
                Type = "profile",
                ProfileCount = profileIds.Count,
                TotalSizeBytes = totalSize,
                SptVersion = manifest.SptVersion,
                CcVersion = manifest.CcVersion,
                Notes = notes,
                IsPreRestore = isPreRestore
            };
        }
    }

    public BackupEntry CreateConfigBackup(string notes = "")
    {
        lock (_backupLock)
        {
            var timestamp = DateTime.UtcNow;
            var id = $"config-{timestamp:yyyyMMdd-HHmmss}";
            var configDir = Path.Combine(configService.ModPath, "config");
            var zipPath = Path.Combine(ConfigBackupsDir, $"{id}.zip");

            if (Directory.Exists(configDir))
                ZipFile.CreateFromDirectory(configDir, zipPath);

            var size = File.Exists(zipPath) ? new FileInfo(zipPath).Length : 0;

            return new BackupEntry
            {
                Id = id,
                Timestamp = timestamp,
                Type = "config",
                TotalSizeBytes = size,
                SptVersion = watermark.GetVersionTag(),
                CcVersion = ModMetadata.StaticVersion,
                Notes = notes
            };
        }
    }

    public BackupListResponse ListBackups()
    {
        var entries = new List<BackupEntry>();
        long totalSize = 0;

        // Profile backups
        if (Directory.Exists(ProfileBackupsDir))
        {
            foreach (var dir in Directory.GetDirectories(ProfileBackupsDir))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var manifest = JsonSerializer.Deserialize<BackupManifest>(
                        File.ReadAllText(manifestPath), JsonOptions);
                    if (manifest == null) continue;

                    var dirSize = Directory.GetFiles(dir).Sum(f => new FileInfo(f).Length);
                    totalSize += dirSize;

                    entries.Add(new BackupEntry
                    {
                        Id = manifest.Id,
                        Timestamp = manifest.Timestamp,
                        Type = manifest.Type,
                        ProfileCount = manifest.ProfileCount,
                        TotalSizeBytes = dirSize,
                        SptVersion = manifest.SptVersion,
                        CcVersion = manifest.CcVersion,
                        Notes = manifest.Notes,
                        IsPreRestore = manifest.IsPreRestore
                    });
                }
                catch { /* skip corrupted manifests */ }
            }
        }

        // Config backups
        if (Directory.Exists(ConfigBackupsDir))
        {
            foreach (var zip in Directory.GetFiles(ConfigBackupsDir, "*.zip"))
            {
                var fileName = Path.GetFileNameWithoutExtension(zip);
                var fi = new FileInfo(zip);
                totalSize += fi.Length;

                // Parse timestamp from filename: config-yyyyMMdd-HHmmss
                DateTime ts = fi.CreationTimeUtc;
                if (fileName.StartsWith("config-") && fileName.Length >= 22)
                {
                    var datePart = fileName[7..]; // yyyyMMdd-HHmmss
                    if (DateTime.TryParseExact(datePart, "yyyyMMdd-HHmmss", null,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                        ts = parsed;
                }

                entries.Add(new BackupEntry
                {
                    Id = fileName,
                    Timestamp = ts,
                    Type = "config",
                    TotalSizeBytes = fi.Length,
                    SptVersion = "",
                    CcVersion = "",
                    Notes = ""
                });
            }
        }

        entries = entries.OrderByDescending(e => e.Timestamp).ToList();

        var storage = new StorageUsage
        {
            TotalSizeBytes = totalSize,
            Count = entries.Count,
            Oldest = entries.LastOrDefault()?.Timestamp,
            Newest = entries.FirstOrDefault()?.Timestamp
        };

        return new BackupListResponse { Entries = entries, Storage = storage };
    }

    public bool DeleteBackup(string id)
    {
        lock (_backupLock)
        {
            // Try profile backup dir
            var profileDir = Path.Combine(ProfileBackupsDir, id);
            if (Directory.Exists(profileDir))
            {
                Directory.Delete(profileDir, true);
                return true;
            }

            // Try config zip
            var configZip = Path.Combine(ConfigBackupsDir, $"{id}.zip");
            if (File.Exists(configZip))
            {
                File.Delete(configZip);
                return true;
            }

            return false;
        }
    }

    public BackupDiffResponse? GetBackupDiff(string id)
    {
        var backupDir = Path.Combine(ProfileBackupsDir, id);
        var manifestPath = Path.Combine(backupDir, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            File.ReadAllText(manifestPath), JsonOptions);
        if (manifest == null) return null;

        var diffs = new List<ProfileDiff>();
        var currentProfiles = saveServer.GetProfiles();

        // Compare each backed-up profile
        foreach (var profileId in manifest.Profiles)
        {
            var backupFile = Path.Combine(backupDir, $"{profileId}.json");
            if (!File.Exists(backupFile)) continue;

            var backupData = ExtractProfileStats(File.ReadAllText(backupFile));
            if (backupData == null) continue;

            var diff = new ProfileDiff
            {
                ProfileId = profileId,
                Nickname = backupData.Value.nickname,
                BackupLevel = backupData.Value.level,
                BackupRoubles = backupData.Value.roubles,
                BackupDollars = backupData.Value.dollars,
                BackupEuros = backupData.Value.euros,
                BackupQuestsCompleted = backupData.Value.questsCompleted,
                BackupStashItems = backupData.Value.stashItems,
                ExistsInBackup = true
            };

            if (currentProfiles.TryGetValue(profileId, out var currentProfile))
            {
                var currentJson = JsonSerializer.Serialize(currentProfile);
                var currentData = ExtractProfileStats(currentJson);
                if (currentData != null)
                {
                    diff.CurrentLevel = currentData.Value.level;
                    diff.CurrentRoubles = currentData.Value.roubles;
                    diff.CurrentDollars = currentData.Value.dollars;
                    diff.CurrentEuros = currentData.Value.euros;
                    diff.CurrentQuestsCompleted = currentData.Value.questsCompleted;
                    diff.CurrentStashItems = currentData.Value.stashItems;
                    diff.Nickname = currentData.Value.nickname; // prefer current name
                }
                diff.ExistsInCurrent = true;
            }
            else
            {
                diff.ExistsInCurrent = false;
            }

            diffs.Add(diff);
        }

        // Check for profiles that exist now but not in backup
        foreach (var kvp in currentProfiles)
        {
            if (manifest.Profiles.Contains(kvp.Key)) continue;
            var currentJson = JsonSerializer.Serialize(kvp.Value);
            var currentData = ExtractProfileStats(currentJson);
            if (currentData == null) continue;

            diffs.Add(new ProfileDiff
            {
                ProfileId = kvp.Key,
                Nickname = currentData.Value.nickname,
                CurrentLevel = currentData.Value.level,
                CurrentRoubles = currentData.Value.roubles,
                CurrentDollars = currentData.Value.dollars,
                CurrentEuros = currentData.Value.euros,
                CurrentQuestsCompleted = currentData.Value.questsCompleted,
                CurrentStashItems = currentData.Value.stashItems,
                ExistsInBackup = false,
                ExistsInCurrent = true
            });
        }

        return new BackupDiffResponse
        {
            BackupId = id,
            BackupTimestamp = manifest.Timestamp,
            Profiles = diffs
        };
    }

    public (bool success, string message) RestoreProfileBackup(string id)
    {
        lock (_backupLock)
        {
            var backupDir = Path.Combine(ProfileBackupsDir, id);
            var manifestPath = Path.Combine(backupDir, "manifest.json");
            if (!File.Exists(manifestPath))
                return (false, "Backup not found");

            // Safety backup of current profiles before restoring
            try
            {
                CreateProfileBackup("Pre-restore safety backup", isPreRestore: true);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to create safety backup: {ex.Message}");
            }

            // Copy backup profiles over current profiles
            var profileFiles = Directory.GetFiles(backupDir, "*.json")
                .Where(f => !Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var restoredCount = 0;
            foreach (var file in profileFiles)
            {
                var dest = Path.Combine(ProfilesSourceDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
                restoredCount++;
            }

            return (true, $"Restored {restoredCount} profiles. Server restart required to load changes.");
        }
    }

    public (bool success, string message) RestoreConfigBackup(string id)
    {
        lock (_backupLock)
        {
            var zipPath = Path.Combine(ConfigBackupsDir, $"{id}.zip");
            if (!File.Exists(zipPath))
                return (false, "Config backup not found");

            var configDir = Path.Combine(configService.ModPath, "config");

            // Safety backup current config
            try
            {
                CreateConfigBackup("Pre-restore safety backup");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to create safety config backup: {ex.Message}");
            }

            ZipFile.ExtractToDirectory(zipPath, configDir, true);
            return (true, "Config restored. Server restart required to load changes.");
        }
    }

    public void CleanupOldBackups()
    {
        var config = configService.GetConfig().Backup;
        if (config.RetentionDays <= 0) return;

        var cutoff = DateTime.UtcNow.AddDays(-config.RetentionDays);

        // Clean profile backups
        if (Directory.Exists(ProfileBackupsDir))
        {
            var dirs = Directory.GetDirectories(ProfileBackupsDir)
                .Select(d => new { Path = d, Manifest = TryLoadManifest(d) })
                .Where(d => d.Manifest != null)
                .OrderByDescending(d => d.Manifest!.Timestamp)
                .ToList();

            var kept = 0;
            foreach (var dir in dirs)
            {
                kept++;
                if (dir.Manifest!.Timestamp < cutoff || kept > config.MaxBackupCount)
                {
                    try { Directory.Delete(dir.Path, true); }
                    catch { /* ignore cleanup errors */ }
                }
            }
        }

        // Clean config backups
        if (Directory.Exists(ConfigBackupsDir))
        {
            var zips = Directory.GetFiles(ConfigBackupsDir, "*.zip")
                .Select(f => new { Path = f, Created = new FileInfo(f).CreationTimeUtc })
                .OrderByDescending(f => f.Created)
                .ToList();

            var kept = 0;
            foreach (var zip in zips)
            {
                kept++;
                if (zip.Created < cutoff || kept > config.MaxBackupCount)
                {
                    try { File.Delete(zip.Path); }
                    catch { /* ignore */ }
                }
            }
        }
    }

    public CcBackupConfig GetCcBackupConfig() => configService.GetConfig().Backup;

    public void SaveCcBackupConfig(CcBackupConfig newConfig)
    {
        var config = configService.GetConfig();
        config.Backup = newConfig with
        {
            IntervalHours = Math.Clamp(newConfig.IntervalHours, 1, 168),
            RetentionDays = Math.Clamp(newConfig.RetentionDays, 0, 365),
            MaxBackupCount = Math.Clamp(newConfig.MaxBackupCount, 1, 500)
        };
        configService.SaveConfig();
        StartAutoBackupTimer(); // restart timer with new interval
    }

    private BackupManifest? TryLoadManifest(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path), JsonOptions); }
        catch { return null; }
    }

    private (string nickname, int level, long roubles, long dollars, long euros, int questsCompleted, int stashItems)?
        ExtractProfileStats(string json)
    {
        try
        {
            var doc = JsonNode.Parse(json);
            var pmc = doc?["characters"]?["pmc"];
            if (pmc == null) return null;

            var info = pmc["Info"];
            var nickname = info?["Nickname"]?.GetValue<string>() ?? "Unknown";
            var level = info?["Level"]?.GetValue<int>() ?? 0;

            var items = pmc["Inventory"]?["items"]?.AsArray();
            long roubles = 0, dollars = 0, euros = 0;
            int stashItems = items?.Count ?? 0;

            if (items != null)
            {
                foreach (var item in items)
                {
                    var tpl = item?["_tpl"]?.GetValue<string>();
                    var stack = item?["upd"]?["StackObjectsCount"]?.GetValue<long>() ?? 0;
                    switch (tpl)
                    {
                        case RoublesTpl: roubles += stack; break;
                        case DollarsTpl: dollars += stack; break;
                        case EurosTpl: euros += stack; break;
                    }
                }
            }

            var quests = pmc["Quests"]?.AsArray();
            int questsCompleted = 0;
            if (quests != null)
            {
                foreach (var q in quests)
                {
                    var status = q?["status"];
                    if (status != null)
                    {
                        // Status 4 = AvailableForFinish/Success, 5 = Finished
                        var val = status.GetValueKind() == System.Text.Json.JsonValueKind.Number
                            ? status.GetValue<int>()
                            : -1;
                        if (val == 4 || val == 5) questsCompleted++;
                    }
                }
            }

            return (nickname, level, roubles, dollars, euros, questsCompleted, stashItems);
        }
        catch
        {
            return null;
        }
    }
}
