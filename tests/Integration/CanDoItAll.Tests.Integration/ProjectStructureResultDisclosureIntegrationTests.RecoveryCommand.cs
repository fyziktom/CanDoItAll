using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    public enum RecoverySourceCase { Catalog, Project, Sandbox }

    [Theory]
    [InlineData(RecoverySourceCase.Catalog)]
    [InlineData(RecoverySourceCase.Project)]
    [InlineData(RecoverySourceCase.Sandbox)]
    public async Task Offline_command_uses_original_source_locations_and_refuses_missing_or_held_publications(RecoverySourceCase sourceCase) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var target = await CreateProjectAsync(services, "Captured source recovery");
        var runtime = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        var storage = sourceCase == RecoverySourceCase.Project
            ? WorkspaceScopeDescriptor.Project(target.ProjectId.ToString("N")) : WorkspaceScopeDescriptor.Sandbox;
        await using var journal = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(runtime.ActiveProfileId!.Value, runtime.ActiveFingerprint!, new(runtime.Generation)),
            transientContext: new("Original recovery authority", workspaceScope: WorkspaceScopeDescriptor.Organization(runtime.ActiveProfileId.Value.ToString("N"))),
            storageScope: storage, ownedParentRoot: application.RootPath);
        var completion = await DeleteWithUnconfirmedSourceReleaseAsync(services, journal, target, sourceCase);
        await OfflineMaintenanceCommandFixture.PersistProfileAsync(services);
        var address = completion.Original.Completion;
        var recovery = await ReadSourceRecoveryAsync(services, completion);
        var catalog = recovery.Source switch {
            ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog direct => direct,
            ProjectDeletionSourceRecovery.CanonicalInvocation invocation => invocation.Catalog,
            _ => throw new InvalidDataException("The actual source did not capture its publication locations.")
        };
        var locations = new List<(string Root, WorkspaceScopeDescriptor Scope)> {
            (catalog.RootPath, WorkspaceScopeDescriptor.Organization(target.DatabaseProfileId.ToString("N")))
        };
        if (sourceCase == RecoverySourceCase.Catalog) {
            Assert.IsType<ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog>(recovery.Source);
        } else {
            var invocation = Assert.IsType<ProjectDeletionSourceRecovery.CanonicalInvocation>(recovery.Source);
            Assert.Equal(sourceCase == RecoverySourceCase.Project ? ProjectDeletionExecutionWorkspaceKind.Project : ProjectDeletionExecutionWorkspaceKind.Sandbox,
                invocation.Scope);
            Assert.Equal(journal.WorkspaceRoot, invocation.Execution.RootPath);
            Assert.Equal(storage.Key, invocation.Key);
            Assert.NotEqual(catalog.RootPath, invocation.Execution.RootPath);
            locations.Add((invocation.Execution.RootPath, storage));
        }
        var before = await SourceRecoveryProtectedStateAsync(application, services, locations.Select(item => item.Root));
        var inspected = await OfflineMaintenanceCommandFixture.RunAsync(application, address, "inspect");
        Assert.Equal("Observed", inspected.GetProperty("status").GetString());
        string digest = inspected.GetProperty("result").GetProperty("plan").GetProperty("digest").GetProperty("hex").GetString()!;
        string missingRoot = locations[^1].Root;
        string movedRoot = missingRoot + ".recovery-unavailable-" + Guid.NewGuid().ToString("N");
        RequireOwnedRelocation(application.RootPath, missingRoot, movedRoot);
        await File.WriteAllTextAsync(Path.Combine(application.RootPath, "recovery-source-relocation.json"), JsonSerializer.Serialize(new {
            Owner = nameof(ProjectStructureResultDisclosureIntegrationTests), Original = missingRoot, Temporary = movedRoot,
            State = "PreparedBeforeMove", ParentProcessId = Environment.ProcessId
        }));
        Directory.Move(missingRoot, movedRoot);
        try {
            var absent = await OfflineMaintenanceCommandFixture.RunAsync(application, address, "reconcile", digest, expectedExitCode: 2);
            Assert.Equal("Refused", absent.GetProperty("status").GetString());
            Assert.False(Directory.Exists(missingRoot));
            await RequireHeldGateAsync(services, completion);
        } finally {
            RequireOwnedRelocation(application.RootPath, movedRoot, missingRoot);
            Directory.Move(movedRoot, missingRoot);
        }
        var exclusion = new FileSandboxWorkspaceMaintenanceExclusion(new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory()));
        foreach (var location in locations) {
            await using (await exclusion.AcquireAsync(location.Root, location.Scope)) {
                var busy = await OfflineMaintenanceCommandFixture.RunAsync(application, address, "reconcile", digest, expectedExitCode: 2);
                Assert.Equal("Refused", busy.GetProperty("status").GetString());
                Assert.Contains(busy.GetProperty("result").GetProperty("reason").GetString(), new[] { "ExclusionBusy", "Cancelled" });
                await RequireHeldGateAsync(services, completion);
            }
            Assert.Equal(before, await SourceRecoveryProtectedStateAsync(application, services, locations.Select(item => item.Root)));
        }
        var wrongPlan = await OfflineMaintenanceCommandFixture.RunAsync(application, address, "reconcile", new string('0', 64), expectedExitCode: 2);
        Assert.Equal("PlanChanged", wrongPlan.GetProperty("result").GetProperty("reason").GetString());
        await RequireHeldGateAsync(services, completion);
        var released = await OfflineMaintenanceCommandFixture.RunAsync(application, address, "reconcile", digest);
        Assert.Equal("Released", released.GetProperty("status").GetString());
        Assert.False(released.GetProperty("result").GetProperty("cleanupUnconfirmed").GetBoolean());
        var repeated = await OfflineMaintenanceCommandFixture.RunAsync(application, address, "reconcile", digest);
        Assert.Equal("Released", repeated.GetProperty("status").GetString());
        Assert.Equal(released.GetProperty("result").GetProperty("releasedAtUtc").GetString(),
            repeated.GetProperty("result").GetProperty("releasedAtUtc").GetString());
        Assert.IsType<ProjectDeletionCompletionObservation.Released>(await services.GetRequiredService<IProjectDeletionCompletionQuery>().ObserveAsync(completion));
        Assert.Equal(before, await SourceRecoveryProtectedStateAsync(application, services, locations.Select(item => item.Root)));
    }

    private static async Task<ProjectDeletionCompletionBinding> DeleteWithUnconfirmedSourceReleaseAsync(IServiceProvider services,
        AgentToolAdmissionJournalFixture fixture, ProjectWriteAdmission target, RecoverySourceCase sourceCase) {
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, target);
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(target.ProjectId)).Value!;
        var tool = ProjectStructureToolPolicy.ProjectStructureProjectUpdate;
        var journal = fixture.NewJournal();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var lease = await journal.AcquireRunAsync(fixture.Session, deadline.Token);
        using var bound = lease.Bind();
        var provider = MetadataProvider(services);
        var context = Context(fixture, actor, target.ProjectId);
        await provider.CreateToolsAsync(context, deadline.Token);
        var descriptor = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == tool);
        var arguments = DefinitionArguments(tool, target.ProjectId, new("Original source", "", "", "") { ExpectedDefinition = original.Expectation });
        var payload = descriptor.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
        var source = ProjectDefinitionToolProposalCodec.Read(payload).Source;
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var batch = (await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("recovery-source", payload, false)], deadline.Token)).Batches[^1];
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "recovery-source", payload, deadline.Token);
        using var dispatch = claim.Bind();
        var authorization = services.GetRequiredService<ProjectAgentSourceMutationAuthority>()
            .Bind(ProjectCreatedTargetProvenance.Original(source, tool).Agent!, [target]);
        if (sourceCase != RecoverySourceCase.Catalog) {
            authorization = ProjectGovernedInvocationSource.Bind(authorization,
                await ((IAgentToolAdmissionVerifier)journal).RequireInvocationAsync(fixture.Session, tool, payload.Digest, deadline.Token));
        }
        var fault = new LostSourceReleaseAcknowledgement(authorization.SourceAuthority);
        var delete = await new ProjectLifecycleTestDriver(services).CaptureSourceOwnerAsync(new ProjectLifecycleCommand.Delete(original.Expectation),
            new(fault, authorization.ExpectedProjects), ProjectLifecycleEffectKind.Agent);
        var accepted = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await delete());
        Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, accepted.CompletionIssue);
        Assert.Equal(1, fault.Released);
        var completion = Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(accepted.Result.Acknowledgement).Completion;
        Assert.Equal(2, completion.Obligations.Count);
        await RequireHeldGateAsync(services, completion);
        return completion;
    }

    private static async Task<ProjectDeletionRecoveryEvidence> ReadSourceRecoveryAsync(IServiceProvider services, ProjectDeletionCompletionBinding completion) {
        await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var row = await context.Set<ProjectDeletionCompletionRecord>().AsNoTracking().SingleAsync();
        return ProjectDeletionRecoveryCodec.Read(row.RecoveryPayload, completion.Original);
    }

    private static async Task RequireHeldGateAsync(IServiceProvider services, ProjectDeletionCompletionBinding completion)
        => Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(
            await services.GetRequiredService<IProjectDeletionCompletionQuery>().ObserveAsync(completion));

    private static async Task<string[]> SourceRecoveryProtectedStateAsync(TestApplication application, IServiceProvider services, IEnumerable<string> roots) {
        var files = roots.Append(application.TestEnvironment.ControlPlaneRootPath).Order(StringComparer.Ordinal)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Select(path => path + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).ToList();
        await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        files.Add(await context.Database.SqlQueryRaw<string>("""
            SELECT md5(jsonb_build_array(
                (SELECT jsonb_agg(to_jsonb(a) ORDER BY "Id") FROM "AgentFramework_ProjectAccessRevocations" a),
                (SELECT jsonb_agg(to_jsonb(w) ORDER BY "Id") FROM "Workbench_ProjectCrossModuleMutations" w),
                (SELECT jsonb_agg(to_jsonb(r) ORDER BY "OperationId") FROM "Projects_LifecycleReceipts" r),
                (SELECT jsonb_agg(to_jsonb(p) ORDER BY "ProjectId", "LifetimeId") FROM "Projects_ProjectRetirements" p)
            )::text) AS "Value"
            """).SingleAsync());
        return files.ToArray();
    }

    private static void RequireOwnedRelocation(string owner, string source, string destination) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(owner)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Assert.StartsWith(root, Path.GetFullPath(source), comparison);
        Assert.StartsWith(root, Path.GetFullPath(destination), comparison);
        Assert.True(Directory.Exists(source));
        Assert.False(Directory.Exists(destination));
    }

    private sealed class LostSourceReleaseAcknowledgement(IProjectMutationSourceAuthority original) : IProjectMutationSourceAuthority {
        internal int Released;
        public async Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default)
            => new Held(await original.AcquireAsync(target, cancellationToken), this);

        private sealed class Held(IProjectMutationSourceLease original, LostSourceReleaseAcknowledgement fault) : IProjectMutationSourceLease {
            public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => original.SourceProjects;
            public ProjectDeletionSourceRecovery CaptureDeletionRecoverySource() => original.CaptureDeletionRecoverySource();
            public Task RequireForMutationAsync(CancellationToken cancellationToken = default) => original.RequireForMutationAsync(cancellationToken);
            public Task RequireForDeletionCommitAsync(ProjectDeletionCommitTarget target, CancellationToken cancellationToken = default)
                => original.RequireForDeletionCommitAsync(target, cancellationToken);
            public async ValueTask DisposeAsync() {
                await original.DisposeAsync();
                Interlocked.Increment(ref fault.Released);
                throw new IOException("Injected acknowledgement failure after the actual source lease released.");
            }
        }
    }
}
