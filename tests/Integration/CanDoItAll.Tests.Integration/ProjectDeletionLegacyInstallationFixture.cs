using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectDeletionLegacyInstallationFixture : DbCommandInterceptor, IProjectDeletionRecoveryEnvironment, IAsyncDisposable {
    internal DatasetActivationTestFixture Data { get; }
    internal ProjectDeletionPreparationContext Original { get; }
    internal ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog Source { get; }
    internal CoordinatedDatabaseTransaction Transactions { get; }
    internal DbContextOptions<ProjectsDbContext> Options { get; }
    internal IProjectDeletionLegacyParticipant[] Participants { get; }
    internal bool SourceHeld { get; private set; }
    internal int SourceAcquisitions { get; private set; }
    internal bool FailSecondBinding;
    internal bool LoseCommitAcknowledgement;
    internal bool CommitAcknowledgementLost;
    internal bool FailObservationAfterCommit;
    internal bool FailSourceRelease;
    internal Func<Task>? BeforeSource;
    private readonly FileSandboxWorkspaceMaintenanceExclusion workspaces = new(new(new PhysicalFileSystemPathPolicyFactory()));
    private WorkspaceScopeDescriptor Scope => WorkspaceScopeDescriptor.Organization(Data.Profile.Profile.Id.ToString("N"));

    private ProjectDeletionLegacyInstallationFixture(DatasetActivationTestFixture data, ProjectDeletionPreparationContext original) {
        Data = data;
        Original = original;
        Source = new(data.Profile.Profile.Id, HostBoundPathPolicy.BindCurrent(data.Environment.RootPath, DateTimeOffset.UtcNow));
        Directory.CreateDirectory(Scope.ResolveDataRoot(Source.RootPath));
        Transactions = CoordinatedDatabaseTransaction.ForProfile(data.Profile);
        string connection = new NpgsqlConnectionStringBuilder(data.Profile.ConnectionString) { Pooling = false, Timeout = 5, CommandTimeout = 5 }.ConnectionString;
        Options = new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(connection).AddInterceptors(this, new CommitFault(this)).Options;
        Participants = [
            new AgentProjectDeletionRecoveryInspector(new DbContextOptionsBuilder<AgentProjectAccessDbContext>().UseNpgsql(connection)
                .AddInterceptors(this).Options, Transactions),
            new ProjectWorkbenchDeletionRecoveryInspector(new DbContextOptionsBuilder<WorkbenchDbContext>().UseNpgsql(connection)
                .AddInterceptors(this).Options, Transactions)
        ];
    }

    internal ProjectDeletionLegacyInstaller Installer => new(Data.Profile, Options, Transactions, Data.Files(), this,
        () => Source, Participants, TimeProvider.System, NullLogger.Instance);
    internal ProjectDeletionRecoveryCoordinator Recovery => new(Data.Profile, Options, Transactions, Data.Files(), this,
        Participants, TimeProvider.System, NullLogger<ProjectDeletionRecoveryCoordinator>.Instance);

    internal static async Task<ProjectDeletionLegacyInstallationFixture> CreateAsync() {
        var data = await DatasetActivationTestFixture.CreateAsync();
        try {
            return new(data, await ProjectDeletionLegacyTestState.SeedAsync(data));
        } catch {
            await data.DisposeAsync();
            throw;
        }
    }

    internal async Task<ProjectDeletionLegacyPlan> InspectAsync()
        => Assert.IsType<ProjectDeletionRecoveryResult.LegacyObserved>(await Installer.InspectAsync(Original)).Plan;

    internal Task<ProjectDeletionRecoveryResult> InstallAsync(ProjectDeletionRecoveryDigest digest)
        => Installer.InstallAsync(Original, digest, ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped);

    public ProjectDeletionRecoveryDigest ProfileIdentity { get; } = new(new string('a', 64));

    public Task RequireOriginalProfileAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (CommitAcknowledgementLost && FailObservationAfterCommit) {
            throw new IOException("Synthetic independent installation observation failure.");
        }
        return Task.CompletedTask;
    }

    public Task RequireOriginalSourceAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Equal(Source, source);
        Source.RequireCurrentHost();
        if (!Directory.Exists(Scope.ResolveDataRoot(Source.RootPath))) {
            throw new IOException("The original canonical organization workspace is unavailable.");
        }
        return Task.CompletedTask;
    }

    public async ValueTask<IAsyncDisposable> AcquireSourceExclusionAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken) {
        await RequireOriginalSourceAsync(source, cancellationToken);
        Assert.False(SourceHeld);
        if (BeforeSource is { } action) {
            BeforeSource = null;
            await action();
        }
        var held = await workspaces.AcquireAsync(Source.RootPath, Scope, cancellationToken);
        SourceHeld = true;
        SourceAcquisitions++;
        return new SourceLease(this, held);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        if (command.CommandText.StartsWith("UPDATE \"AgentFramework_ProjectAccessRevocations\"", StringComparison.Ordinal) ||
                command.CommandText.StartsWith("UPDATE \"Workbench_ProjectCrossModuleMutations\"", StringComparison.Ordinal)) {
            Assert.True(SourceHeld);
            if (FailSecondBinding && command.CommandText.StartsWith("UPDATE \"Workbench_", StringComparison.Ordinal)) {
                throw new IOException("Synthetic second native binding failure.");
            }
        }
        return ValueTask.FromResult(result);
    }

    public ValueTask DisposeAsync() => Data.DisposeAsync();

    private sealed class SourceLease(ProjectDeletionLegacyInstallationFixture fixture, IAsyncDisposable owned) : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            await owned.DisposeAsync();
            fixture.SourceHeld = false;
            if (fixture.FailSourceRelease) {
                throw new IOException("Synthetic terminal source release acknowledgement failure.");
            }
        }
    }

    private sealed class CommitFault(ProjectDeletionLegacyInstallationFixture fixture) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (fixture.LoseCommitAcknowledgement) {
                fixture.CommitAcknowledgementLost = true;
                throw new TimeoutException("Synthetic lost acknowledgement after the actual installation commit.");
            }
            return Task.CompletedTask;
        }
    }
}
