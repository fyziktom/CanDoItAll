using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanAdmissionTests {
    [Fact]
    public async Task Registered_planning_participant_does_not_construct_the_workspace_UI_owner() {
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IWorkspaceDefaultsOwner>();
                services.AddScoped<IWorkspaceDefaultsOwner>(_ => throw new InvalidOperationException("Planning must not construct UI/provider administration."));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectTaskPlanAgentParticipant>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_root_policy_never_invents_a_default(bool persistedLegacyHead) {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        var owner = Settings(fixture);
        if (persistedLegacyHead) {
            var current = await owner.ReadAsync(default);
            Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.ReplaceAsync(new(current.Expectation, current.Values), default));
        } else {
            await using var database = await fixture.Sql.Services.GetRequiredService<IDbContextFactory<WorkspaceSettingsDbContext>>().CreateDbContextAsync();
            await database.Set<WorkspaceSettings>().Where(row => row.IsCurrent).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsCurrent, false));
        }
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => PrepareAsync(fixture));
        Assert.Equal("planning.workspace-policy-missing", failure.Code);
        Assert.Empty(fixture.Wire.Requests);
        SameRun(fixture.Original, await fixture.ReadAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_captures_persisted_policy_head_catalog_and_override_before_real_session_binding(bool useOverride) {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        var root = await ConfigureAsync(fixture);
        var policyOverride = new DecisionDataPolicyOverride(ReadBasisMode.RunSnapshot, MaximumWorkDuration: TimeSpan.FromMinutes(30));
        if (useOverride) {
            await OverrideAsync(fixture, policyOverride);
        }
        var preparation = await PreparationAsync(fixture);
        var admission = await fixture.Participant.PrepareRunAsync(preparation, default);
        Assert.Equal(root.Expectation.Namespace.Value, admission.Provenance.WorkspaceNamespaceId);
        Assert.Equal(root.Expectation.Head!.Revision, admission.Provenance.WorkspaceHeadRevision);
        Assert.Equal(preparation.CatalogRevision.Value, admission.Provenance.CatalogDataRevision);
        Assert.Equal(useOverride ? policyOverride : null, admission.Provenance.AgentOverride);
        Assert.Equal(root.DecisionDataPolicy, admission.Provenance.WorkspacePolicy);
        Assert.Equal(useOverride ? ReadBasisMode.RunSnapshot : ReadBasisMode.StepSnapshot, admission.Provenance.Resolve().ReadBasisMode);
        Assert.Equal(TimeSpan.FromMinutes(useOverride ? 10 : 5), admission.Provenance.Resolve().Budget.MaximumWorkDuration);

        var detail = (await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id))!;
        var runId = Guid.NewGuid();
        var chat = detail.ChatSession! with { Id = Guid.NewGuid(), LatestExecutionRunId = runId };
        var run = fixture.Original with { Id = runId, ChatSessionId = chat.Id, ToolAdmission = null };
        await using (var held = await fixture.Sql.Services.GetRequiredService<IAgentCatalogReadLeaseStore>().AcquireAgentReadLeaseAsync(fixture.Agent.Id)) {
            var journal = fixture.Journal.CreateForNewPlanningRun(run, chat, fixture.Original.ToolAdmission!.OriginalInput!, admission);
            var target = ProjectTaskPlanAgentCodec.Target(journal.PlanningDecision!);
            Assert.Equal(run.Id, target.Session.Reference.ExecutionRunId);
            Assert.Equal(chat.Id, target.Session.Reference.ChatSessionId);
            Assert.Equal(preparation.Governance.AuthorityId, target.Session.Reference.AuthorityId);
            Assert.Equal(fixture.Sql.Project, target.Project);
            Assert.Equal(admission.Provenance, journal.PlanningDecision!.Provenance);
        }
        Assert.Empty(fixture.Wire.Requests);
        SameRun(fixture.Original, await fixture.ReadAsync());
    }

    [Fact]
    public async Task Stale_prepared_catalog_is_refused_instead_of_recapturing_larger_grants() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        var preparation = await PreparationAsync(fixture);
        await OverrideAsync(fixture, new(ReadBasisMode.RunSnapshot));
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Participant.PrepareRunAsync(preparation, default));
        Assert.Equal("planning.catalog-changed", failure.Code);
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Fresh_catalog_with_revoked_source_write_is_refused() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new())
            } : agent).ToArray()
        });
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => PrepareAsync(fixture));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Owner_refuses_an_override_that_relaxes_current_publication() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        await OverrideAsync(fixture, new(PublicationFreshness: PublicationFreshness.PinnedObservation));
        await Assert.ThrowsAsync<DecisionDataPolicyException>(() => PrepareAsync(fixture));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Different_source_identity_or_kind_cannot_reuse_original_authority(bool changeKind) {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        var preparation = await PreparationAsync(fixture);
        var source = preparation.Source;
        var changed = new AgentTurnContextReference(source.TurnContextId, source.ContextEpochId,
            changeKind ? new("untrusted-planning-source") : source.SourceKind,
            changeKind ? source.SourceId : new(Guid.NewGuid().ToString("D")),
            source.Surface, source.View, source.ObservationVersion, source.ModelContextDigest, source.CapturedAtUtc);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Participant.PrepareRunAsync(preparation with { Source = changed }, default));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Changed_profile_fingerprint_cannot_bind_a_policy_from_the_current_database() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        var preparation = await PreparationAsync(fixture);
        var original = preparation.Profile;
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Participant.PrepareRunAsync(preparation with {
            Profile = new(original.ProfileId, "changed-profile", original.Generation)
        }, default));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Deleted_source_lifetime_is_not_replaced_during_admission() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        await new ProjectLifecycleTestDriver(fixture.Sql.Services).DeleteAsync(fixture.Sql.Project.ProjectId);
        var preparation = await PreparationAsync(fixture);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => fixture.Participant.PrepareRunAsync(preparation, default));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Capturing_or_preparing_a_basis_cannot_cross_the_original_policy_dataset() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        var waiting = await fixture.PrepareAsync();
        var decision = waiting.ToolAdmission!.PlanningDecision!;
        var changed = decision with { Provenance = decision.Provenance! with { WorkspaceNamespaceId = Guid.NewGuid() } };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Participant.CaptureBasisAsync(changed, default));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectTaskPlanAgentCodec.Prepare(changed,
            decision.Current.Bases.Single(), ProjectTaskPlanAgentFixture.Draft));
        Assert.Single(fixture.Wire.Requests);
        SameRun(waiting, await fixture.ReadAsync());
    }

    [Fact]
    public async Task Invalid_persisted_root_policy_is_an_explicit_failure() {
        await using var fixture = await ProjectTaskPlanAgentFixture.CreateAsync(projectContext: true);
        await ConfigureAsync(fixture);
        await using (var database = await fixture.Sql.Services.GetRequiredService<IDbContextFactory<WorkspaceSettingsDbContext>>().CreateDbContextAsync()) {
            await database.Set<WorkspaceSettings>().Where(row => row.IsCurrent)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.DecisionDataPolicyJson, "{\"version\":999}"));
        }
        await Assert.ThrowsAsync<WorkspaceSettingsPolicyUnavailableException>(() => PrepareAsync(fixture));
        Assert.Empty(fixture.Wire.Requests);
    }

    private static void SameRun(ExecutionRunRecord expected, ExecutionRunRecord observed)
        => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(observed));

    private static WorkspaceSettingsWriter Settings(ProjectTaskPlanAgentFixture fixture)
        => fixture.Sql.Services.GetRequiredService<WorkspaceSettingsWriter>();

    private static async Task<WorkspaceSettingsSnapshotV2> ConfigureAsync(ProjectTaskPlanAgentFixture fixture) {
        var owner = Settings(fixture);
        var current = await owner.ReadV2Async(default);
        return Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.ReplaceV2Async(new(current.Expectation,
            current.Values, DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10))), default)).Snapshot;
    }

    private static async Task<AgentPlanningRunPreparation> PreparationAsync(ProjectTaskPlanAgentFixture fixture) {
        var catalog = (await fixture.Store.LoadCatalogSnapshotAsync()).Catalog;
        return new(catalog.Agents.Single(agent => agent.Id == fixture.Agent.Id), catalog.CatalogDataRevision,
            fixture.Original.ToolAdmission!.Session.Profile,
            AgentTurnContextMetadata.TryReadTurnContextReference(fixture.Original.MetadataJson)!,
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Original.MetadataJson)!);
    }

    private static async Task<AgentPlanningRunAdmission> PrepareAsync(ProjectTaskPlanAgentFixture fixture)
        => await fixture.Participant.PrepareRunAsync(await PreparationAsync(fixture), default);

    private static async Task OverrideAsync(ProjectTaskPlanAgentFixture fixture, DecisionDataPolicyOverride policy) {
        var preparation = await PreparationAsync(fixture);
        var profile = preparation.Profile;
        var workspace = new AgentExecutionActivityWorkspaceIdentity(profile.ProfileId,
            WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N")), profile.Generation);
        Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Store.ReplaceAgentDecisionDataPolicyAsync(
            new(Guid.NewGuid(), new(workspace, fixture.Agent.Id, preparation.CatalogRevision), policy), () => { }));
    }
}
