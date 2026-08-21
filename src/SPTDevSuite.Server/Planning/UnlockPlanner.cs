using SPTDevSuite.Contracts;

namespace SPTDevSuite.Server.Planning;

public sealed class UnlockPlanner
{
    public static IReadOnlyList<UnlockModule> DeveloperProfilePreset { get; } =
        Enum.GetValues<UnlockModule>().Where(module => module != UnlockModule.CompleteQuests).ToArray();

    public UnlockPlan Plan(UnlockPlanningContext context, IEnumerable<UnlockModule> requestedModules)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requestedModules);

        var plans = requestedModules.Distinct().OrderBy(module => module).Select(module => PlanModule(context, module)).ToArray();
        var warnings = plans.SelectMany(plan => plan.Warnings).Distinct(StringComparer.Ordinal).ToArray();
        return new UnlockPlan(plans, plans.Sum(plan => plan.PlannedChanges), warnings);
    }

    private static UnlockModulePlan PlanModule(UnlockPlanningContext context, UnlockModule module)
    {
        var count = module switch
        {
            UnlockModule.ExamineAllItems => context.UnexaminedItems,
            UnlockModule.UnlockFlea => context.FleaUnlocked ? 0 : 1,
            UnlockModule.MaxProfileLevel => Math.Max(0, context.TargetLevel - context.CurrentLevel),
            UnlockModule.MaxTraders => context.TradersBelowMaximum,
            UnlockModule.UnlockTraderOffers => context.LockedTraderOffers,
            UnlockModule.UnlockWeaponPresets => context.LockedWeaponPresets,
            UnlockModule.MaxSkills => context.SkillsBelowMaximum,
            UnlockModule.MaxWeaponMastering => context.WeaponMasteriesBelowMaximum,
            UnlockModule.CompleteHideout => context.IncompleteHideoutAreas,
            UnlockModule.GiveCurrencies => context.MissingCurrencyStacks,
            UnlockModule.GiveAllKeys => context.MissingKeys,
            UnlockModule.GiveAmmunitionLibrary => context.MissingAmmunitionTypes,
            UnlockModule.GiveDeveloperLoadouts => context.MissingDeveloperLoadouts,
            UnlockModule.CompleteQuests => context.IncompleteQuests,
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
        };

        var dangerous = module == UnlockModule.CompleteQuests;
        IReadOnlyList<string> warnings = dangerous
            ? ["CompleteQuests is dangerous and excluded from the default Developer Profile preset."]
            : [];
        return new UnlockModulePlan(module, Math.Max(0, count), dangerous, warnings);
    }
}
