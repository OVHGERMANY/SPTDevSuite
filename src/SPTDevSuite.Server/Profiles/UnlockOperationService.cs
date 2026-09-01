using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Modding;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTDevSuite.Contracts;

namespace SPTDevSuite.Server.Profiles;

public sealed record UnlockRollbackPayload(
    DateTimeOffset CreatedUtc,
    string ProfileAlias,
    string PmcJson,
    string Sha256,
    long Length,
    IReadOnlyList<UnlockModule> Modules);

public sealed record UnlockAuditEntry(
    Guid OperationId,
    DateTimeOffset TimestampUtc,
    string ProfileAlias,
    IReadOnlyList<UnlockModule> Modules,
    bool Applied,
    string? BackupKey,
    string? BackupSha256,
    int ChangedCount,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Applies the smallest safe quest-completion representation: every tracked quest and every
/// current quest template is marked successful, without replaying rewards, mail, reputation,
/// or mutually-exclusive branch failures.
/// </summary>
public static class QuestCompletionPolicy
{
    public static int Apply(List<QuestStatus> profileQuests, IEnumerable<MongoId> templateQuestIds, double timestamp)
    {
        ArgumentNullException.ThrowIfNull(profileQuests);
        ArgumentNullException.ThrowIfNull(templateQuestIds);

        var changes = 0;
        var existingIds = new HashSet<MongoId>(profileQuests.Select(quest => quest.QId));
        foreach (var quest in profileQuests)
        {
            if (quest.Status == QuestStatusEnum.Success)
            {
                continue;
            }

            quest.Status = QuestStatusEnum.Success;
            quest.StatusTimers ??= [];
            quest.StatusTimers[QuestStatusEnum.Success] = timestamp;
            quest.AvailableAfter = null;
            changes++;
        }

        foreach (var questId in templateQuestIds)
        {
            if (!existingIds.Add(questId))
            {
                continue;
            }

            profileQuests.Add(new QuestStatus
            {
                QId = questId,
                StartTime = timestamp,
                Status = QuestStatusEnum.Success,
                StatusTimers = new Dictionary<QuestStatusEnum, double>
                {
                    [QuestStatusEnum.AvailableForStart] = timestamp,
                    [QuestStatusEnum.Started] = timestamp,
                    [QuestStatusEnum.AvailableForFinish] = timestamp,
                    [QuestStatusEnum.Success] = timestamp,
                },
                CompletedConditions = [],
                AvailableAfter = null,
            });
            changes++;
        }

        return changes;
    }
}

[Injectable(InjectionType.Singleton)]
public sealed class UnlockOperationService(
    FoundationState foundationState,
    ProfileHelper profileHelper,
    SaveServer saveServer,
    ProfileDataService profileData,
    TemplateTable templateTable,
    TradersTable tradersTable,
    ICloner cloner,
    JsonUtil jsonUtil)
{
    private const string ApplyConfirmation = "APPLY_UNLOCKS";
    private const string CompleteQuestsConfirmation = "COMPLETE_ALL_QUESTS";
    private const string AuditKey = "sptdevsuite-unlock-audit";
    private static readonly HashSet<UnlockModule> SupportedModules =
    [
        UnlockModule.ExamineAllItems,
        UnlockModule.UnlockFlea,
        UnlockModule.MaxProfileLevel,
        UnlockModule.MaxTraders,
        UnlockModule.MaxSkills,
        UnlockModule.CompleteQuests,
    ];
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new(StringComparer.Ordinal);

    public async Task<UnlockOperationResult> ExecuteAsync(
        MongoId sessionId,
        UnlockOperationRequest request,
        CancellationToken cancellationToken)
    {
        foundationState.EnsureWriteCapabilitiesAvailable();

        if (sessionId.IsEmpty)
        {
            throw new UnauthorizedAccessException("A valid SPT profile session is required.");
        }

        var modules = ValidateRequest(request);
        var sessionLock = SessionLocks.GetOrAdd(sessionId.ToString(), static _ => new SemaphoreSlim(1, 1));
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            var live = profileHelper.GetPmcProfile(sessionId)
                ?? throw new KeyNotFoundException("No PMC profile exists for the current session.");
            var candidate = cloner.Clone(live)
                ?? throw new InvalidOperationException("Unable to clone the current PMC profile.");
            var warnings = new List<string>();
            var plannedChanges = ApplyModules(candidate, modules, warnings);
            ValidateCandidate(candidate, live);

            var operationId = Guid.NewGuid();
            if (!request.Apply)
            {
                return new UnlockOperationResult(operationId, false, plannedChanges, 0, null, null, modules, warnings);
            }

            var expectedConfirmation = modules.Contains(UnlockModule.CompleteQuests)
                ? CompleteQuestsConfirmation
                : ApplyConfirmation;
            if (!string.Equals(request.Confirmation, expectedConfirmation, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Applying selected unlocks requires confirmation text {expectedConfirmation}.");
            }

            var liveJson = jsonUtil.Serialize(live)
                ?? throw new InvalidOperationException("Could not serialize the current PMC profile for rollback.");
            ValidateJson(liveJson);
            var backupSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(liveJson)));
            var backupKey = $"sptdevsuite-unlock-backup-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{operationId:N}";
            var alias = ProfileProjector.CreateAlias(sessionId.ToString());
            var rollbackPayload = new UnlockRollbackPayload(
                DateTimeOffset.UtcNow, alias, liveJson, backupSha256, Encoding.UTF8.GetByteCount(liveJson), modules);
            await profileData.SaveProfileDataAsync(sessionId, backupKey, rollbackPayload, cancellationToken);

            var original = cloner.Clone(live)
                ?? throw new InvalidOperationException("Unable to retain an in-memory rollback snapshot.");
            try
            {
                var appliedWarnings = new List<string>();
                var appliedChanges = ApplyModules(live, modules, appliedWarnings);
                ValidateCandidate(live, candidate);
                await saveServer.SaveProfileAsync(sessionId, cancellationToken);
                warnings = warnings.Concat(appliedWarnings).Distinct(StringComparer.Ordinal).ToList();
                await AppendAuditAsync(sessionId, new UnlockAuditEntry(
                    operationId, DateTimeOffset.UtcNow, alias, modules, true, backupKey, backupSha256,
                    appliedChanges, warnings), cancellationToken);
                return new UnlockOperationResult(operationId, true, plannedChanges, appliedChanges,
                    backupKey, backupSha256, modules, warnings);
            }
            catch
            {
                var fullProfile = profileHelper.GetFullProfile(sessionId);
                if (fullProfile.CharacterData is not null)
                {
                    fullProfile.CharacterData.PmcData = original;
                }

                await AppendAuditAsync(sessionId, new UnlockAuditEntry(
                    operationId, DateTimeOffset.UtcNow, alias, modules, false, backupKey, backupSha256,
                    0, ["The operation failed; the in-memory PMC snapshot was restored."]), cancellationToken);
                throw;
            }
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private static IReadOnlyList<UnlockModule> ValidateRequest(UnlockOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestedModules = request.Modules ?? [];
        var modules = requestedModules.Distinct().ToArray();
        if (modules.Length == 0)
        {
            throw new InvalidOperationException("Select at least one unlock module.");
        }

        if (modules.Length != requestedModules.Count)
        {
            throw new InvalidOperationException("Duplicate unlock modules are not allowed.");
        }

        var unsupported = modules.Where(module => !SupportedModules.Contains(module)).ToArray();
        if (unsupported.Length > 0)
        {
            throw new InvalidOperationException($"Unsupported unlock modules: {string.Join(", ", unsupported)}.");
        }

        return modules;
    }

    private int ApplyModules(PmcData pmc, IReadOnlyList<UnlockModule> modules, List<string> warnings)
    {
        var changes = 0;
        foreach (var module in modules)
        {
            changes += module switch
            {
                UnlockModule.ExamineAllItems => ExamineAllItems(pmc),
                UnlockModule.UnlockFlea => UnlockFlea(pmc, warnings),
                UnlockModule.MaxProfileLevel => MaxProfileLevel(pmc),
                UnlockModule.MaxTraders => MaxTraders(pmc, warnings),
                UnlockModule.MaxSkills => MaxSkills(pmc, warnings),
                UnlockModule.CompleteQuests => CompleteQuests(pmc, warnings),
                _ => 0,
            };
        }

        return changes;
    }

    private int CompleteQuests(PmcData pmc, List<string> warnings)
    {
        pmc.Quests ??= [];
        var changes = QuestCompletionPolicy.Apply(
            pmc.Quests,
            templateTable.Quests.Keys,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        warnings.Add("Quest completion marks quest records successful only; rewards, mail, reputation changes, and branch-failure effects are not replayed.");
        return changes;
    }

    private int ExamineAllItems(PmcData pmc)
    {
        pmc.Encyclopedia ??= [];
        var changes = 0;
        foreach (var item in templateTable.Items.Where(pair => !string.Equals(pair.Value.Type, "Node", StringComparison.Ordinal)))
        {
            if (!pmc.Encyclopedia.TryGetValue(item.Key, out var examined) || examined)
            {
                pmc.Encyclopedia[item.Key] = false;
                changes++;
            }
        }

        return changes;
    }

    private static int UnlockFlea(PmcData pmc, List<string> warnings)
    {
        if (pmc.Info is null)
        {
            warnings.Add("Flea unlock skipped: PMC Info is absent.");
            return 0;
        }

        var bans = pmc.Info.Bans?.ToArray() ?? [];
        var retained = bans.Where(ban => ban.BanType != BanType.RagFair).ToArray();
        pmc.Info.Bans = retained;
        return bans.Length - retained.Length;
    }

    private int MaxProfileLevel(PmcData pmc)
    {
        if (pmc.Info is null)
        {
            return 0;
        }

        // Keep the value derived from the SPT level table rather than inventing an XP total.
        var targetLevel = profileHelper.GetMaxLevel();
        var targetExperience = profileHelper.GetExperience(targetLevel) ?? 0;
        var changes = 0;
        if (pmc.Info.Experience != targetExperience) { pmc.Info.Experience = targetExperience; changes++; }
        if (pmc.Info.Level != targetLevel) { pmc.Info.Level = targetLevel; changes++; }
        return changes;
    }

    private int MaxTraders(PmcData pmc, List<string> warnings)
    {
        if (pmc.TradersInfo is null)
        {
            warnings.Add("Trader unlock skipped: PMC TradersInfo is absent.");
            return 0;
        }

        var changes = 0;
        foreach (var (traderId, trader) in tradersTable)
        {
            if (!pmc.TradersInfo.TryGetValue(traderId, out var profileTrader))
            {
                warnings.Add($"Trader {traderId} is absent from the profile and was not created.");
                continue;
            }

            var levels = trader.Base.LoyaltyLevels?.ToArray() ?? [];
            var targetLoyalty = Math.Max(1, levels.Length);
            var targetSales = levels.Length == 0 ? 0d : levels.Max(level => level.MinSalesSum ?? 0d);
            var targetStanding = levels.Length == 0 ? 0d : levels.Max(level => level.MinStanding ?? 0d);
            if (profileTrader.LoyaltyLevel != targetLoyalty) { profileTrader.LoyaltyLevel = targetLoyalty; changes++; }
            if (profileTrader.SalesSum.GetValueOrDefault() < targetSales) { profileTrader.SalesSum = targetSales; changes++; }
            if (profileTrader.Standing.GetValueOrDefault() < targetStanding) { profileTrader.Standing = targetStanding; changes++; }
            if (profileTrader.Unlocked != true) { profileTrader.Unlocked = true; changes++; }
            if (profileTrader.Disabled == true) { profileTrader.Disabled = false; changes++; }
        }

        return changes;
    }

    private static int MaxSkills(PmcData pmc, List<string> warnings)
    {
        if (pmc.Skills?.Common is null)
        {
            warnings.Add("Skill unlock skipped: PMC Skills.Common is absent.");
            return 0;
        }

        var changes = 0;
        foreach (var skill in pmc.Skills.Common)
        {
            if (skill.Progress < CommonSkill.MaxSkillProgress)
            {
                skill.Progress = CommonSkill.MaxSkillProgress;
                changes++;
            }
        }

        return changes;
    }

    private void ValidateCandidate(PmcData candidate, PmcData expected)
    {
        if (candidate.Id != expected.Id || candidate.Info is null || candidate.Inventory?.Stash is null)
        {
            throw new InvalidOperationException("Unlock validation rejected a profile with changed identity or missing inventory roots.");
        }

        ValidateJson(jsonUtil.Serialize(candidate) ?? throw new InvalidOperationException("Unlock validation could not serialize the PMC."));
    }

    private static void ValidateJson(string json)
    {
        using var _ = JsonDocument.Parse(json);
    }

    private async Task AppendAuditAsync(MongoId sessionId, UnlockAuditEntry entry, CancellationToken cancellationToken)
    {
        var entries = await profileData.GetProfileDataAsync<List<UnlockAuditEntry>>(sessionId, AuditKey, cancellationToken) ?? [];
        entries.Add(entry);
        if (entries.Count > 100)
        {
            entries.RemoveRange(0, entries.Count - 100);
        }

        await profileData.SaveProfileDataAsync(sessionId, AuditKey, entries, cancellationToken);
    }
}
