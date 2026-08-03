using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectWorkspaceScopePolicyTests
{
    [Fact]
    public void EvaluateProjectScope_denies_foreign_project_media_directly()
    {
        var policy = new ProjectWorkspaceScopePolicy();
        var context = CreateContext(
            ToolContractCatalog.WorkspaceReadSpreadsheetRange,
            "managed-files/project-media/files/be2ebfd7776643f99b2e8051d0b0d99d/foreign.xlsx");

        var decision = policy.EvaluateProjectScope(context, "signature");

        Assert.NotNull(decision);
        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not owned by project", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateProjectScope_allows_current_project_media_directly()
    {
        var policy = new ProjectWorkspaceScopePolicy();
        var context = CreateContext(
            ToolContractCatalog.WorkspaceReadFile,
            "./managed-files/project-media/files/3324868f66e2478abb8f14f32a5db1e9/current.md");

        var decision = policy.EvaluateProjectScope(context, "signature");

        Assert.Null(decision);
    }

    [Fact]
    public void EvaluateProjectScope_ignores_non_project_scope()
    {
        var policy = new ProjectWorkspaceScopePolicy();
        var context = CreateContext(
            ToolContractCatalog.WorkspaceReadFile,
            "managed-files/project-media/files/be2ebfd7776643f99b2e8051d0b0d99d/foreign.md",
            WorkspaceScopeDescriptor.Sandbox);

        var decision = policy.EvaluateProjectScope(context, "signature");

        Assert.Null(decision);
    }

    private static ToolInvocationPolicyContext CreateContext(
        string toolName,
        string path,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        workspaceScope ??= WorkspaceScopeDescriptor.Project(
            "3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = path
        };

        return new ToolInvocationPolicyContext(
            AgentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AgentName: "Project scope policy test",
            ToolName: toolName,
            RedactedArguments: arguments,
            Classification: ToolInvocationClassification.Read,
            IsKnownTool: true,
            AutoApprovalAllowed: false,
            ApprovalWrapperAvailable: false,
            ExecutionRunId: "run-001",
            SourceKind: "project-structure",
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            ContextWorkspaceScopeKind: workspaceScope.Kind.ToString(),
            ContextWorkspaceScopeKey: workspaceScope.Key)
        {
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(
                toolName,
                arguments.Select(argument =>
                    new KeyValuePair<string, object?>(argument.Key, argument.Value)))
        };
    }
}
