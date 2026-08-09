using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Tests.Unit;

internal static class WorkspaceRuntimeServicesTestFactory
{
    public static WorkspaceRuntimeServices Create(string workspaceRoot, WorkspaceScopeDescriptor? scope = null)
        => new WorkspaceRuntimeServicesFactory(
                [],
                new ManagedCodeMarkItDownDocumentMarkdownConverter(),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ExternalTargetPathRegistryFactory())
            .Create(new WorkspaceExecutionScope(workspaceRoot, scope ?? WorkspaceScopeDescriptor.Sandbox));
}
