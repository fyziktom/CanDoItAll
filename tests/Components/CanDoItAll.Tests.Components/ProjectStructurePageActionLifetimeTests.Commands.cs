using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageActionLifetimeTests {
    public enum CommandBoundary { Allowed, BeforeSubmission, AfterSql, AfterCommit, WriteDenied }

    [Theory]
    [InlineData(false, CommandBoundary.Allowed)]
    [InlineData(false, CommandBoundary.BeforeSubmission)]
    [InlineData(false, CommandBoundary.AfterSql)]
    [InlineData(false, CommandBoundary.AfterCommit)]
    [InlineData(false, CommandBoundary.WriteDenied)]
    [InlineData(true, CommandBoundary.Allowed)]
    [InlineData(true, CommandBoundary.BeforeSubmission)]
    [InlineData(true, CommandBoundary.AfterSql)]
    [InlineData(true, CommandBoundary.AfterCommit)]
    [InlineData(true, CommandBoundary.WriteDenied)]
    public async Task Materializing_native_prompt_command_checks_original_structure_authority_and_keeps_exact_commit(bool wizard, CommandBoundary boundary) {
        var access = new CommandAccess();
        var probe = new CommandAuthorityProbe(access, boundary);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access));
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(probe).Options));
        });
        var target = await CreateUnboundPromptCommandTargetAsync(harness);
        Assert.Null(target.Node.ArtifactId);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        var action = wizard ? "command:wizard" : "command:open";
        Assert.Contains(cut.FindComponent<ProjectStructureSelectionPanel>().Instance.State.SelectedNode!.Actions, offered => offered.ActionId == action);
        var visibleBefore = JsonSerializer.Serialize(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes);
        probe.Project = target.ProjectId;
        probe.Armed = boundary is CommandBoundary.AfterSql or CommandBoundary.AfterCommit;
        access.Inner.Allowed = boundary != CommandBoundary.BeforeSubmission;
        access.WriteAllowed = boundary != CommandBoundary.WriteDenied;
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector(action));
        var committed = boundary is CommandBoundary.Allowed or CommandBoundary.AfterCommit;
        var stored = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id);
        await using var prompts = await harness.Context.Services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync();
        var artifacts = await prompts.Set<PromptArtifact>().AsNoTracking().Where(prompt => prompt.ProjectId == target.ProjectId).ToListAsync();
        if (committed) {
            var artifact = Assert.Single(artifacts);
            Assert.Equal(artifact.Id, stored.ArtifactId);
            Assert.Equal(target.Node.Title, artifact.Title);
            Assert.Equal(target.Node.Notes, artifact.Summary);
            var receipt = Assert.Single(cut.Instance.CommittedInsightCommands);
            Assert.Equal(artifact.Id, receipt.Artifact!.EntityId);
            Assert.Equal(target.Admission, receipt.Admission);
            Assert.Equal(target.Node.Id, receipt.NodeId);
        } else {
            Assert.Empty(artifacts);
            Assert.Equal(target.Node.ArtifactId, stored.ArtifactId);
            Assert.Equal(target.Node.Route, stored.Route);
            Assert.Empty(cut.Instance.CommittedInsightCommands);
        }
        if (boundary is CommandBoundary.AfterSql or CommandBoundary.AfterCommit) {
            Assert.True(probe.Reached);
        }
        if (boundary != CommandBoundary.Allowed) {
            Assert.Equal(visibleBefore, JsonSerializer.Serialize(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
            AssertNavigation(harness, target.ProjectId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_prompt_command_rejects_a_changed_original_title_or_binding_without_replaying_materialization(bool peerBinding) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var target = await CreateUnboundPromptCommandTargetAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        if (peerBinding) {
            Assert.NotNull(await Workbench(harness).ExecuteNodeCommandAsync(target.ProjectId, target.Node.Id, ProjectStructureCommandKind.Open,
                expectedState: ProjectStructureNodeStateGuard.Capture(target.Node, target.Admission)));
        } else {
            await using var database = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var peer = await database.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == target.ProjectId && node.NodeKey == target.Node.Id);
            peer.Title = "Peer materialization title";
            await database.SaveChangesAsync();
        }
        var before = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id);
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("command:open"));
        var after = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.Empty(cut.Instance.CommittedInsightCommands);
        await using var prompts = await harness.Context.Services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync();
        Assert.Equal(peerBinding ? 1 : 0, await prompts.Set<PromptArtifact>().CountAsync(prompt => prompt.ProjectId == target.ProjectId));
        AssertNavigation(harness, target.ProjectId);
    }

    private static async Task<Target> CreateUnboundPromptCommandTargetAsync(ComponentTestHarness harness) {
        var target = await CreateTargetAsync(harness, "Original unbound prompt");
        await using var database = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var unbound = await database.Set<ProjectObjectRecord>().SingleAsync(node => node.ProjectId == target.ProjectId && node.NodeKey == target.Node.Id);
        unbound.ObjectType = ProjectObjectType.PromptFlow;
        await database.SaveChangesAsync();
        return target with { Node = Assert.Single((await Workbench(harness).GetStructureAsync(target.ProjectId)).Nodes, node => node.Id == target.Node.Id) };
    }

    [Fact]
    public async Task Readonly_native_test_command_does_not_require_structure_write_permission() {
        var access = new CommandAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access)));
        var target = await CreateTargetAsync(harness, "Read only native command");
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        Assert.Contains(cut.FindComponent<ProjectStructureSelectionPanel>().Instance.State.SelectedNode!.Actions, offered => offered.ActionId == "command:test");
        access.WriteAllowed = false;
        await InvokeSelectionAsync(cut, new InsightsSelectionCommand.Inspector("command:test"));
        Assert.Contains("/test-lab?projectId=", harness.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Single(cut.Instance.CommittedInsightCommands);
    }

    private sealed class CommandAccess : IEditingUiAccess {
        public EditingUiTestAccess Inner { get; } = new();
        public bool WriteAllowed { get; set; } = true;
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken)
            => Inner.OpenAsync(topic, cancellationToken);
        public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
            await Inner.ValidateAsync(grant, write, cancellationToken);
            if (write && !WriteAllowed) {
                throw new UnauthorizedAccessException("Original Structure write is denied.");
            }
        }
    }

    private sealed class CommandAuthorityProbe(CommandAccess access, CommandBoundary boundary) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        private DbContext? matched;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext db && db.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(entry => entry.Entity.ProjectId == Project && entry.Entity.ObjectType == ProjectObjectType.PromptFlow)) {
                Assert.NotNull(db.Database.CurrentTransaction);
                matched = db;
                if (boundary == CommandBoundary.AfterSql) {
                    Revoke();
                }
            }
            return ValueTask.FromResult(result);
        }
        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed && boundary == CommandBoundary.AfterCommit && ReferenceEquals(matched, eventData.Context)) {
                Revoke();
            }
            return Task.CompletedTask;
        }
        private void Revoke() {
            Armed = false;
            Reached = true;
            access.Inner.Allowed = false;
        }
    }
}
