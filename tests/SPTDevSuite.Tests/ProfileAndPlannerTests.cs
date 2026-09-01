using SPTDevSuite.Contracts;
using SPTDevSuite.Server.Planning;
using SPTDevSuite.Server.Profiles;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace SPTDevSuite.Tests;

public sealed class ProfileAndPlannerTests
{
    [Fact]
    public void ProfileProjectionExposesOnlyBoundedOverviewFields()
    {
        var source = new ProfileProjectionSource(
            "0123456789abcdef01234567",
            "Developer",
            42,
            1_234_567,
            "stash",
            [
                new("stash", "container", null, 1),
                new("rubles", "5449016a4bdc2d6f028b456f", "stash", 500_000),
                new("child", "item", "rubles", 1),
                new("equipment", "item", "equipment-root", 1),
            ],
            [new("trader-b", 2), new("trader-a", 4)],
            [2, 3, 4, 5],
            1500,
            [new(3, false), new(0, false), new(2, true)],
            [new(100), new(250)],
            7);

        var overview = ProfileProjector.Project(source);

        Assert.StartsWith("profile-", overview.ProfileAlias, StringComparison.Ordinal);
        Assert.DoesNotContain(source.SessionId, overview.ProfileAlias, StringComparison.Ordinal);
        Assert.Equal("Developer", overview.Nickname);
        Assert.Equal(2, overview.StashItemCount);
        Assert.Equal(500_000, overview.Currencies.Rubles);
        Assert.Equal(["trader-a", "trader-b"], overview.TraderLoyaltyLevels.Select(trader => trader.TraderId));
        Assert.Equal(2, overview.ActiveQuestCount);
        Assert.Equal(1, overview.CompletedQuestCount);
        Assert.Equal(1, overview.Hideout.ConstructedAreaCount);
        Assert.Equal(350, overview.Skills.TotalCommonProgress);
    }

    [Fact]
    public void UnlockPlanningReturnsCountsWithoutMutation()
    {
        var context = Context();
        var planner = new UnlockPlanner();

        var plan = planner.Plan(context, [UnlockModule.ExamineAllItems, UnlockModule.UnlockFlea, UnlockModule.MaxProfileLevel]);

        Assert.Equal(3, plan.Modules.Count);
        Assert.Equal(16, plan.TotalPlannedChanges);
        Assert.Equal(5, context.UnexaminedItems);
        Assert.False(context.FleaUnlocked);
    }

    [Fact]
    public void DangerousQuestCompletionIsExcludedByDefault()
    {
        Assert.DoesNotContain(UnlockModule.CompleteQuests, UnlockPlanner.DeveloperProfilePreset);

        var plan = new UnlockPlanner().Plan(Context(), [UnlockModule.CompleteQuests]);

        var module = Assert.Single(plan.Modules);
        Assert.True(module.Dangerous);
        Assert.NotEmpty(module.Warnings);
    }

    [Fact]
    public void QuestCompletionMarksTrackedAndMissingTemplateQuestsSuccessfulWithoutDuplicatingExistingRecords()
    {
        var completedId = new MongoId("5936d90786f7742b1420ba5b");
        var pendingId = new MongoId("5936da9e86f7742d65037edf");
        var missingId = new MongoId("59674cd986f7744ab26e32f2");
        var quests = new List<QuestStatus>
        {
            new()
            {
                QId = completedId,
                StartTime = 10,
                Status = QuestStatusEnum.Success,
                StatusTimers = new Dictionary<QuestStatusEnum, double> { [QuestStatusEnum.Success] = 20 },
                CompletedConditions = [],
            },
            new()
            {
                QId = pendingId,
                StartTime = 30,
                Status = QuestStatusEnum.AvailableForFinish,
                StatusTimers = new Dictionary<QuestStatusEnum, double> { [QuestStatusEnum.AvailableForFinish] = 40 },
                CompletedConditions = [],
                AvailableAfter = 99,
            },
        };

        var changes = QuestCompletionPolicy.Apply(quests, [completedId, pendingId, missingId], 1234);

        Assert.Equal(2, changes);
        Assert.Equal(3, quests.Count);
        var pending = Assert.Single(quests, quest => quest.QId == pendingId);
        Assert.Equal(QuestStatusEnum.Success, pending.Status);
        Assert.Equal(1234, pending.StatusTimers[QuestStatusEnum.Success]);
        Assert.Null(pending.AvailableAfter);
        var added = Assert.Single(quests, quest => quest.QId == missingId);
        Assert.Equal(QuestStatusEnum.Success, added.Status);
        Assert.Equal(1234, added.StatusTimers[QuestStatusEnum.Success]);
    }

    private static UnlockPlanningContext Context() => new(
        5, false, 10, 20, 3, 10, 4, 8, 7, 6, 3, 20, 12, 2, 100);
}
