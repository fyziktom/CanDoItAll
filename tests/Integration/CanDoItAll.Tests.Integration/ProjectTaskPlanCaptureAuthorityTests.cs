using System.Data.Common;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanCaptureAuthorityTests {
    [Fact]
    public async Task Sql_capture_holds_the_selected_source_without_blocking_unrelated_agent_edits() {
        var barrier = new CaptureBarrier();
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true, projectCaptureProbe: barrier);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var token = deadline.Token;
        var source = fixture.Launcher!;
        var unrelated = source with { Id = Guid.NewGuid(), Name = "Unrelated capture authority" };
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, unrelated] }, token);
        var writer = new FileSandboxWorkspaceStore(
            fixture.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(), fixture.Scope);
        var parent = fixture.Process.Parent;
        var capturer = fixture.Sql.Services.GetServices<IProcessDecisionMaterialCapturer>()
            .Single(item => item.ContentContract == ProjectTaskPlanProcessContract.Content);
        barrier.Armed = true;
        var capture = capturer.CaptureAsync(new(fixture.Process.Source.SourceAuthority, parent.Scope,
            ProjectTaskPlanProcessContract.Content, new(parent.Scope.Id, 1, new(Guid.NewGuid()))), token);
        try {
            await barrier.Entered.Task.WaitAsync(token);
            await writer.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == unrelated.Id ? agent with { Name = "Edited during SQL capture" } : agent).ToArray()
            }, token);
            using var blocked = CancellationTokenSource.CreateLinkedTokenSource(token);
            blocked.CancelAfter(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == source.Id
                    ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
            }, blocked.Token));
        } finally {
            barrier.Release.TrySetResult();
            await capture;
        }
        var material = Assert.IsType<ProcessDecisionMaterialCaptureResult.Captured>(await capture).Material;
        Assert.Equal(parent.Scope, material.Scope);
        Assert.Equal(ProjectTaskPlanProcessContract.Content, material.ContentContract);
        await writer.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == source.Id
                ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
        }, token);
    }

    private sealed class CaptureBarrier : DbCommandInterceptor {
        internal bool Armed { get; set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.StartsWith("SET LOCAL transaction_timeout", StringComparison.Ordinal)) {
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
