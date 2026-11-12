using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Fact]
    public async Task Held_governed_source_retains_catalog_and_original_execution_locations_in_owner_completion() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var target = await CreateProjectAsync(services, "Original release recovery target");
        await using var fixture = await CreateDefinitionJournalAsync(services);
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
        var arguments = DefinitionArguments(tool, target.ProjectId,
            new("Retained source", "", "", "") { ExpectedDefinition = original.Expectation });
        var payload = descriptor.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
        var source = ProjectDefinitionToolProposalCodec.Read(payload).Source;
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var batch = (await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("original-source", payload, false)], deadline.Token)).Batches[^1];
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "original-source", payload, deadline.Token);
        using var dispatch = claim.Bind();
        var admitted = await ((IAgentToolAdmissionVerifier)journal).RequireInvocationAsync(fixture.Session, tool, payload.Digest, deadline.Token);
        var authorization = ProjectGovernedInvocationSource.Bind(
            services.GetRequiredService<ProjectAgentSourceMutationAuthority>().Bind(
                ProjectCreatedTargetProvenance.Original(source, tool).Agent!, [target]), admitted);

        // This fixture drives the existing source/owner seam; the product registers no project-delete tool.
        var delete = await new ProjectLifecycleTestDriver(services).CaptureSourceOwnerAsync(
            new ProjectLifecycleCommand.Delete(original.Expectation), authorization, ProjectLifecycleEffectKind.Agent);
        Assert.IsType<ProjectLifecycleOutcome.Accepted>(await delete());
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        var row = await database.Set<ProjectDeletionCompletionRecord>().AsNoTracking().SingleAsync(deadline.Token);
        var preparation = new ProjectDeletionPreparationContext(new(row.DatabaseProfileId, new(row.NamespaceId), new(row.CompletionId)), target);
        var recovery = ProjectDeletionRecoveryCodec.Read(row.RecoveryPayload, preparation);
        var ordinary = Assert.IsType<ProjectDeletionOriginEvidence.Ordinary>(recovery.Origin);
        var receipt = await database.Set<ProjectLifecycleReceiptRecord>().AsNoTracking().SingleAsync(deadline.Token);
        Assert.Equal(receipt.CallerKind, ordinary.CallerExclusion!.Kind);
        Assert.Equal(receipt.CallerKey, ordinary.CallerExclusion.Key.ToArray());
        Assert.Equal(receipt.OperationId, ordinary.Submission.Operation.Id);
        Assert.Equal(receipt.Epoch, ordinary.Submission.Operation.Epoch.Value);
        Assert.Equal(receipt.NamespaceId, ordinary.Submission.Operation.Namespace.Value);
        Assert.Equal(ProjectMutationScopeKeys.ForLifecycleOperation(ordinary.Submission.Operation, receipt.CallerKind,
            new(receipt.CallerKey)), ordinary.RequireOperationKey());
        var publication = Assert.IsType<ProjectDeletionSourceRecovery.CanonicalInvocation>(recovery.Source);
        Assert.Equal(Path.GetFullPath(fixture.WorkspaceRoot), publication.Execution.RootPath);
        Assert.Equal(ProjectDeletionExecutionWorkspaceKind.Sandbox, publication.Scope);
        Assert.Empty(publication.Key);
        Assert.Equal(target.DatabaseProfileId, publication.Catalog.DatabaseProfileId);
        Assert.NotEqual(publication.Catalog.RootPath, publication.Execution.RootPath);
        Assert.Equal(ProjectDeletionCompletionReleaseState.Released, row.ReleaseState);
        await fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail => detail, deadline.Token);
    }
}
