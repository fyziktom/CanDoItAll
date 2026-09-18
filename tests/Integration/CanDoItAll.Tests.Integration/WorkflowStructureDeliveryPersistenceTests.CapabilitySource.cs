using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Global_workflow_needs_its_current_original_capability_but_no_unrelated_Structure_grant(bool removeCatalogItem) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var services = fixture.Services;
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var writer = new FileSandboxWorkspaceStore(profile.Profile.Profile.Storage.WorkspaceRoot,
            WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")),
            new AgentProjectAccessCatalogPolicy(services.GetRequiredService<ProjectWriteAdmissionService>(), profile.Profile.Profile.Id));
        var current = await writer.LoadCatalogAsync();
        var seededCapability = Assert.Single(current.Capabilities, item => item.Kind == CapabilityKind.Tool && item.Key == WorkflowRuntimeCapabilityKeys.RunStart);
        var capability = seededCapability with { Id = Guid.NewGuid() };
        var agent = current.Agents.First() with { Id = Guid.NewGuid(), Name = "Global Workflow source", IsTemplate = false,
            TemplateKey = string.Empty, ConfigurationJson = "{}", Status = AgentLifecycleStatus.Active,
            Permissions = AgentPermissionsPolicy.Default with { CanUseTools = true },
            Capabilities = [new(capability.Id, capability.Key, capability.Kind, capability.ProofStatus, capability.LastVerifiedAtUtc, capability.ProofNotes)] };
        await writer.UpdateCatalogAsync(catalog => catalog with {
            Capabilities = catalog.Capabilities.Select(item => item.Id == seededCapability.Id ? capability : item).ToArray(),
            Agents = [.. catalog.Agents, agent]
        });
        var admittedCatalog = await writer.LoadCatalogAsync();
        Assert.Equal(capability.Id, Assert.Single(admittedCatalog.Capabilities,
            item => item.Kind == CapabilityKind.Tool && item.Key == WorkflowRuntimeCapabilityKeys.RunStart).Id);
        Assert.Contains(admittedCatalog.Agents.Single(item => item.Id == agent.Id).Capabilities,
            item => item.CapabilityId == capability.Id);
        var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), agent.Id, profile.Profile.Profile.Id,
            services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(),
            WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")), true, true, "fixture-v1", "fixture-policy",
            [WorkflowToolPolicy.WorkflowsRunStart], [WorkflowRuntimeCapabilityKeys.RunStart]);
        var authorityService = services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var authority = await authorityService.CaptureAgentAsync(agent, governance);
        authority = authority with { ProjectScope = new(authority.ProjectScope!.Projects, authority.ProjectScope.AdmissionProjectIds,
            workflowStartCapabilityId: capability.Id) };
        Assert.False(authority.CanCreateTasks);
        Assert.False(authority.CanCreateAssets);
        Assert.Empty(authority.ProjectScope.Projects);
        var now = DateTimeOffset.UtcNow;
        var run = new WorkflowRunSnapshot(WorkflowRunId.New(), fixture.Definition.Id, fixture.Definition.VersionId,
            WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "global-source", "Started", now, now) {
            Origin = new WorkflowLaunchOrigin.AgentRuntimeInvocation(authority.Principal, new("global-source-session"),
                AgentRuntimeToolProviderPurpose.InteractiveChat.ToString(), new(Guid.NewGuid())) { StructureAuthority = authority }
        };
        var store = services.GetRequiredService<IWorkflowRunStore>();
        var deniedGovernance = new AgentExecutionGovernanceSnapshot(governance.AuthorityId, governance.AgentId, governance.DatabaseProfileId,
            governance.DatabaseProfileGeneration, governance.WorkspaceScope, governance.ReadAllowed, governance.MutationAllowed,
            governance.PolicyVersion, governance.PolicyFingerprint, [WorkflowToolPolicy.WorkflowsRunStatusGet],
            [WorkflowRuntimeCapabilityKeys.RunStart]);
        var denied = run with { RunId = WorkflowRunId.New(), Origin = run.Origin! with {
            StructureAuthority = authority with { AgentGovernance = deniedGovernance }
        } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateRunWithStartedEventAsync(denied,
            new(Guid.NewGuid(), denied.RunId, WorkflowEventKind.Started, null, "Denied", "{}", now)));
        Assert.Null(await store.GetRunAsync(denied.RunId));
        Assert.Empty(await store.ListEventsAsync(denied.RunId));
        await store.CreateRunWithStartedEventAsync(run, new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Started", "{}", now));
        Assert.Equal(run.RunId, (await store.GetRunAsync(run.RunId))!.RunId);
        await writer.UpdateCatalogAsync(catalog => removeCatalogItem
            ? catalog with { Capabilities = catalog.Capabilities.Where(item => item.Id != capability.Id).ToArray() }
            : catalog with { Agents = catalog.Agents.Select(item => item.Id == agent.Id ? item with { Capabilities = [] } : item).ToArray() });
        var changedCatalog = await writer.LoadCatalogAsync();
        if (removeCatalogItem) {
            Assert.DoesNotContain(changedCatalog.Capabilities, item => item.Id == capability.Id);
            Assert.Equal(seededCapability.Id, Assert.Single(changedCatalog.Capabilities,
                item => item.Kind == CapabilityKind.Tool && item.Key == WorkflowRuntimeCapabilityKeys.RunStart).Id);
        } else {
            Assert.Contains(changedCatalog.Capabilities, item => item.Id == capability.Id);
        }
        Assert.DoesNotContain(changedCatalog.Agents.Single(item => item.Id == agent.Id).Capabilities,
            item => item.CapabilityId == capability.Id);
        var next = run with { RunId = WorkflowRunId.New() };
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => store.CreateRunWithStartedEventAsync(next,
            new(Guid.NewGuid(), next.RunId, WorkflowEventKind.Started, null, "Started", "{}", now)));
        Assert.Null(await store.GetRunAsync(next.RunId));
        Assert.Empty(await store.ListEventsAsync(next.RunId));
        await using var disclosure = await authorityService.AcquireAsync(authority, WorkflowStructureAuthorityUse.Disclosure);
        Assert.Equal(run.RunId, (await store.GetRunAsync(run.RunId))!.RunId);
    }

    [Fact]
    public async Task Fresh_SaveRun_admission_rechecks_source_after_actual_insert_before_commit() {
        var policy = new MutableApiPolicy();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            RemoveAutomaticDelivery(services);
            services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<CanDoItAll.Modules.Workspace.ApiAccess.ApiAccessOptions>>(policy);
        } });
        await using var fixture = await CreateFixtureAsync(app, sourceSurface: WorkflowStructureOperatorSurface.Api);
        var original = Assert.IsType<WorkflowRunSnapshot>(await fixture.Services.GetRequiredService<IWorkflowRunStore>()
            .GetRunAsync(fixture.Plan.Identity.Occurrence.RunId));
        var next = original with { RunId = WorkflowRunId.New() };
        var revoke = new RevokeRunAfterFlush(policy, next.RunId);
        var owner = new PersistentWorkflowRunStore(new WorkflowFactory(WorkflowOptions(fixture.Services, revoke)),
            fixture.Services.GetRequiredService<IWorkflowScheduledSourceAuthorityPolicy>(),
            fixture.Services.GetRequiredService<CoordinatedDatabaseTransaction>(),
            fixture.Services.GetRequiredService<IWorkflowStructureSourceAuthorityPolicy>());
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.SaveRunAsync(next));
        Assert.True(revoke.SawInsertedRun);
        Assert.Null(await owner.GetRunAsync(next.RunId));
        Assert.Equal(original.RunId, (await owner.GetRunAsync(original.RunId))!.RunId);
    }

    private sealed class RevokeRunAfterFlush(MutableApiPolicy policy, WorkflowRunId runId) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor {
        public bool SawInsertedRun { get; private set; }

        public override async ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkflowDbContext owner && owner.Database.CurrentTransaction is not null) {
                SawInsertedRun = await owner.Set<WorkflowRunRecordEntity>().AnyAsync(row => row.RunId == runId.Value, cancellationToken);
                if (SawInsertedRun) {
                    policy.CurrentValue.Enabled = false;
                }
            }
            return result;
        }
    }
}
