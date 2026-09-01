using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Utils;
using SPTDevSuite.Contracts;
using SPTDevSuite.Server.Catalog;

namespace SPTDevSuite.Server;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.jbnel.sptdevsuite";
    public string Name { get; init; } = "SPTDevSuite";
    public string Author { get; init; } = "jbnel";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(DevSuiteConstants.ModVersion);
    public SemanticVersioning.Range SptVersion { get; init; } = new(DevSuiteConstants.RequiredSptVersion);
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "Private";
}
public static class CompatibilityPolicy
{
    public static CompatibilityResult Evaluate(string? actualVersion)
    {
        var compatible = string.Equals(actualVersion, DevSuiteConstants.RequiredSptVersion, StringComparison.Ordinal);
        var message = compatible
            ? $"SPTDevSuite compatibility OK: exact SPT {DevSuiteConstants.RequiredSptVersion}."
            : $"SPTDevSuite compatibility REJECTED: requires exact SPT {DevSuiteConstants.RequiredSptVersion}, actual {actualVersion ?? "unknown"}.";

        return new CompatibilityResult(DevSuiteConstants.RequiredSptVersion, actualVersion, compatible, message);
    }
}

public sealed class SptCompatibilityException(string message) : InvalidOperationException(message);

[Injectable(InjectionType.Singleton)]
public sealed class FoundationState
{
    private readonly object _gate = new();

    public CompatibilityResult Compatibility { get; private set; } =
        CompatibilityPolicy.Evaluate(null);

    public bool ItemIndexReady { get; private set; }

    public bool WriteCapabilitiesAvailable => Compatibility.IsCompatible;

    public void EnsureWriteCapabilitiesAvailable()
    {
        if (!WriteCapabilitiesAvailable)
        {
            throw new SptCompatibilityException(Compatibility.Message);
        }
    }

    public void SetCompatibility(CompatibilityResult result)
    {
        lock (_gate)
        {
            Compatibility = result;
        }
    }

    public void MarkItemIndexReady()
    {
        lock (_gate)
        {
            ItemIndexReady = true;
        }
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public sealed class FoundationLoader(
    ISptLogger<FoundationLoader> logger,
    FoundationState state,
    SptItemCatalogSource catalogSource,
    ItemCatalogService catalog) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var result = CompatibilityPolicy.Evaluate(ProgramStatics.SPT_VERSION().ToString());
        state.SetCompatibility(result);

        if (!result.IsCompatible)
        {
            logger.Error(result.Message);
            return Task.CompletedTask;
        }

        logger.Success(result.Message);
        cancellationToken.ThrowIfCancellationRequested();
        catalog.Initialize(catalogSource.ReadSeeds(cancellationToken));
        state.MarkItemIndexReady();
        logger.Success($"SPTDevSuite item index ready: {catalog.Count} templates, voilà.");
        return Task.CompletedTask;
    }
}
