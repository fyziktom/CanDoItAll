using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.ProjectTaskPlanAgentFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanHistoricalReceiptTests {
    [Fact]
    public async Task Registered_historical_review_reads_the_original_plan_without_coordination_or_file_writes() {
        await using var fixture = await CreateAsync(projectContext: true);
        Assert.Equal(ProjectTaskPlanAgentResultKind.Published, Result(await fixture.PublishAsync(await fixture.PrepareAsync())).Kind);
        var run = await fixture.ReadAsync();
        var root = fixture.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        using var observation = await HistoricalFileObservation.StartAsync(root);
        await using (var coordination = new FileStream(Layout(fixture).WorkspaceLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            var review = await ReadAsync(fixture, run);
            Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, review.Stop);
            Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, Assert.Single(review.Observations).Knowledge);
        }
        await observation.AssertNoChangesAsync();
        Assert.Single(fixture.Wire.Requests);
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Unknown_commit_can_be_read_without_reconciliation_or_reopening_original_work() {
        await using var fixture = await CreateAsync(projectContext: true);
        var waiting = await fixture.PrepareAsync();
        fixture.Sql.Faults.Commit.AfterCommit = () => {
            fixture.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("receipt-temporarily-unavailable")) : Task.CompletedTask;
            throw new IOException("commit-acknowledgement-lost");
        };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.PublishAsync(waiting));
        fixture.Sql.Probe.BeforeCommand = null;
        var run = await fixture.ReadAsync();
        Assert.Equal(AgentToolProposalState.ReconciliationRequired,
            AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, Binding(waiting)).State);
        var review = await ReadAsync(fixture, run);
        Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, Assert.Single(review.Observations).Knowledge);
        Assert.Equal(run.ToolAdmission!.PlanningDecision!.Work, (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work);
        Assert.Single(fixture.Wire.Requests);
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Current_source_read_revocation_refuses_the_entire_historical_page() {
        await using var fixture = await CreateAsync(projectContext: true);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var run = await fixture.ReadAsync();
        await RevokeReadAsync(fixture);
        var review = await ReadAsync(fixture, run);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, review.Stop);
        Assert.Empty(review.Observations);
        Assert.Null(review.RunRevision);
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleted_or_recreated_source_cannot_disclose_the_old_receipt(bool recreate) {
        await using var fixture = await CreateAsync(projectContext: true);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var run = await fixture.ReadAsync();
        await new ProjectLifecycleTestDriver(fixture.Sql.Services).DeleteAsync(fixture.Sql.Project.ProjectId);
        if (recreate) {
            await using var context = await fixture.Sql.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            context.Add(new Project { Id = fixture.Sql.Project.ProjectId, Name = "Different historical lifetime", Slug = Guid.NewGuid().ToString("N") });
            await context.SaveChangesAsync();
        }
        var review = await ReadAsync(fixture, run);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, review.Stop);
        Assert.Empty(review.Observations);
        Assert.Null(review.NextCursor);
    }

    [Fact]
    public async Task Owner_outage_does_not_become_absence_or_mutation_authority() {
        await using var fixture = await CreateAsync(projectContext: true);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var run = await fixture.ReadAsync();
        var reads = 0;
        fixture.Sql.Probe.BeforeCommand = (command, _) => {
            if (command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)) {
                reads++;
                throw new IOException("historical-receipt-unavailable");
            }
            return Task.CompletedTask;
        };
        var review = await ReadAsync(fixture, run);
        Assert.Equal(1, reads);
        Assert.Equal(AgentHistoricalReceiptKnowledge.ReadUnavailable, Assert.Single(review.Observations).Knowledge);
        fixture.Sql.Probe.BeforeCommand = null;
        Assert.Single(fixture.Wire.Requests);
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Original_and_refreshed_generations_keep_separate_receipt_identities() {
        await using var fixture = await CreateAsync(projectContext: true);
        var waiting = await fixture.PrepareAsync();
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Sql.Publisher()
            .PublishAsync(await fixture.Sql.CommandAsync(1), fixture.Sql.Invocation));
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged, Result(await fixture.PublishAsync(waiting)).Refusal);
        await fixture.Owner.RefreshAsync(AgentPlanningDecisionExpectation.From(await fixture.ReadAsync()), false, fixture.Participant.CanReadAsync, default);
        var next = await fixture.PrepareAsync();
        await fixture.PublishAsync(next);
        var review = await ReadAsync(fixture, await fixture.ReadAsync());
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, review.Stop);
        Assert.Collection(review.Observations,
            first => {
                Assert.Equal(Binding(waiting), first.Binding);
                Assert.Equal(AgentHistoricalReceiptKnowledge.NotObserved, first.Knowledge);
            },
            second => {
                Assert.Equal(Binding(next), second.Binding);
                Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, second.Knowledge);
            });
        Assert.Equal(2, fixture.Wire.Requests.Count);
        await fixture.Sql.VerifyAsync(2, 2);
    }

    [Fact]
    public async Task Catalog_change_during_owner_lookup_discards_the_entire_page() {
        await using var fixture = await CreateAsync(projectContext: true);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var run = await fixture.ReadAsync();
        var changed = false;
        fixture.Sql.Probe.BeforeCommand = async (command, _) => {
            if (!changed && command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)) {
                changed = true;
                await RevokeReadAsync(fixture);
            }
        };
        var review = await ReadAsync(fixture, run);
        Assert.True(changed);
        Assert.Equal(AgentHistoricalReceiptReviewStop.SnapshotChanged, review.Stop);
        Assert.Empty(review.Observations);
        Assert.Null(review.RunRevision);
        fixture.Sql.Probe.BeforeCommand = null;
        Assert.Single(fixture.Wire.Requests);
    }

    private static async Task RevokeReadAsync(ProjectTaskPlanAgentFixture fixture) {
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id != fixture.Agent.Id ? agent : agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new())
            }).ToArray()
        });
    }

    private static FileSandboxWorkspaceStorageLayout Layout(ProjectTaskPlanAgentFixture fixture)
        => new(fixture.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(fixture.Original.ToolAdmission!.Session.Profile.ProfileId.ToString("N")));

    private static async Task<AgentHistoricalReceiptReview> ReadAsync(ProjectTaskPlanAgentFixture fixture, ExecutionRunRecord run) {
        var path = Layout(fixture).RunPath(run.Id);
        var before = await File.ReadAllBytesAsync(path);
        var review = await fixture.Sql.Services.GetRequiredService<IAgentHistoricalReceiptReviewService>()
            .ReadSavedEffectReceiptsAsync(new(run.Id, run.ChatSessionId, run.AgentId, run.CreatedAtUtc));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        return review;
    }
}
