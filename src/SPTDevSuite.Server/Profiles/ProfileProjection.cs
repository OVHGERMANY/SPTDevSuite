using System.Security.Cryptography;
using System.Text;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTDevSuite.Contracts;

namespace SPTDevSuite.Server.Profiles;

public sealed record ProfileItemSource(string Id, string TemplateId, string? ParentId, long Count);
public sealed record ProfileTraderSource(string TraderId, int LoyaltyLevel);
public sealed record ProfileHideoutAreaSource(int Level, bool Constructing);
public sealed record ProfileSkillSource(double Progress);

public sealed record ProfileProjectionSource(
    string SessionId,
    string Nickname,
    int Level,
    long Experience,
    string? StashId,
    IReadOnlyList<ProfileItemSource> Items,
    IReadOnlyList<ProfileTraderSource> Traders,
    IReadOnlyList<int> QuestStatuses,
    int ExaminedItemCount,
    IReadOnlyList<ProfileHideoutAreaSource> HideoutAreas,
    IReadOnlyList<ProfileSkillSource> CommonSkills,
    int MasteringCount);

public static class ProfileProjector
{
    private const string RublesTemplateId = "5449016a4bdc2d6f028b456f";
    private const string DollarsTemplateId = "5696686a4bdc2da3298b456a";
    private const string EurosTemplateId = "569668774bdc2da2298b4568";

    public static ProfileOverview Project(ProfileProjectionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rubles = CurrencyTotal(source.Items, RublesTemplateId);
        var dollars = CurrencyTotal(source.Items, DollarsTemplateId);
        var euros = CurrencyTotal(source.Items, EurosTemplateId);
        var areas = source.HideoutAreas;

        return new ProfileOverview(
            CreateAlias(source.SessionId),
            source.Nickname,
            Math.Max(0, source.Level),
            Math.Max(0, source.Experience),
            CountStashItems(source.Items, source.StashId),
            new CurrencyProjection(rubles, dollars, euros),
            source.Traders.OrderBy(trader => trader.TraderId, StringComparer.Ordinal).Select(trader =>
                new TraderLoyaltyProjection(trader.TraderId, Math.Max(0, trader.LoyaltyLevel))).ToArray(),
            source.QuestStatuses.Count(status => status is (int)QuestStatusEnum.Started or (int)QuestStatusEnum.AvailableForFinish),
            source.QuestStatuses.Count(status => status == (int)QuestStatusEnum.Success),
            Math.Max(0, source.ExaminedItemCount),
            new HideoutProjection(
                areas.Count,
                areas.Count(area => area.Level > 0 && !area.Constructing),
                areas.Sum(area => Math.Max(0, area.Level))),
            new SkillProjection(
                source.CommonSkills.Count,
                Math.Max(0, source.MasteringCount),
                source.CommonSkills.Sum(skill => Math.Max(0, skill.Progress))));
    }

    public static string CreateAlias(string sessionId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        return $"profile-{Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant()}";
    }

    private static long CurrencyTotal(IEnumerable<ProfileItemSource> items, string templateId) =>
        items.Where(item => string.Equals(item.TemplateId, templateId, StringComparison.Ordinal))
            .Sum(item => Math.Max(0, item.Count));

    private static int CountStashItems(IReadOnlyList<ProfileItemSource> items, string? stashId)
    {
        if (string.IsNullOrWhiteSpace(stashId))
        {
            return 0;
        }

        var parents = items.ToDictionary(item => item.Id, item => item.ParentId, StringComparer.Ordinal);
        var count = 0;
        foreach (var item in items)
        {
            if (string.Equals(item.Id, stashId, StringComparison.Ordinal))
            {
                continue;
            }

            var parent = item.ParentId;
            for (var depth = 0; depth <= items.Count && parent is not null; depth++)
            {
                if (string.Equals(parent, stashId, StringComparison.Ordinal))
                {
                    count++;
                    break;
                }

                parent = parents.GetValueOrDefault(parent);
            }
        }

        return count;
    }
}

[Injectable]
public sealed class SptProfileOverviewService(ProfileHelper profileHelper)
{
    public ProfileOverview GetOverview(MongoId sessionId)
    {
        if (sessionId.IsEmpty)
        {
            throw new UnauthorizedAccessException("A valid SPT profile session is required.");
        }

        var pmc = profileHelper.GetPmcProfile(sessionId)
            ?? throw new KeyNotFoundException("No PMC profile exists for the current session.");
        var items = pmc.Inventory?.Items ?? [];
        var source = new ProfileProjectionSource(
            sessionId.ToString(),
            pmc.Info?.Nickname ?? "Unknown",
            pmc.Info?.Level ?? 0,
            pmc.Info?.Experience ?? 0,
            pmc.Inventory?.Stash?.ToString(),
            items.Select(item => new ProfileItemSource(
                item.Id.ToString(),
                item.Template.ToString(),
                item.ParentId,
                checked((long)(item.Upd?.StackObjectsCount ?? 1)))).ToArray(),
            (pmc.TradersInfo ?? []).Select(pair =>
                new ProfileTraderSource(pair.Key.ToString(), pair.Value.LoyaltyLevel ?? 0)).ToArray(),
            (pmc.Quests ?? []).Select(quest => (int)quest.Status).ToArray(),
            pmc.Encyclopedia?.Count ?? 0,
            (pmc.Hideout?.Areas ?? []).Select(area =>
                new ProfileHideoutAreaSource(area.Level ?? 0, area.Constructing == true)).ToArray(),
            (pmc.Skills?.Common ?? []).Select(skill => new ProfileSkillSource(skill.Progress)).ToArray(),
            pmc.Skills?.Mastering?.Count() ?? 0);

        return ProfileProjector.Project(source);
    }
}
