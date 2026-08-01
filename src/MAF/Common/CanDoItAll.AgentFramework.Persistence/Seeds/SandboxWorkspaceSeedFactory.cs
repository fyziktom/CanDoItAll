using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceSeedFactory
{
    public static SandboxWorkspaceDocument Create() => SandboxWorkspaceSeedBuilder.Build();

    public static SandboxWorkspaceDocument Normalize(SandboxWorkspaceDocument document)
        => SandboxWorkspaceSeedNormalizer.Normalize(document);

    public static SandboxWorkspaceCatalog NormalizeCatalog(SandboxWorkspaceCatalog catalog)
        => SandboxWorkspaceSeedNormalizer.NormalizeCatalog(catalog);

    public static SandboxWorkspaceExecutionState NormalizeExecutionState(SandboxWorkspaceExecutionState executionState)
        => SandboxWorkspaceSeedNormalizer.NormalizeExecutionState(executionState);
}
