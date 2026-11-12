using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public abstract record AgentExecutionPublicationRecovery {
    private AgentExecutionPublicationRecovery() {
    }

    public sealed record SharedCatalog : AgentExecutionPublicationRecovery;

    public sealed record Workspace : AgentExecutionPublicationRecovery {
        public Workspace(string rootPath, WorkspaceScopeDescriptor scope) {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            ArgumentNullException.ThrowIfNull(scope);
            if (!Path.IsPathFullyQualified(rootPath) || scope.Kind is not (WorkspaceScopeKind.Project or WorkspaceScopeKind.Sandbox) ||
                    new WorkspaceScopeDescriptor(scope.Kind, scope.Key) != scope) {
                throw new ArgumentException("Execution publication recovery requires its exact original workspace root and supported scope.");
            }
            RootPath = Path.GetFullPath(rootPath);
            Scope = new(scope.Kind, scope.Key);
        }

        public string RootPath { get; }
        public WorkspaceScopeDescriptor Scope { get; }
        public override string ToString() => "Original execution publication workspace";
    }
}
