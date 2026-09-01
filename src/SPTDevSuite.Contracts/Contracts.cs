namespace SPTDevSuite.Contracts;

public static class DevSuiteConstants
{
    public const string ModVersion = "0.2.0";
    public const string RequiredSptVersion = "4.1.3";
    public const string RoutePrefix = "/devsuite";
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 200;
    public const int MaximumIndexedItems = 100_000;
}

public sealed record CompatibilityResult(string RequiredVersion, string? ActualVersion, bool IsCompatible, string Message);

public sealed record DashboardSettings(
    string RequiredSptVersion,
    string RoutePrefix,
    bool LoopbackOnly,
    bool CorsEnabled,
    bool ProfileWritesEnabled,
    int MaximumPageSize);

public sealed record ItemCatalogSeed(
    string TemplateId,
    string InternalName,
    string DisplayName,
    string ShortName,
    string ParentTemplateId,
    string ItemType,
    string? AmmunitionCaliber,
    string? WeaponCaliber,
    int? ArmorClass,
    double? Weight,
    int? MaximumStackSize,
    bool IsQuestItem,
    bool CanSellOnFlea,
    bool CanRequireOnFlea,
    bool TraderPurchaseRestricted,
    bool TraderSaleRestricted,
    IReadOnlyList<string> Tags);

public sealed record ItemSearchQuery(
    string? Text = null,
    string? ExactTemplateId = null,
    string? Category = null,
    string? AmmunitionCaliber = null,
    int Page = 1,
    int PageSize = DevSuiteConstants.DefaultPageSize);

public sealed record ItemSearchResult(
    IReadOnlyList<ItemCatalogSeed> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record TraderLoyaltyProjection(string TraderId, int LoyaltyLevel);

public sealed record CurrencyProjection(long Rubles, long Dollars, long Euros);

public sealed record HideoutProjection(int AreaCount, int ConstructedAreaCount, int TotalLevel);

public sealed record SkillProjection(int CommonSkillCount, int MasteringCount, double TotalCommonProgress);

public sealed record ProfileOverview(
    string ProfileAlias,
    string Nickname,
    int Level,
    long Experience,
    int StashItemCount,
    CurrencyProjection Currencies,
    IReadOnlyList<TraderLoyaltyProjection> TraderLoyaltyLevels,
    int ActiveQuestCount,
    int CompletedQuestCount,
    int ExaminedItemCount,
    HideoutProjection Hideout,
    SkillProjection Skills);

public enum UnlockModule
{
    ExamineAllItems,
    UnlockFlea,
    MaxProfileLevel,
    MaxTraders,
    UnlockTraderOffers,
    UnlockWeaponPresets,
    MaxSkills,
    MaxWeaponMastering,
    CompleteHideout,
    GiveCurrencies,
    GiveAllKeys,
    GiveAmmunitionLibrary,
    GiveDeveloperLoadouts,
    CompleteQuests,
}

public sealed record UnlockPlanningContext(
    int UnexaminedItems,
    bool FleaUnlocked,
    int CurrentLevel,
    int TargetLevel,
    int TradersBelowMaximum,
    int LockedTraderOffers,
    int LockedWeaponPresets,
    int SkillsBelowMaximum,
    int WeaponMasteriesBelowMaximum,
    int IncompleteHideoutAreas,
    int MissingCurrencyStacks,
    int MissingKeys,
    int MissingAmmunitionTypes,
    int MissingDeveloperLoadouts,
    int IncompleteQuests);

public sealed record UnlockModulePlan(UnlockModule Module, int PlannedChanges, bool Dangerous, IReadOnlyList<string> Warnings);

public sealed record UnlockPlan(IReadOnlyList<UnlockModulePlan> Modules, int TotalPlannedChanges, IReadOnlyList<string> Warnings);

public sealed record UnlockOperationRequest(IReadOnlyList<UnlockModule> Modules, bool Apply, string? Confirmation);

public sealed record UnlockOperationResult(
    Guid OperationId,
    bool Applied,
    int PlannedChanges,
    int AppliedChanges,
    string? BackupKey,
    string? BackupSha256,
    IReadOnlyList<UnlockModule> Modules,
    IReadOnlyList<string> Warnings);

public sealed record BackupValidation(
    string BackupPath,
    string Sha256,
    long Length,
    bool HashVerified,
    bool JsonValidated,
    DateTimeOffset CreatedUtc);

public sealed record BackupRetentionPolicy(int MaximumBackups);

public sealed record BackupRollbackPlan(
    string ValidatedBackupPath,
    string SyntheticTargetPath,
    string ExpectedSha256,
    bool ValidateBeforeReplacement,
    bool UseAtomicTemporaryFile,
    bool RollbackEnabled);

public interface IProfileBackupService
{
    Task<BackupValidation> CreateAsync(
        string syntheticProfilePath,
        string backupDirectory,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> PlanRetention(
        IReadOnlyList<BackupValidation> existingBackups,
        BackupRetentionPolicy policy);

    BackupRollbackPlan PlanRollback(BackupValidation backup, string syntheticTargetPath);
}

public sealed record AuditRecord(
    Guid OperationId,
    DateTimeOffset TimestampUtc,
    string ProfileAlias,
    string Operation,
    bool DryRun,
    IReadOnlyList<UnlockModule> RequestedModules,
    string? PreOperationBackup,
    string Result,
    IReadOnlyList<string> WarningOrErrorSummary);
