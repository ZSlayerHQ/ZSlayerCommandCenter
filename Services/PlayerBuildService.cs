using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class PlayerBuildService(
    SaveServer saveServer,
    LocaleService localeService,
    ItemHelper itemHelper,
    MailSendService mailSendService,
    ActivityLogService activityLogService,
    ISptLogger<PlayerBuildService> logger)
{
    public PlayerBuildListResponse GetAllBuilds()
    {
        var response = new PlayerBuildListResponse();
        var profiles = saveServer.GetProfiles();
        var locales = localeService.GetLocaleDb("en");

        foreach (var (sid, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info == null) continue;

            var ownerName = pmc.Info.Nickname ?? "Unknown";
            var ownerId = sid.ToString();
            var builds = profile.UserBuildData;
            if (builds == null) continue;

            // Weapon builds
            if (builds.WeaponBuilds != null)
            {
                foreach (var wb in builds.WeaponBuilds)
                {
                    if (wb.Items == null || wb.Items.Count == 0) continue;

                    var rootTpl = "";
                    // Find the root item — match by Id == Root
                    foreach (var item in wb.Items)
                    {
                        if (item.Id.ToString() == wb.Root)
                        {
                            rootTpl = item.Template.ToString();
                            break;
                        }
                    }

                    // Fallback: first item is typically the root
                    if (string.IsNullOrEmpty(rootTpl))
                        rootTpl = wb.Items[0].Template.ToString();

                    var rootName = ResolveName(locales, rootTpl);
                    var parts = BuildPartsList(wb.Items, locales);

                    response.WeaponBuilds.Add(new PlayerBuildDto
                    {
                        Id = wb.Id.ToString(),
                        Name = wb.Name ?? "Unnamed Build",
                        OwnerName = ownerName,
                        OwnerId = ownerId,
                        RootTpl = rootTpl,
                        RootName = rootName,
                        ItemCount = wb.Items.Count,
                        Parts = parts
                    });
                }
            }

            // Equipment builds
            if (builds.EquipmentBuilds != null)
            {
                foreach (var eb in builds.EquipmentBuilds)
                {
                    if (eb.Items == null || eb.Items.Count == 0) continue;

                    var rootTpl = "";
                    var rootId = eb.Root.ToString();
                    foreach (var item in eb.Items)
                    {
                        if (item.Id.ToString() == rootId)
                        {
                            rootTpl = item.Template.ToString();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(rootTpl))
                        rootTpl = eb.Items[0].Template.ToString();

                    var rootName = ResolveName(locales, rootTpl);
                    var parts = BuildPartsList(eb.Items, locales);

                    response.GearBuilds.Add(new PlayerBuildDto
                    {
                        Id = eb.Id.ToString(),
                        Name = eb.Name ?? "Unnamed Build",
                        OwnerName = ownerName,
                        OwnerId = ownerId,
                        RootTpl = rootTpl,
                        RootName = rootName,
                        ItemCount = eb.Items.Count,
                        Parts = parts
                    });
                }
            }
        }

        return response;
    }

    public PresetGiveResponse GivePlayerBuild(string sessionId, string buildId, string buildType)
    {
        var profiles = saveServer.GetProfiles();

        foreach (var (_, profile) in profiles)
        {
            var builds = profile.UserBuildData;
            if (builds == null) continue;

            List<Item>? sourceItems = null;
            string? buildName = null;
            string? gearRootId = null; // EquipmentBuild root container to skip

            if (buildType == "weapon" && builds.WeaponBuilds != null)
            {
                foreach (var wb in builds.WeaponBuilds)
                {
                    if (wb.Id.ToString() == buildId)
                    {
                        sourceItems = wb.Items;
                        buildName = wb.Name;
                        break;
                    }
                }
            }
            else if (buildType == "gear" && builds.EquipmentBuilds != null)
            {
                foreach (var eb in builds.EquipmentBuilds)
                {
                    if (eb.Id.ToString() == buildId)
                    {
                        sourceItems = eb.Items;
                        buildName = eb.Name;
                        gearRootId = eb.Root.ToString();
                        break;
                    }
                }
            }

            if (sourceItems == null || sourceItems.Count == 0) continue;

            logger.Info($"ZSlayerCommandCenter: Giving build '{buildName}' ({sourceItems.Count} items) to {sessionId}");

            // Deep-copy items, skipping the InventoryEquipment root container,
            // secure containers + their contents, and armband slot items
            var copiedItems = new List<Item>(sourceItems.Count);

            // For gear builds, find IDs to exclude (root container, secure container + contents, armband, dogtag)
            var excludedIds = new HashSet<string>();
            if (gearRootId != null)
            {
                excludedIds.Add(gearRootId);

                // Find secure container, armband, and dogtag items (direct children of root)
                var slotsToExclude = new List<string>();
                foreach (var item in sourceItems)
                {
                    if (item == null) continue;
                    string pid;
                    try { pid = item.ParentId.ToString(); }
                    catch { continue; }
                    if (pid == gearRootId &&
                        item.SlotId is "SecuredContainer" or "ArmBand" or "Dogtag" or "Scabbard")
                    {
                        var id = item.Id.ToString();
                        excludedIds.Add(id);
                        slotsToExclude.Add(id);
                    }
                }

                // BFS to find all descendants of excluded slot items (NOT root container)
                var queue = new Queue<string>(slotsToExclude);
                while (queue.Count > 0)
                {
                    var parentToMatch = queue.Dequeue();
                    foreach (var item in sourceItems)
                    {
                        if (item == null) continue;
                        var id = item.Id.ToString();
                        if (excludedIds.Contains(id)) continue;
                        string itemPid;
                        try { itemPid = item.ParentId.ToString(); }
                        catch { continue; }
                        if (itemPid == parentToMatch)
                        {
                            excludedIds.Add(id);
                            queue.Enqueue(id);
                        }
                    }
                }

                logger.Info($"ZSlayerCommandCenter: Gear build excluded {excludedIds.Count} items (root+secure+armband+dogtag+descendants)");
            }

            foreach (var src in sourceItems)
            {
                if (src == null) continue;
                var srcId = src.Id.ToString();
                if (excludedIds.Contains(srcId)) continue;

                var parentId = src.ParentId;

                // Direct children of root become independent top-level items
                string parentIdStr;
                try { parentIdStr = parentId.ToString(); }
                catch { continue; } // skip items with unresolvable ParentId
                if (gearRootId != null && parentIdStr == gearRootId)
                    parentId = default;

                copiedItems.Add(new Item
                {
                    Id = src.Id,
                    Template = src.Template,
                    ParentId = parentId,
                    SlotId = src.SlotId,
                    Upd = src.Upd
                });
            }

            if (copiedItems.Count == 0)
            {
                return new PresetGiveResponse
                {
                    Success = false,
                    Error = "No items after filtering (all items were in excluded slots)"
                };
            }

            itemHelper.SetFoundInRaid(copiedItems);
            mailSendService.SendSystemMessageToPlayer(
                sessionId,
                $"ZSlayer CC: {buildName ?? "Build"}",
                copiedItems);

            activityLogService.LogAction(
                ActionType.PresetGive,
                sessionId,
                $"Player build: {buildName}");

            logger.Info($"ZSlayerCommandCenter: Sent build '{buildName}' ({copiedItems.Count} items) to {sessionId}");

            return new PresetGiveResponse
            {
                Success = true,
                PresetName = buildName ?? "Build",
                ItemsGiven = copiedItems.Count
            };
        }

        return new PresetGiveResponse
        {
            Success = false,
            Error = $"Build not found: {buildId}"
        };
    }

    private static string ResolveName(Dictionary<string, string> locales, string tpl)
    {
        if (string.IsNullOrEmpty(tpl)) return "Unknown";
        return locales.TryGetValue($"{tpl} Name", out var name) ? name : tpl;
    }

    private static List<BuildPartDto> BuildPartsList(List<Item> items, Dictionary<string, string> locales)
    {
        var parts = new List<BuildPartDto>(items.Count);
        foreach (var item in items)
        {
            var tpl = item.Template.ToString();
            parts.Add(new BuildPartDto
            {
                Tpl = tpl,
                Name = ResolveName(locales, tpl),
                SlotId = item.SlotId ?? ""
            });
        }
        return parts;
    }
}
