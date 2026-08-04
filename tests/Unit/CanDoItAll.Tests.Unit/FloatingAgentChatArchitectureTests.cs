using System.Collections.Concurrent;
using System.Text.Json;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentChatContextRegistryTests
{
    [Fact]
    public void Scope_access_state_is_preserved_and_fails_closed_until_ready()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agentId = Guid.NewGuid();
        var baseScope = CreateScope("Project structure");
        var scope = new AgentChatContextScope(
            baseScope.Id,
            baseScope.Source,
            baseScope.DisplayName,
            baseScope.WorkspaceScope,
            [new AgentChatContextAgentAccess(agentId, AgentChatContextPermission.Read, "Project")],
            AgentChatContextScopeAccessMode.AllowListed,
            AgentChatContextAccessState.Loading);
        using var lease = registry.ActivateScope(scope);

        var loading = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(AgentChatContextAccessState.Loading, loading.Scope.AccessState);
        Assert.False(loading.CanRead(agentId));

        lease.Update(new AgentChatContextScope(
            scope.Id,
            scope.Source,
            scope.DisplayName,
            scope.WorkspaceScope,
            scope.AgentAccess,
            scope.AccessMode,
            AgentChatContextAccessState.Ready));

        var ready = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.True(ready.CanRead(agentId));
    }

    [Fact]
    public void Scope_lease_updates_and_removes_the_active_scope()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scope = CreateScope("Project structure");
        using var lease = registry.ActivateScope(scope);

        lease.Update(CreateScope("Project gantt", scope.Id));

        var updated = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(scope.Id, updated.Scope.Id);
        Assert.Equal("Project gantt", updated.Scope.DisplayName);

        lease.Dispose();

        Assert.Null(registry.Capture());
    }

    [Fact]
    public void Replacing_scope_invalidates_the_old_lease_without_allowing_it_to_remove_the_replacement()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var firstScope = CreateScope("Project structure");
        var firstLease = registry.ActivateScope(firstScope);
        var replacementScope = CreateScope("CRM partner");
        using var replacementLease = registry.ActivateScope(replacementScope);

        Assert.Throws<InvalidOperationException>(() =>
            firstLease.Update(CreateScope("Stale project structure", firstScope.Id)));

        firstLease.Dispose();

        var captured = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        Assert.Equal(replacementScope.Id, captured.Scope.Id);
        Assert.Equal("CRM partner", captured.Scope.DisplayName);
    }

    [Fact]
    public void Fragment_leases_update_remove_and_order_fragments_deterministically()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scope = CreateScope("Project structure");
        using var scopeLease = registry.ActivateScope(scope);
        using var lastLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("z-selection", 10, "Last"));
        var firstLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("b-project", 5, "First"));
        using var middleLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("a-node", 10, "Middle"));

        Assert.Equal(
            ["b-project", "a-node", "z-selection"],
            CaptureContributorIds(registry));

        lastLease.Update(CreateFragment("z-selection", 0, "Updated first"));
        Assert.Equal(
            ["z-selection", "b-project", "a-node"],
            CaptureContributorIds(registry));
        Assert.Equal("Updated first", registry.Capture()!.Fragments[0].Content);

        firstLease.Dispose();

        Assert.Equal(
            ["z-selection", "a-node"],
            CaptureContributorIds(registry));
    }

    [Fact]
    public void Context_contracts_reject_duplicate_agent_access_and_fragment_contributors()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agentId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            CreateScope(
                "Project structure",
                agentAccess:
                [
                    new AgentChatContextAgentAccess(
                        agentId,
                        AgentChatContextPermission.Read,
                        "Project"),
                    new AgentChatContextAgentAccess(
                        agentId,
                        AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                        "Selected nodes")
                ]));

        var scope = CreateScope("Project structure");
        using var scopeLease = registry.ActivateScope(scope);
        using var fragmentLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("selection", 0, "Node A"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterFragment(
                scope.Id,
                CreateFragment("selection", 1, "Node B")));
    }

    [Fact]
    public void Registry_rejects_fragments_beyond_the_count_limit()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scope = CreateScope("Project structure");
        using var scopeLease = registry.ActivateScope(scope);
        var leases = Enumerable.Range(0, AgentChatContextLimits.MaximumFragments)
            .Select(index => registry.RegisterFragment(
                scope.Id,
                CreateFragment($"fragment-{index}", index, $"Value {index}")))
            .ToArray();

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                registry.RegisterFragment(
                    scope.Id,
                    CreateFragment("one-too-many", int.MaxValue, "Rejected")));
            Assert.Equal(AgentChatContextLimits.MaximumFragments, registry.Capture()!.Fragments.Count);
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }
        }
    }

    [Fact]
    public void Registry_rejects_fragment_content_beyond_the_aggregate_limit()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scope = CreateScope("Project structure");
        using var scopeLease = registry.ActivateScope(scope);
        using var firstLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("first", 0, new string('a', AgentChatContextFragment.MaximumContentLength)));
        using var secondLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("second", 1, new string('b', AgentChatContextFragment.MaximumContentLength)));

        Assert.Equal(
            AgentChatContextLimits.MaximumAggregateContentLength,
            registry.Capture()!.Fragments.Sum(item => item.Content.Length));
        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterFragment(
                scope.Id,
                CreateFragment("overflow", 2, "x")));
    }

    [Fact]
    public void Registry_rejects_fragment_updates_beyond_the_aggregate_limit()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scope = CreateScope("Project structure");
        using var scopeLease = registry.ActivateScope(scope);
        using var firstLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("first", 0, new string('a', AgentChatContextFragment.MaximumContentLength - 1)));
        using var secondLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("second", 1, new string('b', AgentChatContextFragment.MaximumContentLength - 1)));
        using var finalLease = registry.RegisterFragment(
            scope.Id,
            CreateFragment("final", 2, "x"));

        Assert.Throws<InvalidOperationException>(() =>
            finalLease.Update(CreateFragment("final", 2, "xyz")));
        Assert.Equal("x", registry.Capture()!.Fragments.Single(item =>
            item.ContributorId == new AgentChatContextContributorId("final")).Content);
    }

    [Fact]
    public void Atomic_publication_round_trips_multiple_opaque_attachment_types()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        var first = new TestAttachment("first");
        var second = new OtherTestAttachment(42);
        using var lease = registry.PublishModuleContext(CreatePublication(
            scopeId,
            "old",
            first,
            second));

        var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());

        Assert.Equal(2, snapshot.Attachments.Length);
        var firstEnvelope = Assert.Single(
            snapshot.Attachments,
            envelope => envelope.AttachmentType == typeof(TestAttachment));
        var secondEnvelope = Assert.Single(
            snapshot.Attachments,
            envelope => envelope.AttachmentType == typeof(OtherTestAttachment));
        Assert.True(firstEnvelope.TryGetAttachment<TestAttachment>(out var capturedFirst));
        Assert.True(secondEnvelope.TryGetAttachment<OtherTestAttachment>(out var capturedSecond));
        Assert.Same(first, capturedFirst);
        Assert.Same(second, capturedSecond);
        Assert.Equal(scopeId, firstEnvelope.ScopeId);
        Assert.Equal(
            new AgentChatContextContributorId("selection"),
            firstEnvelope.ContributorId);
        Assert.Equal(
            new ModulePublicationRevision(1),
            firstEnvelope.PublicationRevision);
    }

    [Fact]
    public void Contributor_publication_rejects_duplicate_exact_payload_types()
    {
        var fragment = CreateFragment("selection", 0, "Selected old");

        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentChatContextContributorPublication(
                fragment,
                [
                    CreateAttachmentDraft(new TestAttachment("first")),
                    CreateAttachmentDraft(new TestAttachment("second"))
                ]));

        Assert.Equal("attachmentDrafts", exception.ParamName);
        Assert.Contains(nameof(TestAttachment), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_attachment_lookup_does_not_return_base_or_derived_type_mismatches()
    {
        var derived = new DerivedTestAttachment("derived");
        var envelope = CreateAttachmentDraft(derived).CreateEnvelope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("test-surface"),
                new AgentChatContextSourceId("surface-1")),
            WorkspaceScopeDescriptor.Project("project-1"),
            new AgentChatContextContributorId("selection"),
            new ModulePublicationRevision(1));

        Assert.True(envelope.TryGetAttachment<DerivedTestAttachment>(out var exact));
        Assert.Same(derived, exact);
        Assert.False(envelope.TryGetAttachment<BaseTestAttachment>(out _));
    }

    [Fact]
    public void Publication_copies_nested_input_lists_before_capture()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        var drafts = new List<AgentChatContextAttachmentDraft>
        {
            CreateAttachmentDraft(new TestAttachment("retained"))
        };
        var contributor = new AgentChatContextContributorPublication(
            CreateFragment("selection", 0, "Selected retained"),
            drafts);
        var contributors = new List<AgentChatContextContributorPublication>
        {
            contributor
        };
        var publication = new AgentChatContextPublication(
            CreatePublishedScope(scopeId, "retained"),
            contributors);

        drafts.Clear();
        contributors.Clear();
        using var lease = registry.PublishModuleContext(publication);
        var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());

        Assert.Single(snapshot.Fragments);
        var envelope = Assert.Single(snapshot.Attachments);
        Assert.True(envelope.TryGetAttachment<TestAttachment>(out var attachment));
        Assert.Equal("retained", attachment.Value);
    }

    [Fact]
    public async Task Concurrent_captures_observe_only_complete_old_or_new_publications()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        using var lease = registry.PublishModuleContext(CreatePublication(
            scopeId,
            "old",
            new TestAttachment("old")));
        var observations = new ConcurrentBag<(
            string Position,
            string Fragment,
            string Attachment)>();
        using var start = new ManualResetEventSlim();

        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var index = 0; index < 2_000; index++)
            {
                var state = index % 2 == 0 ? "new" : "old";
                lease.Update(CreatePublication(
                    scopeId,
                    state,
                    new TestAttachment(state)));
            }
        });
        var readers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                for (var index = 0; index < 2_000; index++)
                {
                    var snapshot = Assert.IsType<AgentChatContextSnapshot>(
                        registry.Capture());
                    var position = Assert.IsType<AgentChatContextEntityReference>(
                        snapshot.Scope.SurfacePosition?.PrimarySelection);
                    var fragment = Assert.Single(snapshot.Fragments);
                    var envelope = Assert.Single(snapshot.Attachments);
                    Assert.True(envelope.TryGetAttachment<TestAttachment>(
                        out var attachment));
                    observations.Add((
                        position.Id,
                        fragment.Content,
                        attachment.Value));
                }
            }))
            .ToArray();

        start.Set();
        await Task.WhenAll([writer, .. readers]);

        Assert.NotEmpty(observations);
        Assert.All(observations, observation =>
        {
            var state = observation.Position;
            Assert.True(state is "old" or "new");
            Assert.Equal($"Selected {state}", observation.Fragment);
            Assert.Equal(state, observation.Attachment);
        });
    }

    [Fact]
    public void Invocation_keeps_the_exact_captured_attachment_after_registry_update()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        var original = new TestAttachment("old");
        using var lease = registry.PublishModuleContext(CreatePublication(
            scopeId,
            "old",
            original));
        var captured = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var invocation = AgentChatContextInvocationFactory.Create(
            captured,
            Guid.NewGuid(),
            chatSessionId: null,
            "Use the selection",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(1),
            new DateTimeOffset(2026, 7, 27, 12, 1, 0, TimeSpan.Zero));

        lease.Update(CreatePublication(
            scopeId,
            "new",
            new TestAttachment("new")));

        var transientContext = Assert.IsType<AgentRuntimeTransientContext>(
            invocation.Options.TransientContext);
        var envelope = Assert.Single(
            transientContext.GetAttachments<TestAttachment>());
        Assert.True(envelope.TryGetAttachment<TestAttachment>(out var attachment));
        Assert.Same(original, attachment);
        Assert.Equal("old", attachment.Value);
        Assert.Equal(
            new ModulePublicationRevision(1),
            envelope.PublicationRevision);
        Assert.Equal(
            new ModulePublicationRevision(2),
            registry.Capture()!.Attachments[0].PublicationRevision);
    }

    [Fact]
    public void Context_models_reject_default_identifiers()
    {
        var validSource = new AgentChatContextSource(
            new AgentChatContextSourceKind("test-surface"),
            new AgentChatContextSourceId("record"));

        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextScope(default, validSource, "Record"));
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(default, new AgentChatContextSourceId("record")),
                "Record"));
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(new AgentChatContextSourceKind("test-surface"), default),
                "Record"));
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextFragment(default, 0, "Record"));

        var registry = new AgentChatContextRegistry(TimeProvider.System);
        using var scopeLease = registry.ActivateScope(CreateScope("Project structure"));
        Assert.Throws<ArgumentException>(() =>
            registry.RegisterFragment(default, CreateFragment("record", 0, "Record")));
    }

    [Theory]
    [InlineData(AgentChatContextPermission.None)]
    [InlineData(AgentChatContextPermission.Mutate)]
    [InlineData((AgentChatContextPermission)4)]
    [InlineData((AgentChatContextPermission)7)]
    public void Agent_access_rejects_invalid_permission_combinations(
        AgentChatContextPermission permissions)
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextAgentAccess(Guid.NewGuid(), permissions, "Record"));
    }

    [Fact]
    public void Context_models_reject_values_beyond_identifier_and_scope_limits()
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextSourceKind(
                new string('k', AgentChatContextLimits.MaximumSourceKindLength + 1)));
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextSourceId(
                new string('i', AgentChatContextLimits.MaximumSourceIdLength + 1)));
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextContributorId(
                new string('c', AgentChatContextLimits.MaximumContributorIdLength + 1)));
        Assert.Throws<ArgumentException>(() =>
            CreateScope(new string('d', AgentChatContextLimits.MaximumDisplayNameLength + 1)));
        Assert.Throws<ArgumentException>(() =>
            new AgentChatContextAgentAccess(
                Guid.NewGuid(),
                AgentChatContextPermission.Read,
                new string('l', AgentChatContextLimits.MaximumScopeLabelLength + 1)));

        var access = Enumerable.Range(0, AgentChatContextLimits.MaximumAgentAccessEntries + 1)
            .Select(_ => new AgentChatContextAgentAccess(
                Guid.NewGuid(),
                AgentChatContextPermission.Read,
                "Record"))
            .ToArray();

        Assert.Throws<ArgumentException>(() =>
            CreateScope("Project structure", agentAccess: access));
    }

    private static IReadOnlyList<string> CaptureContributorIds(AgentChatContextRegistry registry)
    {
        return registry.Capture()!.Fragments
            .Select(item => item.ContributorId.Value)
            .ToArray();
    }

    private static AgentChatContextFragment CreateFragment(
        string contributorId,
        int order,
        string content)
    {
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(contributorId),
            order,
            content);
    }

    private static AgentChatContextPublication CreatePublication(
        AgentChatContextScopeId scopeId,
        string state,
        params IAgentChatContextAttachment[] attachments)
    {
        return new AgentChatContextPublication(
            CreatePublishedScope(scopeId, state),
            [
                new AgentChatContextContributorPublication(
                    CreateFragment(
                        "selection",
                        0,
                        $"Selected {state}"),
                    attachments
                        .Select(CreateAttachmentDraft)
                        .ToArray())
            ]);
    }

    private static AgentChatContextScope CreatePublishedScope(
        AgentChatContextScopeId scopeId,
        string state)
    {
        return new AgentChatContextScope(
            scopeId,
            new AgentChatContextSource(
                new AgentChatContextSourceKind("test-surface"),
                new AgentChatContextSourceId("surface-1")),
            "Test surface",
            WorkspaceScopeDescriptor.Project("project-1"),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            surfacePosition: new AgentChatSurfacePosition(
                "tests",
                "surface",
                "selection",
                "/tests",
                new AgentChatContextEntityReference(
                    "selection",
                    state,
                    state)));
    }

    private static AgentChatContextAttachmentDraft CreateAttachmentDraft(
        IAgentChatContextAttachment attachment)
    {
        var identity = attachment.GetType().Name;
        return new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind("tests.snapshot"),
            new SnapshotContentFingerprint($"content-{identity}"),
            new SnapshotCoverageFingerprint($"coverage-{identity}"),
            new DatabaseProfileGeneration(1),
            new SnapshotFreshnessFingerprint($"freshness-{identity}"),
            new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 27, 12, 5, 0, TimeSpan.Zero),
            attachment);
    }

    private static AgentChatContextScope CreateScope(
        string displayName,
        AgentChatContextScopeId? scopeId = null,
        IReadOnlyList<AgentChatContextAgentAccess>? agentAccess = null,
        AgentChatContextScopeAccessMode accessMode = AgentChatContextScopeAccessMode.Unrestricted)
    {
        return new AgentChatContextScope(
            scopeId ?? AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("test-surface"),
                new AgentChatContextSourceId(displayName)),
            displayName,
            agentAccess: agentAccess,
            accessMode: accessMode);
    }

    private sealed record TestAttachment(string Value) :
        IAgentChatContextAttachment;

    private sealed record OtherTestAttachment(int Value) :
        IAgentChatContextAttachment;

    private record BaseTestAttachment(string Value) :
        IAgentChatContextAttachment;

    private sealed record DerivedTestAttachment(string Value) :
        BaseTestAttachment(Value);
}

public sealed class AgentChatContextContributionComposerTests
{
    [Fact]
    public void Compose_returns_null_only_when_context_is_absent_and_preserves_an_empty_surface_lease()
    {
        var agentId = Guid.NewGuid();
        var workspaceScope = WorkspaceScopeDescriptor.Project("project-1");
        var emptyContext = new AgentChatContextSnapshot(
            CreateScope(workspaceScope: workspaceScope),
            [],
            Version: 1,
            CapturedAtUtc: DateTimeOffset.UtcNow);

        Assert.Null(AgentChatContextContributionComposer.Compose(null, agentId));
        var result = Assert.IsType<AgentRuntimeTransientContext>(
            AgentChatContextContributionComposer.Compose(emptyContext, agentId));
        Assert.Equal(workspaceScope, result.WorkspaceScope);
        Assert.False(result.HasContent);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public void Compose_marks_context_as_untrusted_and_preserves_fragment_order()
    {
        var workspaceScope = WorkspaceScopeDescriptor.Tenant("tenant-1");
        var context = new AgentChatContextSnapshot(
            CreateScope(workspaceScope: workspaceScope),
            [
                CreateFragment("selection", 0, "Selected partner: Contoso"),
                CreateFragment("form", 1, "Unsaved note: Follow up Friday")
            ],
            Version: 2,
            CapturedAtUtc: DateTimeOffset.UtcNow);

        var result = Assert.IsType<AgentRuntimeTransientContext>(
            AgentChatContextContributionComposer.Compose(context, Guid.NewGuid()));

        Assert.Contains("untrusted data for this single run", result.Content);
        Assert.Contains("<application_context_json>", result.Content);
        Assert.Contains("CRM partner", result.Content);
        Assert.True(
            result.Content.IndexOf("Selected partner: Contoso", StringComparison.Ordinal) <
            result.Content.IndexOf("Unsaved note: Follow up Friday", StringComparison.Ordinal));
        Assert.Equal(workspaceScope, result.WorkspaceScope);
    }

    [Fact]
    public void Compose_enforces_allowlisted_read_access_before_ignoring_an_unscoped_empty_context()
    {
        var allowedAgentId = Guid.NewGuid();
        var deniedAgentId = Guid.NewGuid();
        var context = new AgentChatContextSnapshot(
            CreateScope(
                accessMode: AgentChatContextScopeAccessMode.AllowListed,
                agentAccess:
                [
                    new AgentChatContextAgentAccess(
                        allowedAgentId,
                        AgentChatContextPermission.Read,
                        "Partner")
                ]),
            [],
            Version: 1,
            CapturedAtUtc: DateTimeOffset.UtcNow);

        Assert.Null(AgentChatContextContributionComposer.Compose(context, allowedAgentId));
        Assert.Throws<AgentChatContextAccessDeniedException>(() =>
            AgentChatContextContributionComposer.Compose(context, deniedAgentId));
    }

    [Fact]
    public void Scope_defaults_to_fail_closed_access()
    {
        var agentId = Guid.NewGuid();
        var scope = new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("crm"),
                new AgentChatContextSourceId("partner")),
            "CRM partner");
        var context = new AgentChatContextSnapshot(
            scope,
            [CreateFragment("selection", 0, "Selected partner: Contoso")],
            Version: 1,
            CapturedAtUtc: DateTimeOffset.UtcNow);

        Assert.Equal(AgentChatContextScopeAccessMode.AllowListed, scope.AccessMode);
        Assert.False(context.CanRead(agentId));
        Assert.Throws<AgentChatContextAccessDeniedException>(() =>
            AgentChatContextContributionComposer.Compose(context, agentId));
    }

    [Fact]
    public void Compose_rejects_an_empty_agent_identifier()
    {
        Assert.Throws<ArgumentException>(() =>
            AgentChatContextContributionComposer.Compose(null, Guid.Empty));
    }

    private static AgentChatContextScope CreateScope(
        AgentChatContextScopeAccessMode accessMode = AgentChatContextScopeAccessMode.Unrestricted,
        WorkspaceScopeDescriptor? workspaceScope = null,
        params AgentChatContextAgentAccess[] agentAccess)
    {
        return new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("crm"),
                new AgentChatContextSourceId("partner")),
            "CRM partner",
            workspaceScope,
            agentAccess: agentAccess,
            accessMode: accessMode);
    }

    private static AgentChatContextFragment CreateFragment(
        string contributorId,
        int order,
        string content)
    {
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(contributorId),
            order,
            content);
    }
}

public sealed class AgentChatContextInvocationFactoryTests
{
    [Fact]
    public void Create_retains_project_scope_when_the_surface_snapshot_has_no_detail_fragments()
    {
        var agentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        var context = new AgentChatContextSnapshot(
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind("project-structure"),
                    new AgentChatContextSourceId(projectId.ToString("D"))),
                "Project structure",
                projectScope,
                [
                    new AgentChatContextAgentAccess(
                        agentId,
                        AgentChatContextPermission.Read,
                        "This project")
                ],
                AgentChatContextScopeAccessMode.AllowListed),
            [],
            Version: 1,
            CapturedAtUtc: DateTimeOffset.UtcNow);

        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            agentId,
            chatSessionId: null,
            "Summarize this project",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(0),
            DateTimeOffset.UtcNow);

        var transientContext = Assert.IsType<AgentRuntimeTransientContext>(
            invocation.Options.TransientContext);
        Assert.Equal(projectScope, transientContext.WorkspaceScope);
        using var metadata = JsonDocument.Parse(
            Assert.IsType<ExecutionInvocationContext>(invocation.Options.Context).MetadataJson);
        Assert.True(metadata.RootElement.GetProperty(
            ExecutionInvocationMetadata.TransientContextRequiredMetadataKey).GetBoolean());
        Assert.Equal(
            AgentChatContextDigest.Compute(transientContext),
            metadata.RootElement.GetProperty(
                ExecutionInvocationMetadata.TransientContextDigestMetadataKey).GetString());
    }

    [Fact]
    public void Create_keeps_the_prompt_raw_and_attaches_transient_context_metadata()
    {
        var agentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scope = new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("project-structure"),
                new AgentChatContextSourceId(projectId.ToString("D"))),
            "Project structure",
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            [
                new AgentChatContextAgentAccess(
                    agentId,
                    AgentChatContextPermission.Read,
                    "This project")
            ],
            AgentChatContextScopeAccessMode.AllowListed);
        var context = new AgentChatContextSnapshot(
            scope,
            [
                new AgentChatContextFragment(
                    new AgentChatContextContributorId("selection"),
                    0,
                    "Selected node: task-1")
            ],
            Version: 3,
            CapturedAtUtc: DateTimeOffset.UtcNow);
        var sessionId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();

        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            agentId,
            sessionId,
            "Estimate it",
            operationId,
            new DatabaseProfileGeneration(0),
            DateTimeOffset.UtcNow);

        Assert.Equal("Estimate it", invocation.Prompt);
        Assert.Equal(operationId, invocation.Options.InitialActivityOperationId);
        var transientContext = Assert.IsType<AgentRuntimeTransientContext>(
            invocation.Options.TransientContext);
        Assert.Contains("Selected node: task-1", transientContext.Content);
        Assert.Equal(scope.WorkspaceScope, transientContext.WorkspaceScope);
        var executionContext = Assert.IsType<ExecutionInvocationContext>(invocation.Options.Context);
        Assert.Equal("project-structure", executionContext.SourceKind);
        Assert.Equal(projectId.ToString("D"), executionContext.SourceId);
        Assert.Equal(sessionId.ToString("N"), executionContext.CausationId);
        Assert.Contains(ExecutionInvocationMetadata.ContextWorkspaceScopeMetadataKey, executionContext.MetadataJson);
        Assert.Contains(projectId.ToString("D"), executionContext.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(scope.DisplayName, executionContext.MetadataJson, StringComparison.Ordinal);

        using var metadata = JsonDocument.Parse(executionContext.MetadataJson);
        var root = metadata.RootElement;
        Assert.True(root.GetProperty(
            ExecutionInvocationMetadata.TransientContextRequiredMetadataKey).GetBoolean());
        var digest = root.GetProperty(
            ExecutionInvocationMetadata.TransientContextDigestMetadataKey).GetString();
        Assert.Equal(AgentChatContextDigest.Compute(transientContext), digest);
        var invocationDigest = root.EnumerateObject().Single(property =>
            string.Equals(property.Name, "ContextDigest", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(digest, invocationDigest.Value.GetString());
    }

    [Fact]
    public void Create_uses_each_new_snapshot_without_retaining_previous_module_context()
    {
        var agentId = Guid.NewGuid();
        var project = CreateContext(
            agentId,
            "project-structure",
            "project-1",
            "Selected project node: A");
        var crm = CreateContext(
            agentId,
            "crm-partner",
            "partner-2",
            "Selected partner: Contoso");
        var firstOperationId = AgentExecutionOperationId.New();
        var secondOperationId = AgentExecutionOperationId.New();

        var first = AgentChatContextInvocationFactory.Create(
            project,
            agentId,
            chatSessionId: null,
            "Explain",
            firstOperationId,
            new DatabaseProfileGeneration(0),
            DateTimeOffset.UtcNow);
        var second = AgentChatContextInvocationFactory.Create(
            crm,
            agentId,
            chatSessionId: null,
            "Continue",
            secondOperationId,
            new DatabaseProfileGeneration(0),
            DateTimeOffset.UtcNow);

        Assert.Equal("Explain", first.Prompt);
        Assert.Equal("Continue", second.Prompt);
        Assert.Equal(firstOperationId, first.Options.InitialActivityOperationId);
        Assert.Equal(secondOperationId, second.Options.InitialActivityOperationId);
        Assert.Contains("Selected project node: A", first.Options.TransientContext!.Content);
        Assert.DoesNotContain("Selected partner: Contoso", first.Options.TransientContext.Content);
        Assert.Contains("Selected partner: Contoso", second.Options.TransientContext!.Content);
        Assert.DoesNotContain("Selected project node: A", second.Options.TransientContext.Content);
        Assert.Equal("crm-partner", second.Options.Context!.SourceKind);
    }

    [Fact]
    public void CreateCompletionNotification_projects_the_original_explicit_refresh_boundary()
    {
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var scopeId = AgentChatContextScopeId.Create();
        var source = new AgentChatContextSource(
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")));
        var completedAtUtc = new DateTimeOffset(2026, 7, 16, 15, 30, 0, TimeSpan.Zero);
        var run = CreateCompletionRun(
            agentId,
            sessionId,
            runId,
            scopeId,
            source,
            AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
            completedAtUtc,
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun);

        var notification = Assert.IsType<AgentChatExecutionCompleted>(
            AgentChatContextInvocationFactory.CreateCompletionNotification(run));

        Assert.Equal(scopeId, notification.ScopeId);
        Assert.Equal(source, notification.Source);
        Assert.Equal(agentId, notification.AgentId);
        Assert.Equal(sessionId, notification.ChatSessionId);
        Assert.Equal(runId, notification.ExecutionRunId);
        Assert.Equal(completedAtUtc, notification.CompletedAtUtc);
    }

    [Fact]
    public void CreateCompletionNotification_honors_refresh_policy_without_granting_mutation_access()
    {
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var scopeId = AgentChatContextScopeId.Create();
        var source = new AgentChatContextSource(
            new AgentChatContextSourceKind("crm-account"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")));
        var completedAtUtc = new DateTimeOffset(2026, 7, 16, 15, 30, 0, TimeSpan.Zero);
        var run = CreateCompletionRun(
            agentId,
            sessionId,
            runId,
            scopeId,
            source,
            AgentChatContextPermission.Read,
            completedAtUtc,
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun);

        var notification = Assert.IsType<AgentChatExecutionCompleted>(
            AgentChatContextInvocationFactory.CreateCompletionNotification(run));

        Assert.Equal(scopeId, notification.ScopeId);
        Assert.Equal(source, notification.Source);
    }

    [Fact]
    public void CreateCompletionNotification_rejects_non_mutating_non_terminal_and_untrusted_runs()
    {
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var scopeId = AgentChatContextScopeId.Create();
        var source = new AgentChatContextSource(
            new AgentChatContextSourceKind("crm-partner"),
            new AgentChatContextSourceId("partner-1"));
        var completedAtUtc = new DateTimeOffset(2026, 7, 16, 15, 30, 0, TimeSpan.Zero);
        var readOnlyRun = CreateCompletionRun(
            agentId,
            sessionId,
            runId,
            scopeId,
            source,
            AgentChatContextPermission.Read,
            completedAtUtc);
        var mutatingRun = CreateCompletionRun(
            agentId,
            sessionId,
            runId,
            scopeId,
            source,
            AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
            completedAtUtc);

        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(readOnlyRun));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(mutatingRun));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(
            mutatingRun with
            {
                State = ExecutionState.WaitingOnTool,
                Outcome = null,
                CompletedAtUtc = null
            }));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(
            mutatingRun with { Outcome = RunOutcome.Failed }));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(
            mutatingRun with
            {
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Failed
            }));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(
            mutatingRun with { MetadataJson = "{" }));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(
            mutatingRun with { MetadataJson = "{}" }));
        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(
            mutatingRun with { RequestedBy = "untrusted-caller" }));
    }

    private static ExecutionRunRecord CreateCompletionRun(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        AgentChatContextScopeId scopeId,
        AgentChatContextSource source,
        AgentChatContextPermission permission,
        DateTimeOffset completedAtUtc,
        AgentChatContextCompletionRefreshMode completionRefreshMode = AgentChatContextCompletionRefreshMode.None)
    {
        var context = new AgentChatContextSnapshot(
            new AgentChatContextScope(
                scopeId,
                source,
                "Sensitive surface display name",
                agentAccess:
                [
                    new AgentChatContextAgentAccess(
                        agentId,
                        permission,
                        "Current record")
                ],
                accessMode: AgentChatContextScopeAccessMode.AllowListed,
                completionRefreshMode: completionRefreshMode),
            [
                new AgentChatContextFragment(
                    new AgentChatContextContributorId("surface"),
                    0,
                    "Selected record: 42")
            ],
            Version: 7,
            CapturedAtUtc: completedAtUtc.AddMinutes(-1));
        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            agentId,
            sessionId,
            "Update the selected record",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(0),
            DateTimeOffset.UtcNow);
        var invocationContext = Assert.IsType<ExecutionInvocationContext>(
            invocation.Options.Context);

        return new ExecutionRunRecord(
            runId,
            agentId,
            sessionId,
            "Update record",
            invocationContext.SourceKind,
            invocationContext.SourceId,
            invocationContext.CorrelationId,
            invocationContext.CausationId,
            invocationContext.RequestedBy,
            invocationContext.RequestedByKind,
            invocationContext.MetadataJson,
            "Update the selected record",
            "Updated",
            "test-provider",
            "test-model",
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            completedAtUtc.AddMinutes(-1),
            completedAtUtc,
            completedAtUtc.AddMinutes(-1),
            completedAtUtc,
            string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static AgentChatContextSnapshot CreateContext(
        Guid agentId,
        string sourceKind,
        string sourceId,
        string content)
    {
        return new AgentChatContextSnapshot(
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind(sourceKind),
                    new AgentChatContextSourceId(sourceId)),
                sourceKind,
                agentAccess:
                [
                    new AgentChatContextAgentAccess(
                        agentId,
                        AgentChatContextPermission.Read,
                        sourceKind)
                ],
                accessMode: AgentChatContextScopeAccessMode.AllowListed),
            [
                new AgentChatContextFragment(
                    new AgentChatContextContributorId("surface"),
                    0,
                    content)
            ],
            Version: 1,
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }
}

public sealed class AgentChatContextDigestTests
{
    [Fact]
    public void Digest_changes_for_each_attachment_identity_stamp_independently()
    {
        var baseline = CreateContext();
        var contexts = new[]
        {
            baseline,
            CreateContext(promptContent: "Prompt context changed"),
            CreateContext(publicationRevision: 2),
            CreateContext(contentFingerprint: "content-2"),
            CreateContext(coverageFingerprint: "coverage-2"),
            CreateContext(databaseProfileGeneration: 2),
            CreateContext(freshnessFingerprint: "freshness-2"),
            CreateContext(contextWorkspaceScope: WorkspaceScopeDescriptor.Project("project-2"))
        };

        var digests = contexts
            .Select(AgentChatContextDigest.Compute)
            .ToArray();

        Assert.Equal(digests.Length, digests.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Digest_excludes_payload_values_and_runtime_type_names()
    {
        var first = CreateContext(
            attachment: new DigestTestAttachment("secret-one"));
        var second = CreateContext(
            attachment: new OtherDigestTestAttachment("secret-two"));

        Assert.Equal(
            AgentChatContextDigest.Compute(first),
            AgentChatContextDigest.Compute(second));
    }

    private static AgentRuntimeTransientContext CreateContext(
        string promptContent = "Prompt context",
        long publicationRevision = 1,
        string contentFingerprint = "content-1",
        string coverageFingerprint = "coverage-1",
        long databaseProfileGeneration = 1,
        string freshnessFingerprint = "freshness-1",
        WorkspaceScopeDescriptor? contextWorkspaceScope = null,
        IAgentChatContextAttachment? attachment = null)
    {
        var workspaceScope = WorkspaceScopeDescriptor.Project("project-1");
        var envelope = new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind("tests.digest"),
            new SnapshotContentFingerprint(contentFingerprint),
            new SnapshotCoverageFingerprint(coverageFingerprint),
            new DatabaseProfileGeneration(databaseProfileGeneration),
            new SnapshotFreshnessFingerprint(freshnessFingerprint),
            new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 27, 12, 5, 0, TimeSpan.Zero),
            attachment ?? new DigestTestAttachment("payload"))
            .CreateEnvelope(
                new AgentChatContextScopeId(
                    Guid.Parse("59680919-0872-4a2b-9af7-ca98a46d036f")),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind("tests"),
                    new AgentChatContextSourceId("surface-1")),
                workspaceScope,
                new AgentChatContextContributorId("selection"),
                new ModulePublicationRevision(publicationRevision));

        return new AgentRuntimeTransientContext(
            promptContent,
            contextWorkspaceScope ?? workspaceScope,
            [envelope]);
    }

    private sealed record DigestTestAttachment(string Value) :
        IAgentChatContextAttachment;

    private sealed record OtherDigestTestAttachment(string Value) :
        IAgentChatContextAttachment;
}

public sealed class AgentRunTransientContextRegistryTests
{
    [Fact]
    public void Approval_context_rejects_a_different_top_level_workspace_scope()
    {
        var registry = new AgentRunTransientContextRegistry();
        var capturedContext = new AgentRuntimeTransientContext(
            string.Empty,
            WorkspaceScopeDescriptor.Project("project-1"));
        var run = CreateRun(capturedContext);
        var substitutedContext = new AgentRuntimeTransientContext(
            string.Empty,
            WorkspaceScopeDescriptor.Project("project-2"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(run, substitutedContext));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Approval_context_must_match_the_run_digest_and_is_unavailable_after_release()
    {
        var registry = new AgentRunTransientContextRegistry();
        var context = new AgentRuntimeTransientContext(
            "Selected account: 42",
            WorkspaceScopeDescriptor.Organization("org-1"));
        var run = CreateRun(context);

        registry.Register(run, context);

        Assert.Equal(context, registry.Resolve(run));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                run,
                new AgentRuntimeTransientContext(
                    context.Content,
                    context.WorkspaceScope)));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(run, new AgentRuntimeTransientContext("Selected account: 43")));

        registry.Remove(run.Id);

        Assert.Throws<AgentRunTransientContextUnavailableException>(() =>
            registry.Resolve(run));
    }

    private static ExecutionRunRecord CreateRun(AgentRuntimeTransientContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var metadata = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
            "{}",
            AgentChatContextDigest.Compute(context));
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Approval run",
            "crm-account",
            "42",
            string.Empty,
            string.Empty,
            AgentChatContextInvocationFactory.Requester,
            AgentChatContextInvocationFactory.RequesterKind,
            metadata,
            "Update account",
            string.Empty,
            "test-provider",
            "test-model",
            ExecutionState.WaitingOnTool,
            null,
            now,
            now,
            now,
            null,
            "runtime-session",
            "{}",
            []);
    }
}

public sealed class AgentChatExecutionOrchestratorTests
{
    [Fact]
    public async Task SendMessageAsync_captures_the_current_context_for_each_send()
    {
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        workspaceProxy.SendResult = CreateRunResult(agentId, sessionId);
        var hub = new AgentChatExecutionNotificationHub(
            NullLogger<AgentChatExecutionNotificationHub>.Instance);
        var orchestrator = CreateOrchestrator(workspace, registry, hub);

        using (var scopeLease = registry.ActivateScope(CreateScope(
                   agentId,
                   "project-structure",
                   "project-1",
                   "Project structure")))
        using (registry.RegisterFragment(
                   scopeLease.ScopeId,
                   CreateFragment("Selected project node: A")))
        {
            await orchestrator.SendMessageAsync(agentId, sessionId, "Explain");
        }

        using (var scopeLease = registry.ActivateScope(CreateScope(
                   agentId,
                   "project-structure",
                   "project-1",
                   "Project Gantt")))
        using (registry.RegisterFragment(
                   scopeLease.ScopeId,
                   CreateFragment("Current project workspace view: Gantt schedule. Selected project-structure node ids: none.")))
        {
            await orchestrator.SendMessageAsync(agentId, sessionId, "Continue with the schedule");
        }

        using (var scopeLease = registry.ActivateScope(CreateScope(
                   agentId,
                   "crm-partner",
                   "partner-2",
                   "CRM partner")))
        using (registry.RegisterFragment(
                   scopeLease.ScopeId,
                   CreateFragment("Selected partner: Contoso")))
        {
            await orchestrator.SendMessageAsync(agentId, sessionId, "Continue");
        }

        Assert.Collection(
            workspaceProxy.SendCalls,
            first =>
            {
                Assert.Equal("Explain", first.Prompt);
                Assert.Equal("project-structure", first.Options.Context!.SourceKind);
                Assert.Contains("Selected project node: A", first.Options.TransientContext!.Content);
                Assert.DoesNotContain("Selected partner: Contoso", first.Options.TransientContext.Content);
            },
            second =>
            {
                Assert.Equal("Continue with the schedule", second.Prompt);
                Assert.Equal("project-structure", second.Options.Context!.SourceKind);
                Assert.Contains("Gantt schedule", second.Options.TransientContext!.Content);
                Assert.DoesNotContain("Selected project node: A", second.Options.TransientContext.Content);
                Assert.DoesNotContain("Selected partner: Contoso", second.Options.TransientContext.Content);
            },
            third =>
            {
                Assert.Equal("Continue", third.Prompt);
                Assert.Equal("crm-partner", third.Options.Context!.SourceKind);
                Assert.Contains("Selected partner: Contoso", third.Options.TransientContext!.Content);
                Assert.DoesNotContain("Selected project node: A", third.Options.TransientContext.Content);
                Assert.DoesNotContain("Gantt schedule", third.Options.TransientContext.Content);
            });
    }

    [Fact]
    public async Task Send_and_approval_continuation_publish_the_completion_carried_by_the_result()
    {
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var source = new AgentChatContextSource(
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId("project-1"));
        var sendNotification = CreateNotification(source, agentId, sessionId);
        var approvalNotification = CreateNotification(source, agentId, sessionId);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        workspaceProxy.SendResult = CreateRunResult(
            agentId,
            sessionId,
            sendNotification);
        workspaceProxy.ApprovalResult = CreateRunResult(
            agentId,
            sessionId,
            approvalNotification);
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var hub = new AgentChatExecutionNotificationHub(
            NullLogger<AgentChatExecutionNotificationHub>.Instance);
        var published = new List<AgentChatExecutionCompleted>();
        using var subscription = hub.Subscribe(
            source,
            notification =>
            {
                published.Add(notification);
                return Task.CompletedTask;
            });
        var orchestrator = CreateOrchestrator(workspace, registry, hub);

        var sendResult = await orchestrator.SendMessageAsync(
            agentId,
            sessionId,
            "Update it");
        var approvalResult = await orchestrator.RespondToPendingApprovalsAsync(
            agentId,
            sessionId,
            approved: true);

        Assert.Same(workspaceProxy.SendResult, sendResult);
        Assert.Same(workspaceProxy.ApprovalResult, approvalResult);
        Assert.Equal([sendNotification, approvalNotification], published);
        Assert.Equal(1, workspaceProxy.ApprovalCallCount);
    }

    private static AgentChatContextScope CreateScope(
        Guid agentId,
        string sourceKind,
        string sourceId,
        string displayName)
    {
        return new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind(sourceKind),
                new AgentChatContextSourceId(sourceId)),
            displayName,
            agentAccess:
            [
                new AgentChatContextAgentAccess(
                    agentId,
                    AgentChatContextPermission.Read,
                    displayName)
            ],
            accessMode: AgentChatContextScopeAccessMode.AllowListed);
    }

    private static AgentChatContextFragment CreateFragment(string content)
    {
        return new AgentChatContextFragment(
            new AgentChatContextContributorId("selection"),
            0,
            content);
    }

    private static AgentChatExecutionCompleted CreateNotification(
        AgentChatContextSource source,
        Guid agentId,
        Guid sessionId)
    {
        return new AgentChatExecutionCompleted(
            AgentChatContextScopeId.Create(),
            source,
            agentId,
            sessionId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }

    private static AgentChatRunResult CreateRunResult(
        Guid agentId,
        Guid sessionId,
        AgentChatExecutionCompleted? notification = null)
    {
        var runId = Guid.NewGuid();
        return new AgentChatRunResult(
            sessionId,
            new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Done",
                DateTimeOffset.UtcNow,
                TokenEstimate: 1),
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                sessionId,
                DateTimeOffset.UtcNow,
                RunOutcome.Succeeded,
                "test-provider",
                "test-model",
                DurationMs: 1,
                InputTokens: 1,
                OutputTokens: 1,
                ToolCalls: 0)
            {
                ExecutionRunId = runId
            })
        {
            ExecutionRunId = runId,
            State = ExecutionState.Completed,
            ContextCompletionNotification = notification
        };
    }

    private static AgentChatExecutionOrchestrator CreateOrchestrator(
        IOrchestratorWorkspaceService workspace,
        IAgentChatContextRegistry registry,
        IAgentChatExecutionNotificationHub notificationHub)
    {
        var profileId = Guid.NewGuid();
        var scope = WorkspaceScopeDescriptor.Organization(profileId.ToString("N"));
        var coordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
        return new AgentChatExecutionOrchestrator(
            workspace,
            registry,
            notificationHub,
            coordinator,
            new OrchestratorWorkspaceFactory(workspace, scope),
            new OrchestratorDatabaseProfileRuntimeAccessor(profileId),
            new FixedAgentExecutionProfileGenerationSource(
                new DatabaseProfileGeneration(0)),
            TimeProvider.System);
    }

    private static (IOrchestratorWorkspaceService Service, WorkspaceServiceProxy Proxy)
        CreateWorkspaceService()
    {
        var service = DispatchProxy.Create<
            IOrchestratorWorkspaceService,
            WorkspaceServiceProxy>();
        var proxy = (WorkspaceServiceProxy)(object)service;
        proxy.SendResult = CreateRunResult(Guid.NewGuid(), Guid.NewGuid());
        proxy.ApprovalResult = CreateRunResult(Guid.NewGuid(), Guid.NewGuid());
        return (service, proxy);
    }

    private interface IOrchestratorWorkspaceService :
        IAgentFrameworkWorkspaceService,
        IAgentFrameworkWorkspaceActivityExecutionService
    {
    }

    private sealed record SendCall(
        Guid AgentId,
        Guid? ChatSessionId,
        string Prompt,
        AgentChatRunOptions Options);

    private class WorkspaceServiceProxy : DispatchProxy
    {
        public AgentChatRunResult SendResult { get; set; } = default!;

        public AgentChatRunResult ApprovalResult { get; set; } = default!;

        public List<SendCall> SendCalls { get; } = [];

        public int ApprovalCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);

            if (targetMethod.Name == nameof(
                    IAgentFrameworkWorkspaceActivityExecutionService.SendMessageWithinOperationAsync))
            {
                var operation = Assert.IsAssignableFrom<
                    IAgentExecutionActivityOperationLease>(args[0]);
                SendCalls.Add(new SendCall(
                    Assert.IsType<Guid>(args[1]),
                    (Guid?)args[2],
                    Assert.IsType<string>(args[3]),
                    Assert.IsType<AgentChatRunOptions>(args[4])));
                CompleteOperation(operation, SendResult);
                return Task.FromResult(SendResult);
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.SendMessageAsync))
            {
                SendCalls.Add(new SendCall(
                    Assert.IsType<Guid>(args[0]),
                    (Guid?)args[1],
                    Assert.IsType<string>(args[2]),
                    Assert.IsType<AgentChatRunOptions>(args[3])));
                return Task.FromResult(SendResult);
            }

            if (targetMethod.Name == nameof(
                    IAgentFrameworkWorkspaceActivityExecutionService.RespondToPendingApprovalsWithinOperationAsync))
            {
                ApprovalCallCount++;
                var operation = Assert.IsAssignableFrom<
                    IAgentExecutionActivityOperationLease>(args[0]);
                CompleteOperation(operation, ApprovalResult);
                return Task.FromResult(ApprovalResult);
            }

            if (targetMethod.Name == nameof(
                    IAgentFrameworkWorkspaceService.RespondToPendingApprovalsAsync))
            {
                ApprovalCallCount++;
                return Task.FromResult(ApprovalResult);
            }

            throw new InvalidOperationException(
                $"Unexpected workspace service call '{targetMethod.Name}'.");
        }

        private static void CompleteOperation(
            IAgentExecutionActivityOperationLease operation,
            AgentChatRunResult result)
        {
            if (!operation.ChatSessionId.HasValue)
            {
                operation.BindChatSession(result.ChatSessionId);
            }

            operation.BindExecutionRun(
                result.ExecutionRunId,
                result.ChatSessionId);
            operation.Report(
                AgentExecutionActivityPhase.PersistingResult,
                "Persisting test result.");
            operation.Complete("Test operation completed.");
        }
    }

    private sealed class OrchestratorWorkspaceFactory(
        IAgentFrameworkWorkspaceService workspace,
        WorkspaceScopeDescriptor organizationScope)
        : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
        {
            return workspace;
        }

        public IAgentFrameworkWorkspaceService GetWorkspaceService(
            WorkspaceScopeDescriptor scope)
        {
            return workspace;
        }

        public WorkspaceScopeDescriptor GetOrganizationScope()
        {
            return organizationScope;
        }

        public string GetWorkspaceRoot()
        {
            return "test-workspace";
        }
    }

    private sealed class OrchestratorDatabaseProfileRuntimeAccessor(
        Guid profileId) : IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile profile = new(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Test profile",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test");

        public ResolvedDatabaseProfile ResolveCurrentProfile()
        {
            return profile;
        }

        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId)
        {
            if (requestedProfileId != profileId)
            {
                throw new KeyNotFoundException();
            }

            return profile;
        }
    }
}

public sealed class FloatingAgentChatCoordinatorTests
{
    [Fact]
    public async Task StartNewChatAsync_attaches_the_exact_created_session_and_preserves_catalog()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Launch agent", clock.GetUtcNow());
        var sessionId = Guid.NewGuid();
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        workspaceProxy.CreatedSessionId = sessionId;
        var registry = new ActiveAgentChatRegistry(clock);
        var pool = new CoordinatorPreparationPool(agent);
        var settingsService = new CoordinatorSettingsService(
            FloatingAgentChatSettings.Default);
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            pool,
            settingsService,
            clock);
        coordinator.ShowCatalog(AgentChatCatalogTab.ActiveChats);

        var chat = await coordinator.StartNewChatAsync(agent.Id);

        Assert.Equal(sessionId, chat.ChatSessionId);
        var state = coordinator.Snapshot();
        Assert.True(state.IsCatalogVisible);
        Assert.Equal(AgentChatCatalogTab.ActiveChats, state.CatalogTab);
        Assert.Equal(chat, Assert.Single(state.ActiveChats));
        Assert.Equal([null], workspaceProxy.RequestedSessionIds);
    }

    [Fact]
    public async Task OpenChatAsync_preserves_catalog_visibility_and_tab()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("History agent", clock.GetUtcNow());
        var sessionId = Guid.NewGuid();
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        await using var coordinator = CreateCoordinator(
            workspace,
            new ActiveAgentChatRegistry(clock),
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);
        coordinator.ShowCatalog(AgentChatCatalogTab.ActiveChats);

        var chat = await coordinator.OpenChatAsync(agent.Id, sessionId);

        var state = coordinator.Snapshot();
        Assert.True(state.IsCatalogVisible);
        Assert.Equal(AgentChatCatalogTab.ActiveChats, state.CatalogTab);
        Assert.Equal(sessionId, chat.ChatSessionId);
        Assert.Equal([sessionId], workspaceProxy.RequestedSessionIds);
    }

    [Fact]
    public async Task ShowChat_preserves_catalog_visibility_and_tab()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Active agent", clock.GetUtcNow());
        var registry = new ActiveAgentChatRegistry(clock);
        var chat = registry.Open(
            CreateIdentity(agent),
            Guid.NewGuid(),
            FloatingAgentChatSettings.Default);
        registry.KeepActive(chat.HandleId);
        var (workspace, _) = CreateWorkspaceService();
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);
        coordinator.ShowCatalog(AgentChatCatalogTab.ActiveChats);

        var shownChat = coordinator.ShowChat(chat.HandleId);

        var state = coordinator.Snapshot();
        Assert.True(state.IsCatalogVisible);
        Assert.Equal(AgentChatCatalogTab.ActiveChats, state.CatalogTab);
        Assert.True(shownChat.IsVisible);
    }

    [Fact]
    public async Task StartNewChatAsync_failure_removes_only_the_reservation_and_restores_the_visible_chat()
    {
        var clock = new ManualTimeProvider();
        var targetAgent = CreateAgent("Target agent", clock.GetUtcNow());
        var otherAgent = CreateAgent("Other agent", clock.GetUtcNow());
        var registry = new ActiveAgentChatRegistry(clock);
        var settings = FloatingAgentChatSettings.Default;
        var otherChat = registry.Open(
            CreateIdentity(otherAgent),
            Guid.NewGuid(),
            settings);
        var previouslyVisible = registry.Open(
            CreateIdentity(targetAgent),
            Guid.NewGuid(),
            settings);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        var expectedException = new InvalidOperationException("Session creation failed.");
        workspaceProxy.CreateSessionException = expectedException;
        var pool = new CoordinatorPreparationPool(targetAgent);
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            pool,
            new CoordinatorSettingsService(settings),
            clock);
        coordinator.ShowCatalog(AgentChatCatalogTab.ActiveChats);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.StartNewChatAsync(targetAgent.Id));

        Assert.Same(expectedException, actualException);
        var state = coordinator.Snapshot();
        Assert.True(state.IsCatalogVisible);
        Assert.Equal(AgentChatCatalogTab.ActiveChats, state.CatalogTab);
        var chats = state.ActiveChats;
        Assert.Equal(2, chats.Count);
        Assert.Contains(chats, chat => chat.HandleId == otherChat.HandleId && !chat.IsVisible);
        var restored = Assert.Single(chats, chat => chat.HandleId == previouslyVisible.HandleId);
        Assert.True(restored.IsVisible);
        Assert.DoesNotContain(chats, chat => !chat.ChatSessionId.HasValue);
    }

    [Fact]
    public async Task StartNewChatAsync_failure_restores_catalog_without_creating_a_chat_when_none_was_visible()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Unavailable agent", clock.GetUtcNow());
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        var expectedException = new InvalidOperationException("Session creation failed.");
        workspaceProxy.CreateSessionException = expectedException;
        await using var coordinator = CreateCoordinator(
            workspace,
            new ActiveAgentChatRegistry(clock),
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);
        coordinator.ShowCatalog(AgentChatCatalogTab.ActiveChats);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.StartNewChatAsync(agent.Id));

        Assert.Same(expectedException, actualException);
        var state = coordinator.Snapshot();
        Assert.True(state.IsCatalogVisible);
        Assert.Equal(AgentChatCatalogTab.ActiveChats, state.CatalogTab);
        Assert.Empty(state.ActiveChats);
    }

    [Fact]
    public async Task OpenChatAsync_failure_preserves_catalog_and_the_previously_visible_chat()
    {
        var clock = new ManualTimeProvider();
        var targetAgent = CreateAgent("History agent", clock.GetUtcNow());
        var visibleAgent = CreateAgent("Visible agent", clock.GetUtcNow());
        var registry = new ActiveAgentChatRegistry(clock);
        var previouslyVisible = registry.Open(
            CreateIdentity(visibleAgent),
            Guid.NewGuid(),
            FloatingAgentChatSettings.Default);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        var expectedException = new InvalidOperationException("Session lookup failed.");
        workspaceProxy.OpenSessionException = expectedException;
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            new CoordinatorPreparationPool(targetAgent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);
        coordinator.ShowCatalog(AgentChatCatalogTab.ActiveChats);
        var requestedSessionId = Guid.NewGuid();

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.OpenChatAsync(targetAgent.Id, requestedSessionId));

        Assert.Same(expectedException, actualException);
        Assert.Equal([requestedSessionId], workspaceProxy.RequestedSessionIds);
        var state = coordinator.Snapshot();
        Assert.True(state.IsCatalogVisible);
        Assert.Equal(AgentChatCatalogTab.ActiveChats, state.CatalogTab);
        var chat = Assert.Single(state.ActiveChats);
        Assert.Equal(previouslyVisible.HandleId, chat.HandleId);
        Assert.True(chat.IsVisible);
    }

    [Fact]
    public async Task InitializeAsync_loads_settings_configures_the_pool_and_warms_it()
    {
        var clock = new ManualTimeProvider();
        var settings = FloatingAgentChatSettings.Default with
        {
            HiddenActiveChatRetentionMinutes = 7,
            MaximumActiveChats = 4,
            MaximumPreparedAgents = 2,
            PreparedResourceIdleRetentionMinutes = 3
        };
        var (workspace, _) = CreateWorkspaceService();
        var pool = new CoordinatorPreparationPool(
            CreateAgent("Prepared agent", clock.GetUtcNow()));
        var settingsService = new CoordinatorSettingsService(settings);
        await using var coordinator = CreateCoordinator(
            workspace,
            new ActiveAgentChatRegistry(clock),
            pool,
            settingsService,
            clock);

        await coordinator.InitializeAsync();

        Assert.Equal(settings, coordinator.CurrentSettings);
        Assert.Equal(1, settingsService.GetSettingsCallCount);
        Assert.Equal([settings], pool.ConfiguredSettings);
        Assert.Equal(1, pool.WarmCallCount);
    }

    [Fact]
    public async Task Execution_updates_track_the_latest_run_and_ignore_stale_terminal_events()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Executing agent", clock.GetUtcNow());
        var sessionId = Guid.NewGuid();
        var registry = new ActiveAgentChatRegistry(clock);
        var chat = registry.Open(
            CreateIdentity(agent),
            sessionId,
            FloatingAgentChatSettings.Default);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            firstRunId,
            ExecutionState.Running));
        Assert.Equal(
            ActiveAgentChatRunState.Running,
            FindChat(coordinator, chat.HandleId).RunState);

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            firstRunId,
            ExecutionState.WaitingOnTool));
        Assert.Equal(
            ActiveAgentChatRunState.AwaitingApproval,
            FindChat(coordinator, chat.HandleId).RunState);

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            secondRunId,
            ExecutionState.Running));
        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            firstRunId,
            ExecutionState.Completed));
        Assert.Equal(
            ActiveAgentChatRunState.Running,
            FindChat(coordinator, chat.HandleId).RunState);

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            secondRunId,
            ExecutionState.WaitingOnTool));
        Assert.Equal(
            ActiveAgentChatRunState.AwaitingApproval,
            FindChat(coordinator, chat.HandleId).RunState);

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            secondRunId,
            ExecutionState.Completed));
        Assert.Equal(
            ActiveAgentChatRunState.Idle,
            FindChat(coordinator, chat.HandleId).RunState);
    }

    [Fact]
    public async Task ReconcileRunStateAfterOperation_releases_optimistic_running_state_without_an_execution()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Preflight agent", clock.GetUtcNow());
        var registry = new ActiveAgentChatRegistry(clock);
        var chat = registry.Open(
            CreateIdentity(agent),
            Guid.NewGuid(),
            FloatingAgentChatSettings.Default);
        registry.SetRunState(chat.HandleId, ActiveAgentChatRunState.Running);
        registry.KeepActive(chat.HandleId);
        var (workspace, _) = CreateWorkspaceService();
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);

        coordinator.ReconcileRunStateAfterOperation(chat.HandleId);

        var reconciled = FindChat(coordinator, chat.HandleId);
        Assert.False(reconciled.IsVisible);
        Assert.Equal(ActiveAgentChatRunState.Idle, reconciled.RunState);
    }

    [Fact]
    public async Task Operation_lease_rejects_overlap_and_holds_running_state_through_terminal_execution_update()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Leased agent", clock.GetUtcNow());
        var sessionId = Guid.NewGuid();
        var registry = new ActiveAgentChatRegistry(clock);
        var chat = registry.Open(
            CreateIdentity(agent),
            sessionId,
            FloatingAgentChatSettings.Default);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);
        var executionRunId = Guid.NewGuid();

        Assert.True(coordinator.TryBeginOperation(chat.HandleId));
        Assert.False(coordinator.TryBeginOperation(chat.HandleId));

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            executionRunId,
            ExecutionState.Running));
        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            executionRunId,
            ExecutionState.Completed));

        Assert.Equal(
            ActiveAgentChatRunState.Running,
            FindChat(coordinator, chat.HandleId).RunState);

        coordinator.ReconcileRunStateAfterOperation(chat.HandleId);

        Assert.Equal(
            ActiveAgentChatRunState.Idle,
            FindChat(coordinator, chat.HandleId).RunState);
        Assert.True(coordinator.TryBeginOperation(chat.HandleId));
        coordinator.ReconcileRunStateAfterOperation(chat.HandleId);
    }

    [Fact]
    public async Task ReconcileRunStateAfterOperation_preserves_a_tracked_execution_and_ignores_removed_handles()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Running agent", clock.GetUtcNow());
        var sessionId = Guid.NewGuid();
        var registry = new ActiveAgentChatRegistry(clock);
        var chat = registry.Open(
            CreateIdentity(agent),
            sessionId,
            FloatingAgentChatSettings.Default);
        var (workspace, workspaceProxy) = CreateWorkspaceService();
        await using var coordinator = CreateCoordinator(
            workspace,
            registry,
            new CoordinatorPreparationPool(agent),
            new CoordinatorSettingsService(FloatingAgentChatSettings.Default),
            clock);

        workspaceProxy.RaiseExecutionUpdated(CreateExecutionUpdate(
            agent.Id,
            sessionId,
            Guid.NewGuid(),
            ExecutionState.Running));

        coordinator.ReconcileRunStateAfterOperation(chat.HandleId);
        Assert.Equal(
            ActiveAgentChatRunState.Running,
            FindChat(coordinator, chat.HandleId).RunState);

        registry.SetRunState(chat.HandleId, ActiveAgentChatRunState.Idle);
        registry.Stop(chat.HandleId);
        coordinator.ReconcileRunStateAfterOperation(chat.HandleId);
    }

    private static FloatingAgentChatCoordinator CreateCoordinator(
        IAgentFrameworkWorkspaceService workspaceService,
        IActiveAgentChatRegistry registry,
        IAgentChatPreparationPool preparationPool,
        IFloatingAgentChatSettingsService settingsService,
        TimeProvider timeProvider)
    {
        return new FloatingAgentChatCoordinator(
            workspaceService,
            registry,
            preparationPool,
            settingsService,
            timeProvider,
            NullLogger<FloatingAgentChatCoordinator>.Instance);
    }

    private static ActiveAgentChat FindChat(
        FloatingAgentChatCoordinator coordinator,
        AgentChatHandleId handleId)
    {
        return Assert.Single(
            coordinator.Snapshot().ActiveChats,
            chat => chat.HandleId == handleId);
    }

    private static ExecutionLogEntry CreateExecutionUpdate(
        Guid agentId,
        Guid sessionId,
        Guid executionRunId,
        ExecutionState state)
    {
        return new ExecutionLogEntry(
            Guid.NewGuid(),
            agentId,
            sessionId,
            DateTimeOffset.UnixEpoch,
            state,
            state.ToString(),
            "Test update")
        {
            ExecutionRunId = executionRunId
        };
    }

    private static AgentChatIdentity CreateIdentity(AgentDefinition agent)
    {
        return new AgentChatIdentity(
            agent.Id,
            agent.Name,
            agent.RoleTitle,
            agent.AvatarImageUrl);
    }

    private static AgentDefinition CreateAgent(string name, DateTimeOffset timestamp)
    {
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Assistant",
            "Coordinator test agent",
            "Help the user.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp);
    }

    private static (IAgentFrameworkWorkspaceService Service, CoordinatorWorkspaceProxy Proxy)
        CreateWorkspaceService()
    {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CoordinatorWorkspaceProxy>();
        return (service, (CoordinatorWorkspaceProxy)(object)service);
    }

    private class CoordinatorWorkspaceProxy : DispatchProxy
    {
        private EventHandler<ExecutionLogEntry>? executionUpdated;

        public Guid CreatedSessionId { get; set; } = Guid.NewGuid();

        public Exception? CreateSessionException { get; set; }

        public Exception? OpenSessionException { get; set; }

        public List<Guid?> RequestedSessionIds { get; } = [];

        public void RaiseExecutionUpdated(ExecutionLogEntry entry)
            => executionUpdated?.Invoke(this, entry);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);

            if (targetMethod.Name == "add_ExecutionUpdated")
            {
                executionUpdated += Assert.IsType<EventHandler<ExecutionLogEntry>>(args[0]);
                return null;
            }

            if (targetMethod.Name == "remove_ExecutionUpdated")
            {
                executionUpdated -= Assert.IsType<EventHandler<ExecutionLogEntry>>(args[0]);
                return null;
            }

            if (targetMethod.Name == nameof(
                    IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync))
            {
                var agentId = Assert.IsType<Guid>(args[0]);
                var requestedSessionId = (Guid?)args[1];
                RequestedSessionIds.Add(requestedSessionId);
                if (!requestedSessionId.HasValue && CreateSessionException is { } exception)
                {
                    return Task.FromException<ChatSessionRecord>(exception);
                }

                if (requestedSessionId.HasValue && OpenSessionException is { } openException)
                {
                    return Task.FromException<ChatSessionRecord>(openException);
                }

                var sessionId = requestedSessionId ?? CreatedSessionId;
                return Task.FromResult(new ChatSessionRecord(
                    sessionId,
                    agentId,
                    "Coordinator test chat",
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch,
                    Messages: []));
            }

            throw new InvalidOperationException(
                $"Unexpected workspace service call '{targetMethod.Name}'.");
        }
    }

    private sealed class CoordinatorPreparationPool(AgentDefinition agent)
        : IAgentChatPreparationPool
    {
        public bool HasPreparedEntries => false;

        public List<FloatingAgentChatSettings> ConfiguredSettings { get; } = [];

        public int WarmCallCount { get; private set; }

        public void Configure(FloatingAgentChatSettings settings)
            => ConfiguredSettings.Add(settings);

        public Task WarmAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WarmCallCount++;
            return Task.CompletedTask;
        }

        public Task<AgentDefinition?> AcquireAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<AgentDefinition?>(agent.Id == agentId ? agent : null);
        }

        public int PruneExpired()
            => 0;

        public AgentChatPreparationPoolSnapshot Snapshot()
            => new(0, 0, 0, 0, []);
    }

    private sealed class CoordinatorSettingsService(FloatingAgentChatSettings settings)
        : IFloatingAgentChatSettingsService
    {
        public int GetSettingsCallCount { get; private set; }

        public Task<FloatingAgentChatSettings> GetSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetSettingsCallCount++;
            return Task.FromResult(settings);
        }

        public Task<FloatingAgentChatSettings> SaveSettingsAsync(
            FloatingAgentChatSettings nextSettings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settings = nextSettings;
            return Task.FromResult(nextSettings);
        }
    }
}

public sealed class ActiveAgentChatRegistryTests
{
    private static readonly FloatingAgentChatSettings TestSettings = new(
        HiddenActiveChatRetentionMinutes: 10,
        MaximumActiveChats: 3);

    [Fact]
    public void Open_allows_multiple_sessions_for_the_same_agent()
    {
        var registry = CreateRegistry();
        var agent = CreateAgent();
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();

        var first = registry.Open(agent, firstSessionId, TestSettings);
        var second = registry.Open(agent, secondSessionId, TestSettings);

        Assert.NotEqual(first.HandleId, second.HandleId);
        Assert.Equal(2, registry.Snapshot().Count);
        var sessionIds = registry.Snapshot()
            .Select(item => item.ChatSessionId!.Value)
            .ToArray();
        Assert.Contains(firstSessionId, sessionIds);
        Assert.Contains(secondSessionId, sessionIds);
        Assert.False(registry.Snapshot().Single(item => item.HandleId == first.HandleId).IsVisible);
        Assert.True(registry.Snapshot().Single(item => item.HandleId == second.HandleId).IsVisible);
    }

    [Fact]
    public void Open_reuses_and_shows_an_existing_agent_session()
    {
        var clock = new ManualTimeProvider();
        var registry = new ActiveAgentChatRegistry(clock);
        var agent = CreateAgent(name: "Original name");
        var sessionId = Guid.NewGuid();
        var first = registry.Open(agent, sessionId, TestSettings);
        registry.KeepActive(first.HandleId);
        clock.Advance(TimeSpan.FromMinutes(1));

        var reopened = registry.Open(
            CreateAgent(agent.AgentId, "Updated name"),
            sessionId,
            TestSettings);

        Assert.Equal(first.HandleId, reopened.HandleId);
        Assert.Equal("Updated name", reopened.Agent.Name);
        Assert.True(reopened.IsVisible);
        Assert.Null(reopened.HiddenAtUtc);
        Assert.Single(registry.Snapshot());
    }

    [Fact]
    public void KeepActive_hides_a_chat_and_Stop_removes_it()
    {
        var registry = CreateRegistry();
        var opened = registry.Open(CreateAgent(), Guid.NewGuid(), TestSettings);

        var kept = registry.KeepActive(opened.HandleId);

        Assert.False(kept.IsVisible);
        Assert.NotNull(kept.HiddenAtUtc);

        registry.Stop(opened.HandleId);

        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Stop_rejects_a_running_chat()
    {
        var registry = CreateRegistry();
        var opened = registry.Open(CreateAgent(), Guid.NewGuid(), TestSettings);
        registry.SetRunState(opened.HandleId, ActiveAgentChatRunState.Running);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Stop(opened.HandleId));

        Assert.Contains("cannot be stopped", exception.Message);
        Assert.Single(registry.Snapshot());
    }

    [Fact]
    public void Stop_allows_an_approval_waiting_chat()
    {
        var registry = CreateRegistry();
        var opened = registry.Open(CreateAgent(), Guid.NewGuid(), TestSettings);
        registry.SetRunState(opened.HandleId, ActiveAgentChatRunState.AwaitingApproval);

        registry.Stop(opened.HandleId);

        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Open_rejects_a_new_chat_at_maximum_capacity()
    {
        var registry = CreateRegistry();
        var settings = TestSettings with { MaximumActiveChats = 2 };
        registry.Open(CreateAgent(), Guid.NewGuid(), settings);
        registry.Open(CreateAgent(), Guid.NewGuid(), settings);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Open(CreateAgent(), Guid.NewGuid(), settings));

        Assert.Contains("maximum of 2", exception.Message);
        Assert.Equal(2, registry.Snapshot().Count);
    }

    [Fact]
    public void AttachSession_allows_initial_and_idempotent_attachment_but_rejects_rebinding()
    {
        var registry = CreateRegistry();
        var opened = registry.Open(CreateAgent(), chatSessionId: null, TestSettings);
        var sessionId = Guid.NewGuid();

        var attached = registry.AttachSession(opened.HandleId, sessionId);
        var attachedAgain = registry.AttachSession(opened.HandleId, sessionId);

        Assert.Equal(sessionId, attached.ChatSessionId);
        Assert.Equal(attached, attachedAgain);
        Assert.Throws<InvalidOperationException>(() =>
            registry.AttachSession(opened.HandleId, Guid.NewGuid()));
        Assert.Equal(sessionId, registry.Snapshot().Single().ChatSessionId);
    }

    [Fact]
    public void Session_identity_is_global_across_agents_and_handles()
    {
        var registry = CreateRegistry();
        var sessionId = Guid.NewGuid();
        var first = registry.Open(CreateAgent(), sessionId, TestSettings);
        var second = registry.Open(CreateAgent(), chatSessionId: null, TestSettings);

        Assert.Throws<InvalidOperationException>(() =>
            registry.AttachSession(second.HandleId, sessionId));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Open(CreateAgent(), sessionId, TestSettings));

        var snapshot = registry.Snapshot();
        Assert.Equal(sessionId, snapshot.Single(item => item.HandleId == first.HandleId).ChatSessionId);
        Assert.Null(snapshot.Single(item => item.HandleId == second.HandleId).ChatSessionId);
    }

    [Fact]
    public void Registry_rejects_a_default_handle_identifier()
    {
        var registry = CreateRegistry();

        Assert.Throws<ArgumentException>(() => registry.Show(default));
    }

    [Fact]
    public void Open_notifies_when_it_prunes_before_failing_capacity_validation()
    {
        var clock = new ManualTimeProvider();
        var registry = new ActiveAgentChatRegistry(clock);
        var initialSettings = TestSettings with { MaximumActiveChats = 3 };
        var expired = registry.Open(CreateAgent(), Guid.NewGuid(), initialSettings);
        registry.KeepActive(expired.HandleId);
        var busy = registry.Open(CreateAgent(), Guid.NewGuid(), initialSettings);
        registry.SetRunState(busy.HandleId, ActiveAgentChatRunState.Running);
        registry.KeepActive(busy.HandleId);
        var visible = registry.Open(CreateAgent(), Guid.NewGuid(), initialSettings);
        clock.Advance(TestSettings.HiddenActiveChatRetention);
        var changedCount = 0;
        registry.Changed += (_, _) => changedCount++;

        Assert.Throws<InvalidOperationException>(() =>
            registry.Open(
                CreateAgent(),
                Guid.NewGuid(),
                TestSettings with { MaximumActiveChats = 2 }));

        Assert.Equal(1, changedCount);
        Assert.DoesNotContain(registry.Snapshot(), item => item.HandleId == expired.HandleId);
        Assert.Contains(registry.Snapshot(), item => item.HandleId == busy.HandleId);
        Assert.Contains(registry.Snapshot(), item => item.HandleId == visible.HandleId);
    }

    [Fact]
    public void PruneExpired_removes_a_hidden_idle_chat_at_the_retention_boundary()
    {
        var clock = new ManualTimeProvider();
        var registry = new ActiveAgentChatRegistry(clock);
        var opened = registry.Open(CreateAgent(), Guid.NewGuid(), TestSettings);
        registry.KeepActive(opened.HandleId);

        clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromTicks(1));
        Assert.Equal(0, registry.PruneExpired(TestSettings));

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(1, registry.PruneExpired(TestSettings));
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void PruneExpired_keeps_busy_hidden_chat_and_restarts_retention_when_it_becomes_idle()
    {
        var clock = new ManualTimeProvider();
        var registry = new ActiveAgentChatRegistry(clock);
        var opened = registry.Open(CreateAgent(), Guid.NewGuid(), TestSettings);
        registry.SetRunState(opened.HandleId, ActiveAgentChatRunState.Running);
        registry.KeepActive(opened.HandleId);

        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(0, registry.PruneExpired(TestSettings));

        registry.SetRunState(opened.HandleId, ActiveAgentChatRunState.Idle);
        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(1, registry.PruneExpired(TestSettings));
    }

    [Fact]
    public void PruneExpired_never_expires_a_hidden_approval_and_starts_retention_when_it_becomes_idle()
    {
        var clock = new ManualTimeProvider();
        var registry = new ActiveAgentChatRegistry(clock);
        var opened = registry.Open(CreateAgent(), Guid.NewGuid(), TestSettings);
        registry.SetRunState(opened.HandleId, ActiveAgentChatRunState.AwaitingApproval);
        var hidden = registry.KeepActive(opened.HandleId);

        clock.Advance(TestSettings.HiddenActiveChatRetention * 3);

        Assert.Equal(0, registry.PruneExpired(TestSettings));
        var awaitingApproval = Assert.Single(registry.Snapshot());
        Assert.Equal(ActiveAgentChatRunState.AwaitingApproval, awaitingApproval.RunState);
        Assert.Equal(hidden.HiddenAtUtc, awaitingApproval.HiddenAtUtc);

        registry.SetRunState(opened.HandleId, ActiveAgentChatRunState.Idle);
        clock.Advance(TestSettings.HiddenActiveChatRetention - TimeSpan.FromTicks(1));
        Assert.Equal(0, registry.PruneExpired(TestSettings));

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(1, registry.PruneExpired(TestSettings));
        Assert.Empty(registry.Snapshot());
    }

    private static ActiveAgentChatRegistry CreateRegistry()
        => new(new ManualTimeProvider());

    private static AgentChatIdentity CreateAgent(
        Guid? agentId = null,
        string name = "Test agent")
    {
        return new AgentChatIdentity(
            agentId ?? Guid.NewGuid(),
            name,
            "Assistant",
            string.Empty);
    }
}

public sealed class FloatingAgentChatSettingsValidatorTests
{
    public static IEnumerable<object[]> InvalidSettings()
    {
        var defaults = FloatingAgentChatSettings.Default;
        yield return
        [
            defaults with { HiddenActiveChatRetentionMinutes = 0 },
            nameof(FloatingAgentChatSettings.HiddenActiveChatRetentionMinutes)
        ];
        yield return
        [
            defaults with
            {
                HiddenActiveChatRetentionMinutes =
                    FloatingAgentChatSettingsValidator.MaximumRetentionMinutes + 1
            },
            nameof(FloatingAgentChatSettings.HiddenActiveChatRetentionMinutes)
        ];
        yield return
        [
            defaults with { MaximumActiveChats = 0 },
            nameof(FloatingAgentChatSettings.MaximumActiveChats)
        ];
        yield return
        [
            defaults with
            {
                MaximumActiveChats =
                    FloatingAgentChatSettingsValidator.MaximumActiveChatLimit + 1
            },
            nameof(FloatingAgentChatSettings.MaximumActiveChats)
        ];
        yield return
        [
            defaults with { MaximumPreparedAgents = -1 },
            nameof(FloatingAgentChatSettings.MaximumPreparedAgents)
        ];
        yield return
        [
            defaults with
            {
                MaximumPreparedAgents =
                    FloatingAgentChatSettingsValidator.MaximumPreparedAgentLimit + 1
            },
            nameof(FloatingAgentChatSettings.MaximumPreparedAgents)
        ];
        yield return
        [
            defaults with { PreparedResourceIdleRetentionMinutes = 0 },
            nameof(FloatingAgentChatSettings.PreparedResourceIdleRetentionMinutes)
        ];
        yield return
        [
            defaults with
            {
                PreparedResourceIdleRetentionMinutes =
                    FloatingAgentChatSettingsValidator.MaximumRetentionMinutes + 1
            },
            nameof(FloatingAgentChatSettings.PreparedResourceIdleRetentionMinutes)
        ];
    }

    [Fact]
    public void Normalize_uses_default_settings_when_value_is_null()
    {
        Assert.Equal(
            FloatingAgentChatSettings.Default,
            FloatingAgentChatSettingsValidator.Normalize(null));
    }

    [Fact]
    public void Json_serialization_omits_computed_duration_properties()
    {
        var settingsJson = JsonSerializer.Serialize(FloatingAgentChatSettings.Default);

        Assert.DoesNotContain(
            $"\"{nameof(FloatingAgentChatSettings.HiddenActiveChatRetention)}\"",
            settingsJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"\"{nameof(FloatingAgentChatSettings.PreparedResourceIdleRetention)}\"",
            settingsJson,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_accepts_all_inclusive_boundaries()
    {
        FloatingAgentChatSettingsValidator.Validate(new FloatingAgentChatSettings(
            HiddenActiveChatRetentionMinutes: 1,
            MaximumActiveChats: 1,
            MaximumPreparedAgents: 0,
            PreparedResourceIdleRetentionMinutes: 1));
        FloatingAgentChatSettingsValidator.Validate(new FloatingAgentChatSettings(
            HiddenActiveChatRetentionMinutes:
                FloatingAgentChatSettingsValidator.MaximumRetentionMinutes,
            MaximumActiveChats:
                FloatingAgentChatSettingsValidator.MaximumActiveChatLimit,
            MaximumPreparedAgents:
                FloatingAgentChatSettingsValidator.MaximumPreparedAgentLimit,
            PreparedResourceIdleRetentionMinutes:
                FloatingAgentChatSettingsValidator.MaximumRetentionMinutes));
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Validate_rejects_values_outside_inclusive_boundaries(
        FloatingAgentChatSettings settings,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FloatingAgentChatSettingsValidator.Validate(settings));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }
}

public sealed class AgentChatPreparationPoolTests
{
    [Fact]
    public async Task Disabled_pool_does_not_warm_or_retain_activation_metadata()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Agent A", clock.GetUtcNow());
        var referenceData = new StubAgentReferenceDataProvider(clock, [agent]);
        var pool = new AgentChatPreparationPool(referenceData, clock);
        pool.Configure(FloatingAgentChatSettings.Default with { MaximumPreparedAgents = 0 });

        await pool.WarmAsync();
        var acquired = await pool.AcquireAsync(agent.Id);

        Assert.Equivalent(agent, acquired, strict: true);
        Assert.Equal(1, referenceData.RequestCount);
        Assert.Equal(0, pool.Snapshot().PreparedCount);
    }

    [Fact]
    public async Task Adaptive_pool_favors_the_most_used_agent_and_reports_hits()
    {
        var clock = new ManualTimeProvider();
        var agentA = CreateAgent("Agent A", clock.GetUtcNow());
        var agentB = CreateAgent("Agent B", clock.GetUtcNow().AddMinutes(1));
        var referenceData = new StubAgentReferenceDataProvider(clock, [agentA, agentB]);
        var pool = new AgentChatPreparationPool(referenceData, clock);
        pool.Configure(FloatingAgentChatSettings.Default with
        {
            MaximumPreparedAgents = 1,
            AdaptivePreparationEnabled = true
        });

        await pool.WarmAsync();
        await pool.AcquireAsync(agentA.Id);
        await pool.AcquireAsync(agentA.Id);
        await pool.AcquireAsync(agentB.Id);

        var snapshot = pool.Snapshot();
        Assert.Equal(1, snapshot.PreparedCount);
        Assert.Equal([agentA.Id], snapshot.PreparedAgentIds);
        Assert.True(snapshot.CacheHits >= 1);
        Assert.True(snapshot.CacheMisses >= 2);
    }

    [Fact]
    public async Task Pool_revalidates_catalog_state_before_returning_stale_metadata()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Agent A", clock.GetUtcNow());
        var referenceData = new StubAgentReferenceDataProvider(clock, [agent]);
        var pool = new AgentChatPreparationPool(referenceData, clock);
        pool.Configure(FloatingAgentChatSettings.Default with { MaximumPreparedAgents = 1 });
        await pool.WarmAsync();
        clock.Advance(TimeSpan.FromSeconds(21));
        referenceData.Agents = [];

        var acquired = await pool.AcquireAsync(agent.Id);

        Assert.Null(acquired);
        Assert.Empty(pool.Snapshot().PreparedAgentIds);
        Assert.Equal(2, referenceData.RequestCount);
    }

    [Fact]
    public async Task PruneExpired_honors_the_prepared_resource_idle_retention()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Agent A", clock.GetUtcNow());
        var pool = new AgentChatPreparationPool(
            new StubAgentReferenceDataProvider(clock, [agent]),
            clock);
        pool.Configure(FloatingAgentChatSettings.Default with
        {
            MaximumPreparedAgents = 1,
            PreparedResourceIdleRetentionMinutes = 1
        });
        await pool.WarmAsync();

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, pool.PruneExpired());
        Assert.Equal(0, pool.Snapshot().PreparedCount);
    }

    [Fact]
    public async Task Reference_data_invalidation_clears_prepared_agent_metadata()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Agent A", clock.GetUtcNow());
        var invalidator = new AgentReferenceDataCache();
        using var pool = new AgentChatPreparationPool(
            new StubAgentReferenceDataProvider(clock, [agent]),
            clock,
            invalidator);
        pool.Configure(FloatingAgentChatSettings.Default with { MaximumPreparedAgents = 1 });
        await pool.WarmAsync();

        invalidator.Invalidate();

        Assert.Equal(0, pool.Snapshot().PreparedCount);
    }

    [Fact]
    public async Task Catalog_refresh_preserves_idle_age_for_existing_prepared_agents()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Agent A", clock.GetUtcNow());
        using var pool = new AgentChatPreparationPool(
            new StubAgentReferenceDataProvider(clock, [agent]),
            clock);
        pool.Configure(FloatingAgentChatSettings.Default with
        {
            MaximumPreparedAgents = 1,
            PreparedResourceIdleRetentionMinutes = 1
        });
        await pool.WarmAsync();
        clock.Advance(TimeSpan.FromSeconds(30));

        await pool.WarmAsync();
        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(1, pool.PruneExpired());
    }

    [Fact]
    public async Task Invalidation_during_refresh_discards_the_stale_catalog_result()
    {
        var clock = new ManualTimeProvider();
        var agentId = Guid.NewGuid();
        var original = CreateAgent("Original", clock.GetUtcNow()) with { Id = agentId };
        var updated = original with { Name = "Updated" };
        var referenceData = new BlockingFirstAgentReferenceDataProvider(clock, original);
        var invalidator = new AgentReferenceDataCache();
        using var pool = new AgentChatPreparationPool(referenceData, clock, invalidator);
        pool.Configure(FloatingAgentChatSettings.Default with { MaximumPreparedAgents = 1 });

        var warmTask = pool.WarmAsync();
        await referenceData.FirstRequestStarted;
        referenceData.Agent = updated;
        invalidator.Invalidate();
        referenceData.ReleaseFirstRequest();
        await warmTask;

        var acquired = await pool.AcquireAsync(agentId);

        Assert.Equal("Updated", acquired?.Name);
        Assert.Equal(2, referenceData.RequestCount);
    }

    [Fact]
    public async Task Concurrent_acquires_for_the_same_agent_share_one_reference_data_refresh()
    {
        var clock = new ManualTimeProvider();
        var agent = CreateAgent("Agent A", clock.GetUtcNow());
        var referenceData = new BlockingFirstAgentReferenceDataProvider(clock, agent);
        using var pool = new AgentChatPreparationPool(referenceData, clock);
        pool.Configure(FloatingAgentChatSettings.Default with { MaximumPreparedAgents = 1 });

        var firstAcquire = pool.AcquireAsync(agent.Id);
        await referenceData.FirstRequestStarted;
        var secondAcquire = pool.AcquireAsync(agent.Id);
        referenceData.ReleaseFirstRequest();

        var acquiredAgents = await Task.WhenAll(firstAcquire, secondAcquire);

        Assert.Equal(1, referenceData.RequestCount);
        Assert.All(acquiredAgents, acquired => Assert.Equivalent(agent, acquired, strict: true));
    }

    private static AgentDefinition CreateAgent(string name, DateTimeOffset timestamp)
    {
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Assistant",
            "Prepared test agent",
            "Help the user.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp);
    }

    private sealed class StubAgentReferenceDataProvider(
        TimeProvider timeProvider,
        IReadOnlyList<AgentDefinition> agents) : IAgentReferenceDataProvider
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = agents;

        public int RequestCount { get; private set; }

        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                Agents,
                [],
                new Dictionary<Guid, ProviderProfile>(),
                timeProvider.GetUtcNow(),
                TimeSpan.Zero));
        }
    }

    private sealed class BlockingFirstAgentReferenceDataProvider(
        TimeProvider timeProvider,
        AgentDefinition agent) : IAgentReferenceDataProvider
    {
        private readonly TaskCompletionSource firstRequestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirstRequest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AgentDefinition Agent { get; set; } = agent;

        public int RequestCount { get; private set; }

        public Task FirstRequestStarted => firstRequestStarted.Task;

        public void ReleaseFirstRequest()
            => releaseFirstRequest.TrySetResult();

        public async Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            var capturedAgent = Agent;
            if (RequestCount == 1)
            {
                firstRequestStarted.TrySetResult();
                await releaseFirstRequest.Task.WaitAsync(cancellationToken);
            }

            return new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                [capturedAgent],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                timeProvider.GetUtcNow(),
                TimeSpan.Zero);
        }
    }
}

public sealed class AgentChatExecutionNotificationHubTests
{
    [Fact]
    public async Task Publish_is_source_keyed_and_disposable()
    {
        var hub = new AgentChatExecutionNotificationHub(
            NullLogger<AgentChatExecutionNotificationHub>.Instance);
        var projectSource = new AgentChatContextSource(
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")));
        var crmSource = new AgentChatContextSource(
            new AgentChatContextSourceKind("crm-partner"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")));
        var notificationCount = 0;
        var subscription = hub.Subscribe(
            projectSource,
            _ =>
            {
                notificationCount++;
                return Task.CompletedTask;
            });

        await hub.PublishAsync(CreateNotification(crmSource));
        await hub.PublishAsync(CreateNotification(projectSource));
        subscription.Dispose();
        await hub.PublishAsync(CreateNotification(projectSource));

        Assert.Equal(1, notificationCount);
    }

    private static AgentChatExecutionCompleted CreateNotification(
        AgentChatContextSource source)
    {
        return new AgentChatExecutionCompleted(
            AgentChatContextScopeId.Create(),
            source,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }
}

file sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset utcNow = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
        => utcNow;

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        utcNow += duration;
    }
}
