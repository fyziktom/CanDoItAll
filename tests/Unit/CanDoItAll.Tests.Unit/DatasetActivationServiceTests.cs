using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class DatasetActivationServiceTests {
    [Fact]
    public async Task Prepared_plan_survives_restart_and_only_exact_completion_can_activate_it() {
        await using var environment = CanDoItAllTestEnvironment.Create("dataset-activation-resume");
        var profileId = Guid.NewGuid();
        var plan = Bootstrap();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(environment).ReadActiveBindingAsync(profileId));
        using (var first = Service(environment).Acquire(profileId)) {
            Assert.Null(await first.ReadAsync());
            await first.WritePreparedAsync(plan);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(environment).ReadActiveBindingAsync(profileId));
        using (var restarted = Service(environment).Acquire(profileId)) {
            var retained = Assert.IsType<DatasetActivationFileRecord>(await restarted.ReadAsync());
            Assert.Equal(DatasetActivationFileState.Prepared, retained.State);
            Assert.Equal(plan, retained.Plan);
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.WritePreparedAsync(Bootstrap()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.MarkActiveAsync(Bootstrap()));
            await restarted.MarkActiveAsync(plan);
            await restarted.WritePreparedAsync(plan);
            Assert.Equal(DatasetActivationFileState.Active, (await restarted.ReadAsync())!.State);
        }
        Assert.Equal(plan.Binding, await Service(environment).ReadActiveBindingAsync(profileId));
        using var final = Service(environment).Acquire(profileId);
        Assert.Equal(plan, (await final.ReadAsync())!.Plan);
        Assert.Equal(DatasetActivationFileState.Active, (await final.ReadAsync())!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interrupted_file_replacement_preserves_the_exact_previous_recovery_record(bool completing) {
        await using var environment = CanDoItAllTestEnvironment.Create("dataset-activation-interruption");
        var profileId = Guid.NewGuid();
        var original = Bootstrap();
        var replacement = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate,
            Binding(), original.Binding);
        using (var first = Service(environment).Acquire(profileId)) {
            await first.WritePreparedAsync(original);
            if (!completing) {
                await first.MarkActiveAsync(original);
            }
        }
        var failingFiles = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == DurableFileWriteStage.BeforeCommit) {
                throw new IOException("Synthetic activation write interruption.");
            }
        });
        using (var interrupted = Service(environment, failingFiles).Acquire(profileId)) {
            await Assert.ThrowsAsync<IOException>(() => completing
                ? interrupted.MarkActiveAsync(original)
                : interrupted.WritePreparedAsync(replacement));
        }
        using var resumed = Service(environment).Acquire(profileId);
        var retained = (await resumed.ReadAsync())!;
        Assert.Equal(original, retained.Plan);
        Assert.Equal(completing ? DatasetActivationFileState.Prepared : DatasetActivationFileState.Active, retained.State);
        if (completing) {
            await resumed.MarkActiveAsync(original);
        } else {
            await resumed.WritePreparedAsync(replacement);
            await resumed.MarkActiveAsync(replacement);
        }
        Assert.Equal(DatasetActivationFileState.Active, (await resumed.ReadAsync())!.State);
        Assert.Empty(Directory.EnumerateFiles(environment.ControlPlaneRootPath, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Explicit_alias_attachment_preserves_the_SQL_namespace_and_maintenance_identity() {
        await using var environment = CanDoItAllTestEnvironment.Create("dataset-activation-alias");
        var binding = Binding();
        var plan = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Attach, binding, binding);
        using var alias = Service(environment).Acquire(Guid.NewGuid());
        await alias.WritePreparedAsync(plan);
        await alias.MarkActiveAsync(plan);
        var retained = (await alias.ReadAsync())!;
        Assert.Equal(binding, retained.Plan.Binding);
        Assert.Equal(binding, retained.Plan.ExpectedPreviousSqlBinding);
        Assert.Throws<ArgumentException>(() => new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Attach,
            Binding(), binding));
    }

    [Theory]
    [InlineData(ActivationFileCorruption.Malformed)]
    [InlineData(ActivationFileCorruption.Oversize)]
    [InlineData(ActivationFileCorruption.WrongProfile)]
    public async Task Corrupt_or_mismatched_files_fail_explicitly_without_replacing_recovery_evidence(ActivationFileCorruption corruption) {
        await using var environment = CanDoItAllTestEnvironment.Create("dataset-activation-corruption");
        var profileId = Guid.NewGuid();
        var plan = Bootstrap();
        using (var initial = Service(environment).Acquire(profileId)) {
            await initial.WritePreparedAsync(plan);
        }
        var path = Path.Combine(environment.ControlPlaneRootPath, "database-profiles", "dataset-activations", $"{profileId:D}.json");
        var original = await File.ReadAllTextAsync(path);
        var invalid = corruption switch {
            ActivationFileCorruption.Malformed => "{ incomplete",
            ActivationFileCorruption.Oversize => new string(' ', DatasetActivationService.MaximumRecordBytes + 1),
            ActivationFileCorruption.WrongProfile => original.Replace(profileId.ToString("D"), Guid.NewGuid().ToString("D"), StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        await File.WriteAllTextAsync(path, invalid);
        using var restarted = Service(environment).Acquire(profileId);
        await Assert.ThrowsAsync<InvalidDataException>(() => restarted.ReadAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => restarted.WritePreparedAsync(Bootstrap()));
        Assert.Equal(invalid, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Active_binding_cache_is_profile_scoped_avoids_file_locks_and_invalidates_on_explicit_activation() {
        await using var environment = CanDoItAllTestEnvironment.Create("dataset-activation-cache");
        var service = Service(environment);
        var firstProfile = Guid.NewGuid();
        var secondProfile = Guid.NewGuid();
        var first = Bootstrap();
        var second = Bootstrap();
        foreach (var (profile, plan) in new[] { (firstProfile, first), (secondProfile, second) }) {
            using var setup = service.Acquire(profile);
            await setup.WritePreparedAsync(plan);
            await setup.MarkActiveAsync(plan);
        }
        Assert.Equal(first.Binding, await service.ReadActiveBindingAsync(firstProfile));
        Assert.Equal(second.Binding, await service.ReadActiveBindingAsync(secondProfile));
        var replacement = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, Binding(), first.Binding);
        using (var held = service.Acquire(firstProfile)) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            Assert.Equal(first.Binding, await service.ReadActiveBindingAsync(firstProfile, deadline.Token));
            Assert.Equal(second.Binding, await service.ReadActiveBindingAsync(secondProfile, deadline.Token));
            await held.WritePreparedAsync(replacement);
            Assert.Equal(second.Binding, await service.ReadActiveBindingAsync(secondProfile, deadline.Token));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadActiveBindingAsync(firstProfile));
        using (var completion = service.Acquire(firstProfile)) {
            await completion.MarkActiveAsync(replacement);
        }
        Assert.Equal(replacement.Binding, await service.ReadActiveBindingAsync(firstProfile));
        Assert.Equal(second.Binding, await service.ReadActiveBindingAsync(secondProfile));
    }

    [Fact]
    public async Task Disposed_lease_cannot_write_and_releases_the_control_plane_coordination() {
        await using var environment = CanDoItAllTestEnvironment.Create("dataset-activation-dispose");
        var profileId = Guid.NewGuid();
        var plan = Bootstrap();
        using var first = Service(environment).Acquire(profileId);
        await first.WritePreparedAsync(plan);
        first.Dispose();
        first.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first.MarkActiveAsync(plan));
        using var successor = Service(environment).Acquire(profileId);
        Assert.Equal(plan, (await successor.ReadAsync())!.Plan);
        await successor.MarkActiveAsync(plan);
    }

    private static DatasetActivationService Service(CanDoItAllTestEnvironment environment, DurableFileWriter? files = null) {
        files ??= new(new PhysicalFileSystemPathPolicyFactory());
        var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions {
            RootPath = environment.ControlPlaneRootPath
        }), environment.CreateHostEnvironment(nameof(DatasetActivationServiceTests)), files);
        return new(paths, files, new PhysicalFileSystemPathPolicyFactory());
    }

    private static DatasetActivationBinding Binding()
        => new(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(), DatasetLifecycleRecord.CurrentSchemaVersion,
            DatasetGuardProtocol.SharedRows);

    private static DatasetActivationPlan Bootstrap()
        => new(Guid.NewGuid(), DatasetActivationKind.Bootstrap, Binding(), null);

    public enum ActivationFileCorruption { Malformed, Oversize, WrongProfile }
}
