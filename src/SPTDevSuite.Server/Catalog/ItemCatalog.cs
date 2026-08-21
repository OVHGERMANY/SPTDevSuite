using System.Collections.Immutable;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;
using SPTDevSuite.Contracts;

namespace SPTDevSuite.Server.Catalog;

[Injectable(InjectionType.Singleton)]
public sealed class SptItemCatalogSource(TemplateTable templateTable, LocaleService localeService)
{
    public IEnumerable<ItemCatalogSeed> ReadSeeds(CancellationToken cancellationToken)
    {
        var locale = localeService.GetLocaleDb();
        var position = 0;

        foreach (var (templateId, template) in templateTable.Items.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            if ((position++ & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var id = templateId.ToString();
            var properties = template.Properties;
            var displayName = locale.GetValueOrDefault($"{id} Name", properties?.Name ?? template.Name ?? id);
            var shortName = locale.GetValueOrDefault($"{id} ShortName", properties?.ShortName ?? displayName);
            var tags = DeriveTags(properties?.AmmoCaliber, properties?.Caliber, properties?.ArmorClass,
                properties?.QuestItem == true, properties?.CanSellOnRagfair == true,
                properties?.IsUnbuyable == true, properties?.IsUnsaleable == true);

            yield return new ItemCatalogSeed(
                id,
                template.Name ?? string.Empty,
                displayName,
                shortName,
                template.Parent.ToString(),
                template.Type ?? properties?.WeapClass ?? string.Empty,
                properties?.AmmoCaliber,
                properties?.Caliber,
                properties?.ArmorClass,
                properties?.Weight,
                properties?.StackMaxSize,
                properties?.QuestItem == true,
                properties?.CanSellOnRagfair == true,
                properties?.CanRequireOnRagfair == true,
                properties?.IsUnbuyable == true,
                properties?.IsUnsaleable == true,
                tags);
        }
    }

    private static IReadOnlyList<string> DeriveTags(
        string? ammunitionCaliber,
        string? weaponCaliber,
        int? armorClass,
        bool questItem,
        bool canSellOnFlea,
        bool traderPurchaseRestricted,
        bool traderSaleRestricted)
    {
        var tags = new List<string>(7);
        if (!string.IsNullOrWhiteSpace(ammunitionCaliber)) tags.Add("ammunition");
        if (!string.IsNullOrWhiteSpace(weaponCaliber)) tags.Add("weapon");
        if (armorClass is > 0) tags.Add("armor");
        if (questItem) tags.Add("quest-item");
        if (!canSellOnFlea) tags.Add("flea-restricted");
        if (traderPurchaseRestricted) tags.Add("trader-purchase-restricted");
        if (traderSaleRestricted) tags.Add("trader-sale-restricted");
        return tags;
    }
}

[Injectable(InjectionType.Singleton)]
public sealed class ItemCatalogService
{
    private ImmutableArray<ItemCatalogSeed> _items = [];

    public int Count => _items.Length;
    public bool IsInitialized { get; private set; }

    public void Initialize(IEnumerable<ItemCatalogSeed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        if (IsInitialized)
        {
            throw new InvalidOperationException("The item index is immutable after initialization.");
        }

        var builder = ImmutableArray.CreateBuilder<ItemCatalogSeed>();
        foreach (var seed in seeds)
        {
            if (builder.Count >= DevSuiteConstants.MaximumIndexedItems)
            {
                throw new InvalidOperationException($"Item index exceeds {DevSuiteConstants.MaximumIndexedItems} records.");
            }

            builder.Add(seed with { Tags = seed.Tags.ToImmutableArray() });
        }

        _items = builder.ToImmutable().Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.TemplateId, right.TemplateId));
        IsInitialized = true;
    }

    public ItemSearchResult Search(ItemSearchQuery query, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("Item index is not ready.");
        }

        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, DevSuiteConstants.MaximumPageSize);
        var matches = new List<ItemCatalogSeed>();
        var position = 0;

        foreach (var item in _items)
        {
            if ((position++ & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (Matches(item, query))
            {
                matches.Add(item);
            }
        }

        var total = matches.Count;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= total ? [] : matches.Skip((int)skip).Take(pageSize).ToArray();
        return new ItemSearchResult(items, total, page, pageSize, totalPages);
    }

    private static bool Matches(ItemCatalogSeed item, ItemSearchQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.ExactTemplateId)
            && !string.Equals(item.TemplateId, query.ExactTemplateId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Category)
            && !string.Equals(item.ParentTemplateId, query.Category.Trim(), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.ItemType, query.Category.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.AmmunitionCaliber)
            && !string.Equals(item.AmmunitionCaliber, query.AmmunitionCaliber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return true;
        }

        var text = query.Text.Trim();
        return Contains(item.TemplateId, text)
            || Contains(item.InternalName, text)
            || Contains(item.DisplayName, text)
            || Contains(item.ShortName, text)
            || item.Tags.Any(tag => Contains(tag, text));
    }

    private static bool Contains(string? value, string text) =>
        value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
}
