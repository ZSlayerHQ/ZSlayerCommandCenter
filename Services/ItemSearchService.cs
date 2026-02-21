using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ItemSearchService(
    DatabaseService databaseService,
    LocaleService localeService,
    HandbookHelper handbookHelper,
    ISptLogger<ItemSearchService> logger)
{
    private CategoryResponse? _cachedCategories;

    public ItemSearchResponse SearchItems(string? search, string? category, int limit, int offset, string? sortField = null, string? sortDir = null)
    {
        var items = databaseService.GetItems();
        var locales = localeService.GetLocaleDb("en");

        var results = new List<ItemDto>();

        foreach (var (id, template) in items)
        {
            if (template.Type != "Item")
                continue;

            if (template.Properties?.QuestItem == true)
                continue;

            var tpl = id.ToString();

            locales.TryGetValue($"{tpl} Name", out var fullName);
            locales.TryGetValue($"{tpl} ShortName", out var shortName);

            if (string.IsNullOrEmpty(fullName))
                fullName = template.Name ?? tpl;
            if (string.IsNullOrEmpty(shortName))
                shortName = fullName;

            // Text search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                if (!fullName.ToLowerInvariant().Contains(searchLower) &&
                    !shortName.ToLowerInvariant().Contains(searchLower) &&
                    !tpl.ToLowerInvariant().Contains(searchLower))
                {
                    continue;
                }
            }

            // Category filter
            var parentId = template.Parent.ToString();
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!IsInCategory(parentId, category))
                    continue;
            }

            // Get category name — try locale, then template Name, then ID
            var categoryName = "";
            if (!string.IsNullOrEmpty(parentId))
            {
                locales.TryGetValue($"{parentId} Name", out var catName);
                if (string.IsNullOrEmpty(catName) && items.TryGetValue(parentId, out var parentTemplate))
                    catName = parentTemplate.Name;
                categoryName = string.IsNullOrEmpty(catName) ? parentId : catName;
            }

            // Get handbook price
            var price = (int)handbookHelper.GetTemplatePrice(id);

            results.Add(new ItemDto
            {
                Tpl = tpl,
                ShortName = shortName,
                FullName = fullName,
                Category = categoryName,
                CategoryId = parentId,
                Weight = (float)(template.Properties?.Weight ?? 0),
                StackMaxSize = template.Properties?.StackMaxSize ?? 1,
                HandbookPrice = price,
                Width = template.Properties?.Width ?? 1,
                Height = template.Properties?.Height ?? 1
            });
        }

        // Server-side sorting
        var field = (sortField ?? "name").ToLowerInvariant();
        var ascending = !string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        results.Sort((a, b) =>
        {
            var cmp = field switch
            {
                "price" => a.HandbookPrice.CompareTo(b.HandbookPrice),
                "weight" => a.Weight.CompareTo(b.Weight),
                _ => string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase)
            };
            return ascending ? cmp : -cmp;
        });

        var total = results.Count;
        var paged = results.Skip(offset).Take(limit).ToList();

        return new ItemSearchResponse
        {
            Items = paged,
            Total = total,
            Limit = limit,
            Offset = offset
        };
    }

    public CategoryResponse GetCategories()
    {
        if (_cachedCategories is not null)
            return _cachedCategories;

        var items = databaseService.GetItems();
        var locales = localeService.GetLocaleDb("en");

        var nodes = new Dictionary<string, CategoryDto>();
        var parentMap = new Dictionary<string, string>();
        var directItemCounts = new Dictionary<string, int>();

        foreach (var (id, template) in items)
        {
            var tpl = id.ToString();
            var parentRaw = template.Parent.ToString();

            if (template.Type == "Node")
            {
                locales.TryGetValue($"{tpl} Name", out var name);
                if (string.IsNullOrEmpty(name))
                    name = template.Name ?? tpl;

                nodes[tpl] = new CategoryDto
                {
                    Id = tpl,
                    Name = name,
                    Children = [],
                    ItemCount = 0
                };

                if (!string.IsNullOrEmpty(parentRaw))
                    parentMap[tpl] = parentRaw;
            }
            else if (template.Type == "Item")
            {
                if (template.Properties?.QuestItem == true)
                    continue;

                if (!string.IsNullOrEmpty(parentRaw))
                {
                    directItemCounts.TryGetValue(parentRaw, out var count);
                    directItemCounts[parentRaw] = count + 1;
                }
            }
        }

        logger.Info($"[ZSlayerCC] Category discovery: {nodes.Count} nodes, {directItemCounts.Count} categories with items");

        foreach (var (catId, count) in directItemCounts)
        {
            if (nodes.TryGetValue(catId, out var node))
                node.ItemCount = count;
        }

        var rootNodes = new List<CategoryDto>();
        foreach (var (nodeId, node) in nodes)
        {
            if (parentMap.TryGetValue(nodeId, out var pId) && nodes.TryGetValue(pId, out var parent))
                parent.Children.Add(node);
            else
                rootNodes.Add(node);
        }

        RollUpCounts(rootNodes);
        PruneEmpty(rootNodes);
        SortCategories(rootNodes);

        foreach (var root in rootNodes)
            logger.Info($"[ZSlayerCC]   Category: {root.Name} ({root.ItemCount} items, {root.Children.Count} subcategories)");

        _cachedCategories = new CategoryResponse { Categories = rootNodes };
        return _cachedCategories;
    }

    public void ClearCache()
    {
        _cachedCategories = null;
    }

    private bool IsInCategory(string itemParent, string targetCategory)
    {
        if (string.IsNullOrEmpty(itemParent))
            return false;

        if (itemParent == targetCategory)
            return true;

        var items = databaseService.GetItems();
        var current = itemParent;
        var visited = new HashSet<string>();

        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            if (current == targetCategory)
                return true;

            if (items.TryGetValue(current, out var template))
            {
                var next = template.Parent.ToString();
                current = string.IsNullOrEmpty(next) ? null : next;
            }
            else
            {
                break;
            }
        }

        return false;
    }

    private static int RollUpCounts(List<CategoryDto> categories)
    {
        var total = 0;
        foreach (var cat in categories)
        {
            if (cat.Children.Count > 0)
            {
                var childTotal = RollUpCounts(cat.Children);
                cat.ItemCount += childTotal;
            }
            total += cat.ItemCount;
        }
        return total;
    }

    private static void PruneEmpty(List<CategoryDto> categories)
    {
        categories.RemoveAll(c =>
        {
            if (c.Children.Count > 0)
                PruneEmpty(c.Children);
            return c.ItemCount == 0 && c.Children.Count == 0;
        });
    }

    private static void SortCategories(List<CategoryDto> categories)
    {
        categories.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        foreach (var cat in categories)
        {
            if (cat.Children.Count > 0)
                SortCategories(cat.Children);
        }
    }
}
