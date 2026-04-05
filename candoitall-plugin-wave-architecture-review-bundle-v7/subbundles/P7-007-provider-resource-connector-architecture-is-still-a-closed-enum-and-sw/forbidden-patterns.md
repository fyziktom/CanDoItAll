# Forbidden patterns

The following patterns must be removed or made impossible:
- enum ProviderKind
- enum ResourceKind
- connector extensibility requiring enum expansion
- no connector descriptor/manifest implementation

## Evidence anchors
- src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-63
- src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-48
- src/CanDoItAll.Modules.Resources/ResourceModels.cs:10-81
