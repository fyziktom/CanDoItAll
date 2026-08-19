using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record ExternalWorkspaceRootSelection(
    IReadOnlyList<string> AllowedAliases,
    IReadOnlyList<ExternalTargetRootBinding> RootBindings);
