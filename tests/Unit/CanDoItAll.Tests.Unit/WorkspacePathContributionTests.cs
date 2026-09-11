using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class WorkspacePathContributionTests {
    private static readonly WorkspaceScopeDescriptor Scope = WorkspaceScopeDescriptor.Project("3324868f-66e2-478a-bb8f-14f32a5db1e9");
    private const string CurrentPath = "managed-files/project-media/files/3324868f66e2478abb8f14f32a5db1e9/current.md";

    [Theory]
    [InlineData(CurrentPath, ToolInvocationDecisionKind.Allow)]
    [InlineData("managed-files/project-media/files/be2ebfd7776643f99b2e8051d0b0d99d/foreign.md", ToolInvocationDecisionKind.Deny)]
    [InlineData("project-structure-context-brief.md", ToolInvocationDecisionKind.Deny)]
    public async Task Projects_registration_supplies_original_policy_without_Structure_tool_registration(
        string path, ToolInvocationDecisionKind expected) {
        var services = new ServiceCollection();
        services.AddProjectsModule();
        using var provider = services.BuildServiceProvider();
        var contributors = provider.GetServices<IToolInvocationPolicyContextContributor>().ToArray();
        Assert.IsType<ProjectWorkspacePathContributor>(Assert.Single(contributors));
        Assert.Empty(provider.GetServices<IAgentRuntimeToolProvider>());
        var context = Context(path);
        var original = new ProjectWorkspaceScopePolicy().EvaluateProjectScope(context,
            AgentToolInvocationPolicyMetadata.BuildSignature(context.ToolName, context.RedactedArguments));
        var pipeline = new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy(), contributors);

        var result = await pipeline.ComposeAndEvaluateAsync(context, null, CancellationToken.None);

        Assert.Equal(expected, result.Decision.Kind);
        Assert.Equal(Scope, result.EffectiveContext.WorkspacePaths?.Scope);
        if (original is not null) {
            Assert.Equal(original, result.Decision);
        }
        var repeated = await pipeline.ComposeAndEvaluateAsync(result.EffectiveContext, null, CancellationToken.None);
        Assert.Equal(result.Decision, repeated.Decision);
    }

    [Theory]
    [InlineData(null, ToolInvocationDecisionKind.Allow)]
    [InlineData(ToolInvocationDecisionKind.Deny, ToolInvocationDecisionKind.Deny)]
    [InlineData(ToolInvocationDecisionKind.SkipExecution, ToolInvocationDecisionKind.SkipExecution)]
    public async Task Restriction_may_defer_or_deny_without_granting_authority(
        ToolInvocationDecisionKind? contributed, ToolInvocationDecisionKind expected) {
        var contribution = Contribution((_, signature) => contributed is { } kind
            ? new(kind, "owner restriction", signature) : null);
        var pipeline = Pipeline(contribution);

        var result = await pipeline.ComposeAndEvaluateAsync(Context(CurrentPath), null, CancellationToken.None);

        Assert.Equal(expected, result.Decision.Kind);
    }

    [Theory]
    [InlineData(ToolInvocationDecisionKind.Allow)]
    [InlineData(ToolInvocationDecisionKind.RequireApproval)]
    [InlineData(ToolInvocationDecisionKind.SanitizeResult)]
    [InlineData((ToolInvocationDecisionKind)999)]
    public async Task Restriction_cannot_return_an_authorizing_decision(ToolInvocationDecisionKind kind) {
        var pipeline = Pipeline(Contribution((_, signature) => new(kind, "invalid contribution", signature)));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ComposeAndEvaluateAsync(Context(CurrentPath), null, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_delegate_is_rejected_even_when_no_path_would_be_denied(bool invocation) {
        var contribution = Contribution((_, _) => null);
        contribution = invocation
            ? contribution with { RestrictInvocation = null! }
            : contribution with { RestrictSearchRoots = null! };
        Assert.Throws<InvalidOperationException>(() => WorkspacePathScopeContribution.Resolve(Scope, [new Stub(contribution)]));
    }

    [Fact]
    public async Task Missing_duplicate_and_mismatched_owner_contributions_fail_explicitly() {
        var context = Context(CurrentPath);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy())
                .ComposeAndEvaluateAsync(context, null, CancellationToken.None));
        var contribution = Contribution((_, _) => null);
        Assert.Throws<InvalidOperationException>(() => WorkspacePathScopeContribution.Resolve(Scope,
            [new Stub(contribution), new Stub(contribution)]));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy(),
                [new ProjectWorkspacePathContributor(), new Stub(contribution)])
                .ComposeAndEvaluateAsync(context, null, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => WorkspacePathScopeContribution.Resolve(Scope,
            [new Stub(contribution with { Scope = WorkspaceScopeDescriptor.Project("another-project") })]));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Pipeline(contribution).ComposeAndEvaluateAsync(context with {
                WorkspacePaths = contribution with { Scope = WorkspaceScopeDescriptor.Sandbox }
            }, null, CancellationToken.None));
    }

    [Fact]
    public void Invocation_scope_binding_preserves_the_supplied_legacy_key_exactly() {
        var context = Context(CurrentPath) with { ContextWorkspaceScopeKey = " Legacy.Project " };
        var contributed = new ProjectWorkspacePathContributor().Contribute(context, null);

        Assert.Equal(WorkspaceScopeKind.Project, contributed.WorkspacePaths?.Scope.Kind);
        Assert.Equal(context.ContextWorkspaceScopeKey, contributed.WorkspacePaths?.Scope.Key);
    }

    [Fact]
    public async Task Earlier_known_tool_denial_still_precedes_owner_invocation_restriction() {
        var calls = 0;
        var pipeline = Pipeline(Contribution((_, _) => {
            calls++;
            throw new InvalidOperationException("The earlier denial must win.");
        }));

        var result = await pipeline.ComposeAndEvaluateAsync(Context(CurrentPath) with { IsKnownTool = false }, null, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, result.Decision.Kind);
        Assert.Contains("not part of the composed capability set", result.Decision.Reason, StringComparison.Ordinal);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("sibling", "expanded the configured search boundary")]
    [InlineData("../escape", "non-canonical relative search root")]
    public void Rag_owner_cannot_expand_explicit_or_physical_search_boundary(string returnedRoot, string reason) {
        var root = CreateRoot();
        try {
            var explicitRoot = Directory.CreateDirectory(Path.Combine(root, "selected")).FullName;
            Directory.CreateDirectory(Path.Combine(root, "sibling"));
            var contribution = Contribution((_, _) => null) with { RestrictSearchRoots = _ => [returnedRoot] };
            var retriever = new WorkspaceRagRetriever(root, Scope, TestWorkspaceServices.PhysicalPathPolicyFactory, contribution);

            var exception = Assert.Throws<InvalidOperationException>(() => retriever.ResolveSearchRoots(explicitRoot));

            Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Rag_rejects_absolute_or_missing_owner_roots_and_keeps_a_valid_explicit_subroot() {
        var root = CreateRoot();
        try {
            var selected = Directory.CreateDirectory(Path.Combine(root, "selected")).FullName;
            var child = Directory.CreateDirectory(Path.Combine(selected, "child")).FullName;
            WorkspaceRagRetriever Retriever(Func<WorkspaceSearchRootRequest, IReadOnlyList<string>> roots)
                => new(root, Scope, TestWorkspaceServices.PhysicalPathPolicyFactory,
                    Contribution((_, _) => null) with { RestrictSearchRoots = roots });
            Assert.Throws<InvalidOperationException>(() => Retriever(_ => [child]).ResolveSearchRoots(selected));
            Assert.Throws<InvalidOperationException>(() => Retriever(_ => null!).ResolveSearchRoots(selected));
            Assert.Throws<InvalidOperationException>(() => new WorkspaceRagRetriever(root, Scope,
                TestWorkspaceServices.PhysicalPathPolicyFactory).ResolveSearchRoots(root));
            Assert.Equal(child, Assert.Single(Retriever(_ => ["selected/child"]).ResolveSearchRoots(selected)));
            var file = Path.Combine(selected, "document.md");
            File.WriteAllText(file, "selected document");
            var ownerRetriever = new WorkspaceRagRetriever(root, Scope, TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ProjectWorkspacePathContributor().ContributeWorkspacePaths(Scope));
            Assert.Equal(file, Assert.Single(ownerRetriever.ResolveSearchRoots(file)));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AgentToolInvocationPolicyPipeline Pipeline(WorkspacePathScopeContribution contribution)
        => new(new DefaultAgentToolInvocationPolicy(), [new Stub(contribution)]);

    private static WorkspacePathScopeContribution Contribution(
        Func<ToolInvocationPolicyContext, string, ToolInvocationPolicyDecision?> invocation)
        => new(Scope, invocation, request => [request.RelativePath]);

    private static ToolInvocationPolicyContext Context(string path) {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["path"] = path };
        return new(Guid.NewGuid(), "Path policy test", ToolContractCatalog.WorkspaceReadFile, arguments,
            ToolInvocationClassification.Read, true, false, false, "run", "project-structure", string.Empty, string.Empty,
            ContextWorkspaceScopeKind: Scope.Kind.ToString(), ContextWorkspaceScopeKey: Scope.Key) {
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(ToolContractCatalog.WorkspaceReadFile,
                arguments.Select(pair => new KeyValuePair<string, object?>(pair.Key, pair.Value)))
        };
    }

    private static string CreateRoot() {
        var root = Path.Combine(Path.GetTempPath(), nameof(WorkspacePathContributionTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class Stub(WorkspacePathScopeContribution contribution) : IToolInvocationPolicyContextContributor {
        public ToolInvocationPolicyContext Contribute(ToolInvocationPolicyContext context,
            WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
            => context with { WorkspacePaths = contribution };

        public WorkspacePathScopeContribution? ContributeWorkspacePaths(WorkspaceScopeDescriptor scope) => contribution;
    }
}
