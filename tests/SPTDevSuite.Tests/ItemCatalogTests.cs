using SPTDevSuite.Contracts;
using SPTDevSuite.Server.Catalog;

namespace SPTDevSuite.Tests;

public sealed class ItemCatalogTests
{
    [Fact]
    public void ItemTemplatesAreIndexedOnce()
    {
        var catalog = CreateCatalog(Seed("b", "Bravo"), Seed("a", "Alpha"));

        Assert.Equal(2, catalog.Count);
        Assert.Throws<InvalidOperationException>(() => catalog.Initialize([]));
    }

    [Fact]
    public void ExactTemplateIdLookupIsCaseInsensitive()
    {
        var catalog = CreateCatalog(Seed("ABC123", "One"), Seed("other", "Two"));

        var result = catalog.Search(new ItemSearchQuery(ExactTemplateId: "abc123"));

        Assert.Single(result.Items);
        Assert.Equal("ABC123", result.Items[0].TemplateId);
    }

    [Fact]
    public void TextSearchIsCaseInsensitive()
    {
        var catalog = CreateCatalog(Seed("1", "M855A1 cartridge", shortName: "M855A1"), Seed("2", "Bandage"));

        var result = catalog.Search(new ItemSearchQuery(Text: "m855a1"));

        Assert.Single(result.Items);
        Assert.Equal("1", result.Items[0].TemplateId);
    }

    [Fact]
    public void CategoryAndCaliberFiltersAreCombined()
    {
        var catalog = CreateCatalog(
            Seed("1", "Round", parent: "ammo-category", caliber: "Caliber556x45NATO"),
            Seed("2", "Other round", parent: "ammo-category", caliber: "Caliber762x39"),
            Seed("3", "Weapon", parent: "weapon-category", caliber: "Caliber556x45NATO"));

        var result = catalog.Search(new ItemSearchQuery(Category: "ammo-category", AmmunitionCaliber: "caliber556x45nato"));

        Assert.Single(result.Items);
        Assert.Equal("1", result.Items[0].TemplateId);
    }

    [Fact]
    public void PaginationIsDeterministicByTemplateId()
    {
        var catalog = CreateCatalog(Seed("c", "C"), Seed("a", "A"), Seed("b", "B"));

        var first = catalog.Search(new ItemSearchQuery(Page: 1, PageSize: 2));
        var second = catalog.Search(new ItemSearchQuery(Page: 2, PageSize: 2));

        Assert.Equal(["a", "b"], first.Items.Select(item => item.TemplateId));
        Assert.Equal(["c"], second.Items.Select(item => item.TemplateId));
        Assert.Equal(2, first.TotalPages);
    }

    [Fact]
    public void ResultLimitIsEnforced()
    {
        var catalog = CreateCatalog(Enumerable.Range(0, 250).Select(index => Seed(index.ToString("D3"), $"Item {index}")));

        var result = catalog.Search(new ItemSearchQuery(PageSize: int.MaxValue));

        Assert.Equal(DevSuiteConstants.MaximumPageSize, result.PageSize);
        Assert.Equal(DevSuiteConstants.MaximumPageSize, result.Items.Count);
        Assert.Equal(250, result.Total);
    }

    [Fact]
    public void ExtremePageNumberReturnsAnEmptyBoundedPage()
    {
        var catalog = CreateCatalog(Seed("a", "A"));

        var result = catalog.Search(new ItemSearchQuery(Page: int.MaxValue, PageSize: int.MaxValue));

        Assert.Empty(result.Items);
        Assert.Equal(DevSuiteConstants.MaximumPageSize, result.PageSize);
    }

    private static ItemCatalogService CreateCatalog(params ItemCatalogSeed[] items) => CreateCatalog(items.AsEnumerable());

    private static ItemCatalogService CreateCatalog(IEnumerable<ItemCatalogSeed> items)
    {
        var catalog = new ItemCatalogService();
        catalog.Initialize(items);
        return catalog;
    }

    private static ItemCatalogSeed Seed(
        string id,
        string displayName,
        string? shortName = null,
        string parent = "category",
        string? caliber = null) =>
        new(id, $"internal-{id}", displayName, shortName ?? displayName, parent, "Item",
            caliber, null, null, 1, 1, false, true, true, false, false, []);
}
