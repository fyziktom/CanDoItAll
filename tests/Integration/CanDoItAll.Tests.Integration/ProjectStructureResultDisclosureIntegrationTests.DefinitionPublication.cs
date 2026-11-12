using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_definition_holds_original_execution_publication_through_SQL_and_releases_it_after_commit(bool projectScope) {
        var probe = new DefinitionPublicationProbe();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton)
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Original definition under exact live claim");
        await using var fixture = projectScope ? await CreateJournalAsync(services, project) : await CreateDefinitionJournalAsync(services);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, project);
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(project.ProjectId)).Value!;
        var arguments = DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, project.ProjectId,
            new("Accepted under original publication", "", "", "") { ExpectedDefinition = original.Expectation });
        var journal = fixture.NewJournal();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var lease = await journal.AcquireRunAsync(fixture.Session, deadline.Token);
        using var bound = lease.Bind();
        var context = Context(fixture, actor, project.ProjectId);
        var provider = MetadataProvider(services);
        var tools = await provider.CreateToolsAsync(context, deadline.Token);
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
        var payload = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var batch = (await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("protected-definition", payload, false)], deadline.Token)).Batches[^1];
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "protected-definition", payload, deadline.Token);
        using var dispatch = claim.Bind();
        using var effects = AgentToolInvocationEffectScope.Begin();
        probe.Target = project.ProjectId;
        var pending = Assert.Single(tools.OfType<AIFunction>(), item => item.Name == metadata.ToolName)
            .InvokeAsync(new AIFunctionArguments(arguments), deadline.Token).AsTask();
        try {
            await probe.Entered.Task.WaitAsync(deadline.Token);
            using var blocked = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.NewStore().UpdateExecutionRunDetailAsync(
                fixture.Session.ExecutionRunId, detail => detail, blocked.Token));
        } finally {
            probe.Release.TrySetResult();
            await pending;
        }
        var accepted = Assert.IsType<JsonElement>(await pending);
        Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, claim.Binding.IntentId.Value.ToString("D")), effects.CommittedEffect);
        await claim.RequireCurrentIdentityAsync(deadline.Token);
        await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(accepted.GetRawText()), AgentToolEffectState.Committed, deadline.Token);
        await fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail => detail, deadline.Token);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => claim.RequireCurrentIdentityAsync(deadline.Token).AsTask());
        var current = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(project.ProjectId, deadline.Token)).Value!;
        Assert.Equal("Accepted under original publication", current.Scalars.Name);
        Assert.NotEqual(original.Expectation.Revision, current.Expectation.Revision);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        var receipt = await database.Set<ProjectDefinitionReceiptRecord>().SingleAsync(deadline.Token);
        Assert.Equal(claim.Binding.IntentId.Value, receipt.OperationId);
        Assert.Equal(current.Expectation.Revision, receipt.AcceptedRevision);
        Assert.Equal(1, probe.Hits);
    }

    private sealed class DefinitionPublicationProbe : SaveChangesInterceptor {
        private int hits;
        internal Guid Target { get; set; }
        internal int Hits => Volatile.Read(ref hits);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Target != Guid.Empty && eventData.Context is ProjectsDbContext database &&
                database.ChangeTracker.Entries<Project>().Any(entry => entry.Entity.Id == Target && entry.State == EntityState.Modified) &&
                Interlocked.CompareExchange(ref hits, 1, 0) == 0) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
