using System.Security.Cryptography;
using SPTDevSuite.Contracts;
using SPTDevSuite.Server.Backups;

namespace SPTDevSuite.Tests;

public sealed class BackupAndSafetyTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(Path.GetTempPath(), $"SPTDevSuite.Tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SyntheticProfileBackupIsCreatedAndHashVerified()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var source = Path.Combine(_temporaryRoot, "synthetic-profile.json");
        var destination = Path.Combine(_temporaryRoot, "backups");
        await File.WriteAllTextAsync(source, "{\"info\":{\"id\":\"synthetic\"},\"characters\":{}}");

        var result = await new AtomicProfileBackupService().CreateAsync(
            source, destination, new DateTimeOffset(2026, 8, 20, 12, 30, 0, TimeSpan.Zero));

        Assert.True(result.HashVerified);
        Assert.True(result.JsonValidated);
        Assert.True(File.Exists(result.BackupPath));
        var actualHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(result.BackupPath)));
        Assert.Equal(actualHash, result.Sha256);
        Assert.Contains("20260820T123000", Path.GetFileName(result.BackupPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AtomicCommitFailureLeavesNoBackupOrTemporaryFile()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var source = Path.Combine(_temporaryRoot, "synthetic-profile.json");
        var destination = Path.Combine(_temporaryRoot, "backups");
        await File.WriteAllTextAsync(source, "{\"synthetic\":true}");
        var service = new AtomicProfileBackupService(new FailingCommitter());

        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(source, destination, DateTimeOffset.UtcNow));

        Assert.Empty(Directory.GetFiles(destination));
    }

    [Fact]
    public void InstalledSptProfilePathIsRejectedBeforeFileAccess()
    {
        var installedProfile = @"E:\Games\SPT\SPT_Runtime\user\profiles\0123456789abcdef01234567.json";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AtomicProfileBackupService.RejectInstalledProfilePath(installedProfile));

        Assert.Contains("outside", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RetentionAndRollbackAreDeterministicReadOnlyPlans()
    {
        var service = new AtomicProfileBackupService();
        var first = Validation("b.json", new DateTimeOffset(2026, 8, 20, 1, 0, 0, TimeSpan.Zero));
        var second = Validation("a.json", first.CreatedUtc);
        var newest = Validation("c.json", first.CreatedUtc.AddMinutes(1));

        var deletions = service.PlanRetention([first, newest, second], new(1));
        var rollback = service.PlanRollback(newest, Path.Combine(_temporaryRoot, "synthetic-target.json"));

        Assert.Equal(["a.json", "b.json"], deletions.Select(Path.GetFileName));
        Assert.True(rollback.ValidateBeforeReplacement);
        Assert.True(rollback.UseAtomicTemporaryFile);
        Assert.True(rollback.RollbackEnabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, true);
        }
    }

    private sealed class FailingCommitter : IAtomicBackupCommitter
    {
        public void Commit(string temporaryPath, string finalPath) => throw new IOException("Synthetic commit failure.");
    }

    private static BackupValidation Validation(string path, DateTimeOffset createdUtc) =>
        new(path, new string('A', 64), 10, true, true, createdUtc);
}
