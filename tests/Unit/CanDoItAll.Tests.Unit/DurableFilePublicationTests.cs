using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.Storage;

[Trait("Category", "HostPlatform")]
public sealed class DurableFilePublicationTests(ITestOutputHelper output) {
    [Fact]
    public async Task Existing_pending_evidence_blocks_its_target_before_stale_cleanup_and_leaves_other_targets_writable() {
        await using var environment = CanDoItAllTestEnvironment.Create("durable-publication-pending-owner");
        string root = environment.ControlPlaneRootPath;
        string target = Path.Combine(root, "authority.json");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        await writer.WriteTextAsync(root, target, "original");
        string pending = Path.Combine(root, $".authority.json.{Guid.NewGuid():N}.pending");
        string stale = Path.Combine(root, $".authority.json.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(pending, "uncertain");
        await File.WriteAllTextAsync(stale, "abandoned");

        var writeFailure = await Assert.ThrowsAsync<DurableFilePublicationRecoveryException>(() =>
            writer.WriteTextAsync(root, target, "later"));
        var deleteFailure = await Assert.ThrowsAsync<DurableFilePublicationRecoveryException>(() =>
            writer.DeleteAsync(root, target));
        await writer.WriteTextAsync(root, Path.Combine(root, "other.json"), "independent");

        Assert.Equal(pending, writeFailure.RecoveryFilePath);
        Assert.Equal(pending, deleteFailure.RecoveryFilePath);
        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Equal("uncertain", await File.ReadAllTextAsync(pending));
        Assert.Equal("abandoned", await File.ReadAllTextAsync(stale));
        Assert.Equal("independent", await File.ReadAllTextAsync(Path.Combine(root, "other.json")));
    }

    [Theory]
    [InlineData(1176, false)]
    [InlineData(1176, true)]
    [InlineData(1177, false)]
    [InlineData(1177, true)]
    public async Task Partial_replacement_preserves_pending_payload_and_blocks_subsequent_mutation(int nativeError, bool backup) {
        if (!OperatingSystem.IsWindows()) {
            output.WriteLine("Affirmative replacement-failure execution requires Windows; the Unix rename path is unchanged.");
            return;
        }

        await using var environment = CanDoItAllTestEnvironment.Create("durable-publication-partial");
        string root = environment.ControlPlaneRootPath;
        string target = Path.Combine(root, "authority.json");
        var factory = new PhysicalFileSystemPathPolicyFactory();
        var writer = new DurableFileWriter(factory);
        await writer.WriteTextAsync(root, target, "original");
        var attempts = 0;
        var injected = new IOException("Injected partial replacement.", unchecked((int)0x80070000) | nativeError);
        var failing = new DurableFileWriter(factory, null, (_, destination, backupPath) => {
            attempts++;
            Assert.Equal(target, destination);
            Assert.Equal(backup ? target + ".bak" : null, backupPath);
            if (nativeError == 1176 && !backup) {
                File.Delete(destination);
            } else if (nativeError == 1177) {
                File.Move(destination, backupPath ?? target + ".displaced");
            }

            throw injected;
        });

        var failure = await Assert.ThrowsAsync<DurableFilePublicationRecoveryException>(() =>
            failing.WriteTextAsync(root, target, "replacement", DurableFileWriteOptions.Default with { CreateBackup = backup }));

        Assert.Same(injected, failure.InnerException);
        Assert.Equal(1, attempts);
        Assert.Equal("replacement", await File.ReadAllTextAsync(failure.RecoveryFilePath));
        Assert.EndsWith(".pending", failure.RecoveryFilePath, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp"));
        var nextOwner = new DurableFileWriter(factory);
        var refusedWrite = await Assert.ThrowsAsync<DurableFilePublicationRecoveryException>(() =>
            nextOwner.WriteTextAsync(root, target, "later"));
        var refusedDelete = await Assert.ThrowsAsync<DurableFilePublicationRecoveryException>(() =>
            nextOwner.DeleteAsync(root, target));
        Assert.Equal(failure.RecoveryFilePath, refusedWrite.RecoveryFilePath);
        Assert.Equal(failure.RecoveryFilePath, refusedDelete.RecoveryFilePath);
        Assert.Equal("replacement", await File.ReadAllTextAsync(failure.RecoveryFilePath));
        if (nativeError == 1176 && backup) {
            Assert.Equal("original", await File.ReadAllTextAsync(target));
        } else {
            Assert.False(File.Exists(target));
        }
    }

    [Fact]
    public async Task Nontransition_failure_retries_the_same_staging_file_then_cleans_only_that_attempt() {
        if (!OperatingSystem.IsWindows()) {
            output.WriteLine("Affirmative replacement-failure execution requires Windows; the Unix rename path is unchanged.");
            return;
        }

        await using var environment = CanDoItAllTestEnvironment.Create("durable-publication-nontransition");
        string root = environment.ControlPlaneRootPath;
        string target = Path.Combine(root, "authority.json");
        var factory = new PhysicalFileSystemPathPolicyFactory();
        await new DurableFileWriter(factory).WriteTextAsync(root, target, "original");
        var stagedPaths = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var writer = new DurableFileWriter(factory, null, (source, _, _) => {
            stagedPaths.Add(source);
            if (stagedPaths.Count == 2) {
                cancellation.Cancel();
            }

            throw new IOException("Injected sharing violation.", unchecked((int)0x80070020));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.WriteTextAsync(root, target, "replacement", cancellationToken: cancellation.Token));

        Assert.Equal(2, stagedPaths.Count);
        Assert.Single(stagedPaths.Distinct(StringComparer.Ordinal));
        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(root, ".*.pending"));
        Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp"));
    }

    [Fact]
    public async Task Create_new_never_overwrites_an_existing_target() {
        await using var environment = CanDoItAllTestEnvironment.Create("durable-publication-create-new");
        string root = environment.ControlPlaneRootPath;
        string target = Path.Combine(root, "authority.json");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        await writer.WriteTextAsync(root, target, "original", DurableFileWriteOptions.CreateNew);

        await Assert.ThrowsAsync<IOException>(() =>
            writer.WriteTextAsync(root, target, "replacement", DurableFileWriteOptions.CreateNew));

        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(root, ".*.pending"));
        Assert.Empty(Directory.EnumerateFiles(root, ".*.tmp"));
    }

    [Fact]
    public async Task Unclassified_replacement_failure_preserves_pending_bytes() {
        if (!OperatingSystem.IsWindows()) {
            output.WriteLine("Affirmative replacement-failure execution requires Windows; the Unix rename path is unchanged.");
            return;
        }

        await using var environment = CanDoItAllTestEnvironment.Create("durable-publication-unclassified");
        string root = environment.ControlPlaneRootPath;
        string target = Path.Combine(root, "authority.json");
        var factory = new PhysicalFileSystemPathPolicyFactory();
        await new DurableFileWriter(factory).WriteTextAsync(root, target, "original");
        var injected = new InvalidOperationException("Injected unknown publication state.");
        var writer = new DurableFileWriter(factory, null, (_, _, _) => throw injected);

        var failure = await Assert.ThrowsAsync<DurableFilePublicationRecoveryException>(() =>
            writer.WriteTextAsync(root, target, "replacement"));

        Assert.Same(injected, failure.InnerException);
        Assert.Equal("replacement", await File.ReadAllTextAsync(failure.RecoveryFilePath));
        Assert.Equal("original", await File.ReadAllTextAsync(target));
    }
}
