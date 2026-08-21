using System.Security.Cryptography;
using System.Text.Json;
using SPTDevSuite.Contracts;

namespace SPTDevSuite.Server.Backups;

public interface IAtomicBackupCommitter
{
    void Commit(string temporaryPath, string finalPath);
}

public sealed class FileSystemAtomicBackupCommitter : IAtomicBackupCommitter
{
    public void Commit(string temporaryPath, string finalPath) => File.Move(temporaryPath, finalPath, false);
}

public sealed class AtomicProfileBackupService(IAtomicBackupCommitter? committer = null) : IProfileBackupService
{
    private readonly IAtomicBackupCommitter _committer = committer ?? new FileSystemAtomicBackupCommitter();

    public async Task<BackupValidation> CreateAsync(
        string syntheticProfilePath,
        string backupDirectory,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syntheticProfilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        RejectInstalledProfilePath(syntheticProfilePath);

        var source = Path.GetFullPath(syntheticProfilePath);
        var destination = Path.GetFullPath(backupDirectory);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Synthetic profile does not exist.", source);
        }

        Directory.CreateDirectory(destination);
        var temporaryPath = Path.Combine(destination, $".{Guid.NewGuid():N}.tmp");
        string? finalPath = null;

        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true))
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }

            var preCommitHash = await HashAsync(temporaryPath, cancellationToken);
            await ValidateJsonAsync(temporaryPath, cancellationToken);
            var stamp = timestamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
            finalPath = Path.Combine(destination, $"{stamp}-{preCommitHash[..12].ToLowerInvariant()}-profile.json");
            _committer.Commit(temporaryPath, finalPath);

            var postCommitHash = await HashAsync(finalPath, cancellationToken);
            if (!string.Equals(preCommitHash, postCommitHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Backup hash changed during atomic commit.");
            }

            await ValidateJsonAsync(finalPath, cancellationToken);
            var length = new FileInfo(finalPath).Length;
            return new BackupValidation(finalPath, postCommitHash, length, true, true, timestamp.ToUniversalTime());
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            if (finalPath is not null && File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            throw;
        }
    }

    public static void RejectInstalledProfilePath(string profilePath)
    {
        var normalized = Path.GetFullPath(profilePath).Replace('/', '\\');
        if (normalized.Contains("\\SPT_Runtime\\user\\profiles\\", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Installed SPT profiles are outside this milestone's backup boundary.");
        }
    }

    public IReadOnlyList<string> PlanRetention(
        IReadOnlyList<BackupValidation> existingBackups,
        BackupRetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(existingBackups);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.MaximumBackups < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Retention must preserve at least one backup.");
        }

        var excess = Math.Max(0, existingBackups.Count - policy.MaximumBackups);
        return existingBackups
            .OrderBy(backup => backup.CreatedUtc)
            .ThenBy(backup => backup.BackupPath, StringComparer.Ordinal)
            .Take(excess)
            .Select(backup => backup.BackupPath)
            .ToArray();
    }

    public BackupRollbackPlan PlanRollback(BackupValidation backup, string syntheticTargetPath)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentException.ThrowIfNullOrWhiteSpace(syntheticTargetPath);
        RejectInstalledProfilePath(syntheticTargetPath);
        if (!backup.HashVerified || !backup.JsonValidated)
        {
            throw new InvalidOperationException("Rollback requires a JSON-valid backup with a verified SHA-256.");
        }

        return new BackupRollbackPlan(
            backup.BackupPath,
            Path.GetFullPath(syntheticTargetPath),
            backup.Sha256,
            true,
            true,
            true);
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task ValidateJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A profile backup must contain one JSON object.");
        }
    }
}
