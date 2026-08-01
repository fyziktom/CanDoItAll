using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureInvocationSnapshotTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mapper_defensively_copies_and_redacts_the_held_surface_without_deeper_fields()
    {
        var projectId = Guid.NewGuid();
        var root = CreateNode("project:root", "Root");
        var selected = CreateNode(
            "node:selected",
            @"Architecture token=top-secret C:\repo\architecture.md") with
        {
            ParentId = root.Id,
            Notes = "sk-secretvalue must never enter the snapshot",
            MetadataJson = """{"password":"never"}""",
            StorageObjectReferenceJson = """{"token":"never"}""",
            Route = "/projects/private",
            MediaRelativePath = "external-target/C/private/architecture.md",
            MediaOriginalFileName = "architecture.md",
            X = 420,
            Y = 240
        };
        var nodes = new List<ProjectStructureNode> { root, selected };
        var links = new List<ProjectStructureLink>
        {
            new(root.Id, selected.Id, ProjectObjectLinkKind.Contains, false)
        };
        var selections = new List<AgentChatContextEntityReference>
        {
            new("project-node", selected.Id, selected.Title)
        };
        var surface = new ProjectStructureSurface(
            projectId,
            "Delivery password=hidden",
            nodes,
            links,
            null);

        var capture = ProjectStructureInvocationSnapshotMapper.Capture(
            surface,
            ProjectStructureAgentChatView.Canvas,
            selections,
            new DatabaseProfileGeneration(7),
            CapturedAtUtc);
        nodes.Clear();
        links.Clear();
        selections.Clear();

        Assert.Equal(2, capture.Snapshot.Nodes.Length);
        Assert.Single(capture.Snapshot.Links);
        Assert.Collection(
            capture.Snapshot.SelectedNodeIds,
            nodeId => Assert.Equal("node:selected", nodeId));
        Assert.True(capture.Snapshot.Coverage.HasCompleteHierarchy);
        Assert.True(capture.Snapshot.Coverage.HasCompleteLinks);
        Assert.True(capture.Snapshot.Coverage.HasCompleteSelection);
        var capturedSelected = Assert.Single(
            capture.Snapshot.Nodes,
            node => node.Id == "node:selected");
        Assert.Contains("[REDACTED]", capturedSelected.Title, StringComparison.Ordinal);
        Assert.Contains("[PATH OMITTED]", capturedSelected.Title, StringComparison.Ordinal);

        var serialized = JsonSerializer.Serialize(capture.Snapshot);
        Assert.DoesNotContain("top-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secretvalue", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("external-target", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("architecture.md", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"password\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("StorageObjectReference", serialized, StringComparison.Ordinal);
        Assert.Contains(
            ProjectStructureInvocationSnapshotOmission.StorageReferences,
            capture.Snapshot.Coverage.Omissions);
        Assert.Contains(
            ProjectStructureInvocationSnapshotOmission.FileContents,
            capture.Snapshot.Coverage.Omissions);
    }

    [Fact]
    public void Mapper_fingerprints_are_deterministic_and_independent_by_responsibility()
    {
        var projectId = Guid.NewGuid();
        var root = CreateNode("project:root", "Root");
        var child = CreateNode("node:child", "Child") with
        {
            ParentId = root.Id
        };
        ProjectStructureLink[] links =
        [
            new(root.Id, child.Id, ProjectObjectLinkKind.Contains, false)
        ];
        AgentChatContextEntityReference[] selected =
        [
            new("project-node", child.Id, child.Title)
        ];
        var first = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(
                projectId,
                "Delivery",
                [root, child],
                links,
                null),
            ProjectStructureAgentChatView.Canvas,
            selected,
            new DatabaseProfileGeneration(3),
            CapturedAtUtc);
        var reordered = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(
                projectId,
                "Delivery",
                [child, root],
                links.Reverse().ToArray(),
                null),
            ProjectStructureAgentChatView.Canvas,
            selected.Reverse().ToArray(),
            new DatabaseProfileGeneration(3),
            CapturedAtUtc.AddMinutes(1));
        var changedProfile = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(
                projectId,
                "Delivery",
                [root, child],
                links,
                null),
            ProjectStructureAgentChatView.Canvas,
            selected,
            new DatabaseProfileGeneration(4),
            CapturedAtUtc);
        var changedContent = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(
                projectId,
                "Delivery",
                [root, child with { Status = "Ready" }],
                links,
                null),
            ProjectStructureAgentChatView.Canvas,
            selected,
            new DatabaseProfileGeneration(3),
            CapturedAtUtc);

        Assert.Equal(
            first.AttachmentDraft.ContentFingerprint,
            reordered.AttachmentDraft.ContentFingerprint);
        Assert.Equal(
            first.AttachmentDraft.CoverageFingerprint,
            reordered.AttachmentDraft.CoverageFingerprint);
        Assert.Equal(
            first.AttachmentDraft.FreshnessFingerprint,
            reordered.AttachmentDraft.FreshnessFingerprint);
        Assert.Equal(
            first.AttachmentDraft.ContentFingerprint,
            changedProfile.AttachmentDraft.ContentFingerprint);
        Assert.Equal(
            first.AttachmentDraft.CoverageFingerprint,
            changedProfile.AttachmentDraft.CoverageFingerprint);
        Assert.NotEqual(
            first.AttachmentDraft.FreshnessFingerprint,
            changedProfile.AttachmentDraft.FreshnessFingerprint);
        Assert.NotEqual(
            first.AttachmentDraft.ContentFingerprint,
            changedContent.AttachmentDraft.ContentFingerprint);
        Assert.Equal(
            first.AttachmentDraft.CoverageFingerprint,
            changedContent.AttachmentDraft.CoverageFingerprint);
    }

    [Fact]
    public void Mapper_reuses_equivalent_capture_only_before_its_deadline()
    {
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Delivery",
            [CreateNode("project:root", "Root")],
            [],
            null);
        var first = ProjectStructureInvocationSnapshotMapper.Capture(
            surface,
            ProjectStructureAgentChatView.Canvas,
            [],
            new DatabaseProfileGeneration(3),
            CapturedAtUtc);
        var beforeDeadline = ProjectStructureInvocationSnapshotMapper.Capture(
            surface,
            ProjectStructureAgentChatView.Canvas,
            [],
            new DatabaseProfileGeneration(3),
            CapturedAtUtc.AddMinutes(4));
        var atDeadline = ProjectStructureInvocationSnapshotMapper.Capture(
            surface,
            ProjectStructureAgentChatView.Canvas,
            [],
            new DatabaseProfileGeneration(3),
            CapturedAtUtc.AddMinutes(5));

        Assert.Same(
            first,
            ProjectStructureInvocationSnapshotMapper.ReuseCurrent(
                first,
                beforeDeadline,
                CapturedAtUtc.AddMinutes(4)));
        Assert.Same(
            atDeadline,
            ProjectStructureInvocationSnapshotMapper.ReuseCurrent(
                first,
                atDeadline,
                CapturedAtUtc.AddMinutes(5)));
    }

    [Fact]
    public void Mapper_bounds_large_surfaces_and_preserves_selected_exact_nodes()
    {
        var projectId = Guid.NewGuid();
        var nodes = Enumerable.Range(
                0,
                ProjectStructureInvocationSnapshotMapper.MaximumCapturedNodeCount + 1)
            .Select(index => CreateNode($"node:{index:D4}", $"Node {index:D4}"))
            .ToArray();
        var selectedNode = nodes[^1];
        var capture = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(projectId, "Large", nodes, [], null),
            ProjectStructureAgentChatView.Canvas,
            [new AgentChatContextEntityReference(
                "project-node",
                selectedNode.Id,
                selectedNode.Title)],
            new DatabaseProfileGeneration(1),
            CapturedAtUtc);

        Assert.Equal(
            ProjectStructureInvocationSnapshotMapper.MaximumCapturedNodeCount,
            capture.Snapshot.Nodes.Length);
        Assert.False(capture.Snapshot.Coverage.HasCompleteHierarchy);
        Assert.True(capture.Snapshot.Coverage.HasCompleteSelection);
        Assert.Contains(
            capture.Snapshot.Nodes,
            node => node.Id == selectedNode.Id);
        Assert.True(capture.Snapshot.Coverage.HasCompletePriorityDerivation);
    }

    [Fact]
    public void Mapper_uses_canonical_pause_rules_without_exposing_marker_text()
    {
        var projectId = Guid.NewGuid();
        var parent = CreateNode("node:parent", "Parent") with
        {
            Priority = 4,
            MarkerLabel = "Wait for approval"
        };
        var child = CreateNode("node:child", "Child") with
        {
            ParentId = parent.Id,
            Priority = 1
        };

        var capture = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(
                projectId,
                "Priority",
                [parent, child],
                [],
                null),
            ProjectStructureAgentChatView.Canvas,
            [new AgentChatContextEntityReference(
                "project-node",
                parent.Id,
                parent.Title)],
            new DatabaseProfileGeneration(1),
            CapturedAtUtc);

        var capturedParent = Assert.Single(
            capture.Snapshot.Nodes,
            node => node.Id == parent.Id);
        Assert.Equal(4, capturedParent.EffectivePriority);
        Assert.True(capture.Snapshot.Coverage.HasCompletePriorityDerivation);
        Assert.DoesNotContain(
            "Wait for approval",
            JsonSerializer.Serialize(capture.Snapshot),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        ProjectStructureReadSource.ContextDefault,
        AgentRuntimeToolProviderPurpose.InteractiveChat,
        "project-structure",
        false,
        ProjectStructureReadSource.InvocationSnapshot)]
    [InlineData(
        ProjectStructureReadSource.ContextDefault,
        AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
        "project-structure",
        true,
        ProjectStructureReadSource.CanonicalCurrent)]
    [InlineData(
        ProjectStructureReadSource.ContextDefault,
        AgentRuntimeToolProviderPurpose.InteractiveChat,
        "projects",
        false,
        ProjectStructureReadSource.CanonicalCurrent)]
    [InlineData(
        ProjectStructureReadSource.InvocationSnapshot,
        AgentRuntimeToolProviderPurpose.InteractiveChat,
        "project-structure",
        false,
        ProjectStructureReadSource.InvocationSnapshot)]
    [InlineData(
        ProjectStructureReadSource.CanonicalCurrent,
        AgentRuntimeToolProviderPurpose.InteractiveChat,
        "project-structure",
        false,
        ProjectStructureReadSource.CanonicalCurrent)]
    public void Source_policy_resolves_context_default_without_fallback(
        ProjectStructureReadSource requestedSource,
        AgentRuntimeToolProviderPurpose purpose,
        string sourceKind,
        bool isGovernedProcessStep,
        ProjectStructureReadSource expectedSource)
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = sourceKind,
            IsGovernedProcessStep = isGovernedProcessStep
        };

        var effectiveSource =
            ProjectStructureInvocationSnapshotReadDispatcher.ResolveEffectiveSource(
                requestedSource,
                purpose,
                intent);

        Assert.Equal(expectedSource, effectiveSource);
    }

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation, "project-structure", true)]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat, "projects", false)]
    public void Explicit_snapshot_source_fails_closed_outside_interactive_project_context(
        AgentRuntimeToolProviderPurpose purpose,
        string sourceKind,
        bool isGovernedProcessStep)
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = sourceKind,
            IsGovernedProcessStep = isGovernedProcessStep
        };

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureInvocationSnapshotReadDispatcher.ResolveEffectiveSource(
                ProjectStructureReadSource.InvocationSnapshot,
                purpose,
                intent));

        Assert.Equal(
            "ProjectStructureInvocationSnapshotContextIneligible",
            exception.ErrorCode);
    }

    [Fact]
    public async Task Eligible_exact_snapshot_read_performs_zero_canonical_reads()
    {
        var fixture = CreateReadFixture();
        var canonicalReadCount = 0;

        var result = await ProjectStructureInvocationSnapshotReadDispatcher.ReadAsync(
            fixture.Context,
            fixture.Generation,
            CapturedAtUtc.AddMinutes(1),
            fixture.ProjectId,
            new ProjectStructureReadRequest(
                NodeIds: ["node:child"],
                Source: ProjectStructureReadSource.ContextDefault),
            ReadCanonicalAsync,
            CancellationToken.None);

        Assert.Equal(ProjectStructureReadSource.InvocationSnapshot, result.Source);
        Assert.Equal(0, canonicalReadCount);
        var node = Assert.Single(result.Response.Nodes);
        Assert.Equal("node:child", node.Id);
        Assert.Null(node.Notes);
        Assert.Null(node.MetadataJson);
        Assert.Null(node.ActionCapabilities);
        Assert.Contains(
            "CanonicalCurrent",
            Assert.Single(result.Response.Warnings),
            StringComparison.Ordinal);
        return;

        Task<ProjectStructureReadResponse> ReadCanonicalAsync(
            ProjectStructureReadRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref canonicalReadCount);
            return Task.FromResult(CreateCanonicalResponse(fixture.ProjectId));
        }
    }

    [Fact]
    public async Task Snapshot_coverage_miss_fails_without_canonical_read()
    {
        var fixture = CreateReadFixture();
        var canonicalReadCount = 0;

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            ProjectStructureInvocationSnapshotReadDispatcher.ReadAsync(
                fixture.Context,
                fixture.Generation,
                CapturedAtUtc.AddMinutes(1),
                fixture.ProjectId,
                new ProjectStructureReadRequest(
                    NodeIds: ["node:child"],
                    IncludeNotes: true,
                    Source: ProjectStructureReadSource.InvocationSnapshot),
                ReadCanonicalAsync,
                CancellationToken.None));

        Assert.Equal(
            "ProjectStructureInvocationSnapshotCoverageInsufficient",
            exception.ErrorCode);
        Assert.Equal(0, canonicalReadCount);
        return;

        Task<ProjectStructureReadResponse> ReadCanonicalAsync(
            ProjectStructureReadRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref canonicalReadCount);
            return Task.FromResult(CreateCanonicalResponse(fixture.ProjectId));
        }
    }

    [Fact]
    public async Task Canonical_current_performs_exactly_one_canonical_read()
    {
        var fixture = CreateReadFixture();
        var canonicalReadCount = 0;

        var result = await ProjectStructureInvocationSnapshotReadDispatcher.ReadAsync(
            fixture.Context,
            fixture.Generation,
            CapturedAtUtc.AddMinutes(1),
            fixture.ProjectId,
            new ProjectStructureReadRequest(
                IncludeNotes: true,
                Source: ProjectStructureReadSource.CanonicalCurrent),
            ReadCanonicalAsync,
            CancellationToken.None);

        Assert.Equal(ProjectStructureReadSource.CanonicalCurrent, result.Source);
        Assert.Equal(1, canonicalReadCount);
        Assert.Equal("Canonical", result.Response.ProjectName);
        return;

        Task<ProjectStructureReadResponse> ReadCanonicalAsync(
            ProjectStructureReadRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref canonicalReadCount);
            Assert.Equal(ProjectStructureReadSource.CanonicalCurrent, request.Source);
            return Task.FromResult(CreateCanonicalResponse(fixture.ProjectId));
        }
    }

    [Theory]
    [InlineData(true, false, "ProjectStructureInvocationSnapshotProfileMismatch")]
    [InlineData(false, true, "ProjectStructureInvocationSnapshotExpired")]
    public async Task Freshness_mismatch_fails_without_canonical_read(
        bool changeProfile,
        bool expire,
        string expectedErrorCode)
    {
        var fixture = CreateReadFixture();
        var canonicalReadCount = 0;
        var generation = changeProfile
            ? new DatabaseProfileGeneration(fixture.Generation.Value + 1)
            : fixture.Generation;
        var nowUtc = expire
            ? CapturedAtUtc.Add(ProjectStructureInvocationSnapshotMapper.FreshnessLifetime)
            : CapturedAtUtc.AddMinutes(1);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            ProjectStructureInvocationSnapshotReadDispatcher.ReadAsync(
                fixture.Context,
                generation,
                nowUtc,
                fixture.ProjectId,
                new ProjectStructureReadRequest(
                    NodeIds: ["node:child"],
                    Source: ProjectStructureReadSource.InvocationSnapshot),
                ReadCanonicalAsync,
                CancellationToken.None));

        Assert.Equal(expectedErrorCode, exception.ErrorCode);
        Assert.Equal(0, canonicalReadCount);
        return;

        Task<ProjectStructureReadResponse> ReadCanonicalAsync(
            ProjectStructureReadRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref canonicalReadCount);
            return Task.FromResult(CreateCanonicalResponse(fixture.ProjectId));
        }
    }

    private static ReadFixture CreateReadFixture()
    {
        var projectId = Guid.NewGuid();
        var generation = new DatabaseProfileGeneration(9);
        var root = CreateNode("project:root", "Root");
        var child = CreateNode("node:child", "Child") with
        {
            ParentId = root.Id,
            Notes = "Canonical-only notes"
        };
        var capture = ProjectStructureInvocationSnapshotMapper.Capture(
            new ProjectStructureSurface(
                projectId,
                "Snapshot",
                [root, child],
                [new ProjectStructureLink(
                    root.Id,
                    child.Id,
                    ProjectObjectLinkKind.Contains,
                    false)],
                null),
            ProjectStructureAgentChatView.Canvas,
            [new AgentChatContextEntityReference(
                "project-node",
                child.Id,
                child.Title)],
            generation,
            CapturedAtUtc);
        var scopeId = AgentChatContextScopeId.Create();
        var source = ProjectStructureAgentChatContextBuilder.BuildSource(projectId);
        var envelope = capture.AttachmentDraft.CreateEnvelope(
            scopeId,
            source,
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            new AgentChatContextContributorId(
                ProjectStructureAgentChatContextBuilder.SelectionContributorId),
            new ModulePublicationRevision(1));
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = ProjectStructureAgentChatContextBuilder.SourceKind,
            SourceId = projectId.ToString("D"),
            WorkspaceScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"))
        };
        var context = new ProjectStructureInvocationSnapshotReadContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            ImmutableArray.Create(envelope),
            ImmutableArray.Create(envelope));
        return new ReadFixture(projectId, generation, context);
    }

    private static ProjectStructureReadResponse CreateCanonicalResponse(Guid projectId)
    {
        return new ProjectStructureReadResponse(
            projectId,
            "Canonical",
            [],
            [],
            []);
    }

    private static ProjectStructureNode CreateNode(string id, string title)
    {
        return new ProjectStructureNode(
            id,
            null,
            ProjectObjectType.Note,
            "note",
            title,
            string.Empty,
            "Draft",
            string.Empty,
            string.Empty,
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("pill", "#64748b", "NT", "Note"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
    }

    private sealed record ReadFixture(
        Guid ProjectId,
        DatabaseProfileGeneration Generation,
        ProjectStructureInvocationSnapshotReadContext Context);
}
