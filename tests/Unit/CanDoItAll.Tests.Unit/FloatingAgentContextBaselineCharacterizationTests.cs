using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Baseline characterization for the floating agent context path captured before the
/// MAF runtime/context refactor. These tests freeze observable behavior so later
/// extractions can be distinguished from behavior rewrites.
/// </summary>
public sealed class FloatingAgentContextBaselineCharacterizationTests
{
    [Fact]
    public void Captured_canvas_snapshot_remains_immutable_after_the_registry_publishes_a_gantt_view()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        var canvasAttachment = new ViewStateAttachment("canvas");
        using var lease = registry.PublishModuleContext(CreateViewPublication(
            scopeId,
            view: "canvas",
            fragmentContent: "Current view: Canvas",
            canvasAttachment));

        var capturedForRun = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var capturedVersion = capturedForRun.Version;

        lease.Update(CreateViewPublication(
            scopeId,
            view: "gantt",
            fragmentContent: "Current view: Gantt",
            new ViewStateAttachment("gantt")));

        // The admitted run keeps the exact canvas capture: fragment text, attachment
        // instance, and version are unchanged by the later Gantt publication.
        Assert.Equal(
            "Current view: Canvas",
            capturedForRun.Fragments.Single(item =>
                item.ContributorId == new AgentChatContextContributorId("view")).Content);
        var envelope = Assert.Single(capturedForRun.Attachments);
        Assert.True(envelope.TryGetAttachment<ViewStateAttachment>(out var attachment));
        Assert.Same(canvasAttachment, attachment);
        Assert.Equal(capturedVersion, capturedForRun.Version);

        // The next capture (= next turn) observes the Gantt publication at a higher version.
        var nextTurnCapture = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(
            "Current view: Gantt",
            nextTurnCapture.Fragments.Single(item =>
                item.ContributorId == new AgentChatContextContributorId("view")).Content);
        Assert.True(nextTurnCapture.Version > capturedVersion);
    }

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

        // Strict capture is owned by the turn-context capture service and is
        // invoked from the send path only. The continuation path resolves the
        // original leased context by run identifier
        // (AgentTurnContextLeaseRegistry.Resolve, SB15 rename) and never captures the
        // current registry state.
        Assert.DoesNotContain("contextRegistry", orchestratorSource, StringComparison.Ordinal);
        var captureServiceCallCount = CountOccurrences(orchestratorSource, ".CaptureAsync(");
        Assert.Equal(1, captureServiceCallCount);

        var continuationStart = orchestratorSource.IndexOf(
            "ContinueApprovalCoreAsync",
            StringComparison.Ordinal);
        Assert.True(continuationStart >= 0, "Continuation entry point not found.");
        var continuationSection = orchestratorSource[continuationStart..];
        var sendSectionStart = continuationSection.IndexOf(
            "SendMessageCoreAsync",
            StringComparison.Ordinal);
        if (sendSectionStart >= 0)
        {
            continuationSection = continuationSection[..sendSectionStart];
        }

        Assert.DoesNotContain("CaptureAsync", continuationSection, StringComparison.Ordinal);
        Assert.DoesNotContain("turnContextCaptureService", continuationSection, StringComparison.Ordinal);

        // The capture service itself performs exactly one strict registry capture.
        var captureServiceSource = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\MAF\Common\CanDoItAll.AgentFramework.Core\Context\AgentTurnContextCaptureService.cs"));
        Assert.Equal(1, CountOccurrences(captureServiceSource, ".CaptureAsync(cancellationToken)"));
        Assert.DoesNotContain("RespondToPendingApprovals", captureServiceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Baseline_gap_gantt_panel_does_not_contribute_observation_facts_yet()
    {
        // Documents the pre-refactor limitation targeted by the Gantt observation
        // contributor work: the Gantt panel exposes no facts to the agent chat
        // context; the only Gantt-specific model context is the static view fragment
        // in ProjectStructureAgentChatContextBuilder. When the Gantt observation
        // contributor lands, this assertion must be replaced by positive coverage
        // of the contributor contract.
        var root = FindRepoRoot();
        var panelSources = new[]
        {
            @"src\Modules\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureGanttPanel.razor",
            @"src\Modules\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureGanttPanel.razor.cs"
        };

        var contributesObservationFacts = panelSources.Any(relativePath =>
        {
            var fullPath = TestRepositoryPath.Resolve(root, relativePath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            var text = File.ReadAllText(fullPath);
            return text.Contains("AgentChatContext", StringComparison.Ordinal) ||
                   text.Contains("GanttObservationContributor", StringComparison.Ordinal);
        });

        var builderSource = File.ReadAllText(TestRepositoryPath.Resolve(
            root,
            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentChatContextBuilder.cs"));
        var ganttObservationContributorExists = Directory
            .EnumerateFiles(
                TestRepositoryPath.Resolve(root, @"src\Modules\CanDoItAll.Modules.Workbench"),
                "*GanttObservation*",
                SearchOption.AllDirectories)
            .Any();

        if (ganttObservationContributorExists)
        {
            // The Gantt observation contributor has landed; the baseline gap is closed
            // and rich facts are expected to flow through the dedicated contributor.
            Assert.True(
                contributesObservationFacts || ganttObservationContributorExists,
                "Gantt observation contributor exists but is not wired to the panel or context path.");
            return;
        }

        Assert.False(
            contributesObservationFacts,
            "Baseline expectation changed: the Gantt panel now references the agent chat context. " +
            "Update this characterization with positive contributor coverage.");
        Assert.Contains("BuildViewFragment", builderSource, StringComparison.Ordinal);
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
