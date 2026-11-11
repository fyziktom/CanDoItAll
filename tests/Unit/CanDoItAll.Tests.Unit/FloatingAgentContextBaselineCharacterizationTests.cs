using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Guards floating-agent context capture and the ownership boundaries around runtime invocation.
/// </summary>
public sealed class FloatingAgentContextBaselineCharacterizationTests
{
    [Fact]
    public void Composed_transient_context_digest_is_not_affected_by_later_view_publications()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        using var lease = registry.PublishModuleContext(CreateViewPublication(
            scopeId,
            view: "canvas",
            fragmentContent: "Current view: Canvas",
            new ViewStateAttachment("canvas")));

        var captured = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var transientContext = Assert.IsType<AgentRuntimeTransientContext>(
            AgentChatContextContributionComposer.Compose(captured, Guid.NewGuid()));
        var digestBeforeSwitch = AgentChatContextDigest.Compute(transientContext);

        lease.Update(CreateViewPublication(
            scopeId,
            view: "gantt",
            fragmentContent: "Current view: Gantt",
            new ViewStateAttachment("gantt")));

        Assert.Equal(digestBeforeSwitch, AgentChatContextDigest.Compute(transientContext));
    }

    [Fact]
    public void Context_publication_and_navigation_paths_cannot_invoke_the_agent_runtime()
    {
        var root = FindRepoRoot();
        var navigationOwnedSources = new[]
        {
            @"src\MAF\Common\CanDoItAll.AgentFramework.Core\Context\AgentChatContextRegistry.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Components\AgentChatContextSurfaceProvider.razor",
            @"src\Modules\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureAgentChatContextProvider.razor"
        };

        foreach (var relativePath in navigationOwnedSources)
        {
            var text = File.ReadAllText(TestRepositoryPath.Resolve(root, relativePath));
            Assert.DoesNotContain("IAgentRuntime", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".RunAsync(", text, StringComparison.Ordinal);
            Assert.DoesNotContain("RespondToPendingApprovalsAsync", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Approval_continuation_path_does_not_recapture_current_ui_context()
    {
        var root = FindRepoRoot();
        var orchestratorSource = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.AgentFramework\Services\AgentChatExecutionOrchestrator.cs"));

        var executorSource = File.ReadAllText(TestRepositoryPath.Resolve(root,
            "src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatOperationExecutor.cs"));
        Assert.DoesNotContain("contextRegistry", executorSource, StringComparison.Ordinal);
        Assert.Contains(".CaptureAsync(command, observation, cancellationToken)", executorSource, StringComparison.Ordinal);

        var admission = Between(orchestratorSource, "public AgentChatOperationHandle StartApprovalContinuation(",
            "public (AgentExecutionActivityStreamId StreamId, Task<ExecutionRunResult> Completion) StartRunRecovery(");
        var execution = Between(executorSource, "private async Task<AgentChatRunResult> ContinueApprovalCoreAsync(",
            "private async Task<ExecutionRunResult> RecoverRunCoreAsync(");
        foreach (var continuation in new[] { admission, execution }) {
            Assert.DoesNotContain("CaptureAsync", continuation, StringComparison.Ordinal);
            Assert.DoesNotContain("CaptureObservation", continuation, StringComparison.Ordinal);
            Assert.DoesNotContain("contextRegistry", continuation, StringComparison.Ordinal);
            Assert.DoesNotContain("turnContextCaptureService", continuation, StringComparison.Ordinal);
        }

        // The capture service itself performs exactly one strict registry capture.
        var captureServiceSource = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\MAF\Common\CanDoItAll.AgentFramework.Core\Context\AgentTurnContextCaptureService.cs"));
        Assert.Equal(1, CountOccurrences(captureServiceSource, ".CaptureAsync(cancellationToken)"));
        Assert.DoesNotContain("RespondToPendingApprovals", captureServiceSource, StringComparison.Ordinal);
    }

    private static string Between(string source, string start, string end) {
        var first = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0, $"Entry point not found: {start}");
        var last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
        Assert.True(last > first, $"Next entry point not found: {end}");
        return source[first..last];
    }

    private static AgentChatContextPublication CreateViewPublication(
        AgentChatContextScopeId scopeId,
        string view,
        string fragmentContent,
        IAgentChatContextAttachment attachment)
    {
        var scope = new AgentChatContextScope(
            scopeId,
            new AgentChatContextSource(
                new AgentChatContextSourceKind("project-structure"),
                new AgentChatContextSourceId("project-1")),
            "Project structure",
            WorkspaceScopeDescriptor.Project("project-1"),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            surfacePosition: new AgentChatSurfacePosition(
                "workbench",
                "project-structure",
                view,
                "/projects/project-1/structure",
                new AgentChatContextEntityReference("view", view, view)));
        return new AgentChatContextPublication(
            scope,
            [
                new AgentChatContextContributorPublication(
                    new AgentChatContextFragment(
                        new AgentChatContextContributorId("view"),
                        0,
                        fragmentContent),
                    [
                        new AgentChatContextAttachmentDraft(
                            new AgentChatContextAttachmentKind("workbench.view-state"),
                            new SnapshotContentFingerprint($"content-{view}"),
                            new SnapshotCoverageFingerprint($"coverage-{view}"),
                            new DatabaseProfileGeneration(1),
                            new SnapshotFreshnessFingerprint($"freshness-{view}"),
                            new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
                            new DateTimeOffset(2026, 8, 6, 12, 5, 0, TimeSpan.Zero),
                            attachment)
                    ])
            ]);
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed record ViewStateAttachment(string View) : IAgentChatContextAttachment;
}
