# Target Boundary

## Current transitional shape

```text
ProcessRunAutomationDispatchService.ArtifactProjection.cs
  -> creates ArtifactProjectionCoordinatorContext
  -> new ProcessExecutionArtifactProjectionCoordinator(this)
  -> new ProcessMockArtifactProjectionCoordinator(this)
  -> ...
```

The current nested shape is a valid transition but not a stable module boundary.

## Target module-local shape

```text
ProcessRunAutomationDispatchService.ArtifactProjection.cs
  -> ProcessArtifactProjectionOrchestrator.ProjectAsync(context)

ProcessArtifactProjectionOrchestrator
  -> IProcessArtifactProjectionSourceCoordinator[] ordered coordinators
     1. ProcessExecutionArtifactProjectionCoordinator
     2. ProcessMockArtifactProjectionCoordinator
     3. ProcessWorkspaceWrittenArtifactProjectionCoordinator
     4. ProcessExistingManagedArtifactProjectionCoordinator
     5. ProcessResponseTextArtifactProjectionCoordinator
     6. ProcessProviderNativeBrowserArtifactProjectionCoordinator
     7. ProcessCompletedDecisionArtifactCoordinator

ProcessArtifactProjectionContext
  -> candidate, detail, response text, workspace root/scope, completion status, cancellation token, lineage

ProcessArtifactProjectionHost
  -> explicit internal operations needed by coordinators
```

## Allowed dependency style

Use internal module-local services and delegates. Avoid broad service references.

Allowed examples:

```csharp
internal sealed record ProcessArtifactProjectionHost(
    Func<ProcessStepDispatchClaim, CancellationToken, Task> EnsureClaimHeldAsync,
    Func<string, string, ArtifactPathResolution> ResolveArtifactPath,
    Func<ProcessAutomationExecutionArtifact, string, byte[], string?> TryDecodeTextArtifactContent,
    Func<ProcessAutomationExecutionArtifact, ProcessArtifactKind> ResolveArtifactKind,
    Func<ProcessArtifactProjectionWriteRequest, CancellationToken, Task<Result<ProcessArtifactProjectionWriteResult>>> WriteAsync,
    Func<ProcessArtifactProjectionRecordOnlyRequest, CancellationToken, Task<Result<ProcessArtifactProjectionRecordOnlyResult>>> RecordOnlyAsync);
```

The exact shape may differ, but every dependency must be named and tested.

## Forbidden dependency style

```csharp
new ProcessExecutionArtifactProjectionCoordinator(this)
```

This hides a large dependency surface and makes later Core/driver extraction harder.
