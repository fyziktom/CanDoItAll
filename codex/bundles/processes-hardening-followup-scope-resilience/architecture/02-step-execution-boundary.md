# Step Execution Boundary Model

## Problem

The prompt says architecture/planning/review steps should not mutate product files unless explicitly required. However, prompt-only enforcement is insufficient.

## Proposed Generic Model

Add a normalized runtime policy object:

```csharp
public sealed record ProcessStepExecutionBoundary(
    ProcessStepOperationSet AllowedOperations,
    ProcessStepOperationSet DeniedOperations,
    IReadOnlyList<string> AllowedArtifactWriteRoots,
    IReadOnlyList<string> AllowedProductMutationRoots,
    IReadOnlyList<string> ReadOnlyRoots,
    ProcessScopeViolationAction ScopeViolationAction,
    string SourceSummary);
```

Suggested operation enum:

```csharp
[Flags]
public enum ProcessStepOperation
{
    None = 0,
    ReadProcessContext = 1,
    ReadUpstreamArtifacts = 2,
    WriteManagedArtifacts = 4,
    MutateDeclaredTarget = 8,
    RunValidation = 16,
    LaunchRuntime = 32,
    CaptureBrowserProof = 64,
    PerformExternalAction = 128,
    RecoverArtifactsOnly = 256
}
```

## Boundary Examples

| Step type | Allowed | Denied |
| --- | --- | --- |
| Scope/intake | ReadProcessContext, WriteManagedArtifacts | MutateDeclaredTarget, LaunchRuntime |
| Architecture/design | ReadProcessContext, ReadUpstreamArtifacts, WriteManagedArtifacts | MutateDeclaredTarget unless explicitly requested |
| Implementation | ReadProcessContext, ReadUpstreamArtifacts, WriteManagedArtifacts, MutateDeclaredTarget, RunValidation | ExternalAction unless requested |
| QA/review | ReadProcessContext, ReadUpstreamArtifacts, WriteManagedArtifacts, RunValidation, optional CaptureBrowserProof | MutateDeclaredTarget unless repair step |
| Approval/security | ReadProcessContext, ReadUpstreamArtifacts, WriteManagedArtifacts | MutateDeclaredTarget, LaunchRuntime unless required |
| Recovery | ReadProcessContext, ReadUpstreamArtifacts, WriteManagedArtifacts, RecoverArtifactsOnly | Product mutation unless recovery explicitly permits repair |

## Tool Enforcement

Pass boundary metadata to AgentFramework execution metadata. Workspace tools must deny:

- product/source writes when `MutateDeclaredTarget` is not allowed,
- runtime launch when `LaunchRuntime` is not allowed,
- browser tools when `CaptureBrowserProof` is not allowed,
- external actions when not allowed,
- writes outside declared roots.

The denial should produce a process-readable tool receipt and a finalizer diagnostic, not merely a chat instruction.
