# B02 — Runtime-owned subprocess bridge

## Goal

Make `StepKind=Subprocess` deterministic. Runtime must own subprocess launch, waiting, child terminal inspection and parent artifact synthesis.

## Current problem

The adapter already has runtime-side subprocess coordination, while templates also tell the agent to call `project_structure_process_subprocess_launch`. This dual ownership is fragile.

## Required design decision

For controlled process templates with `StepKind=Subprocess` and `SubprocessProcessKey`, use this model:

1. Runtime launches or resolves the child process.
2. If child is active, parent step is deferred/waiting, not blocked.
3. If child completed with accepted output, runtime writes parent evidence and returns success.
4. If child completed with no-go output, runtime returns `NeedsManager` with concrete child evidence.
5. Only use agent-owned `project_structure_process_subprocess_launch` as a legacy/manual fallback.

## New focused service

Create a focused service, not another large partial method:

```csharp
public interface IParentSubprocessArtifactBridge
{
    ValueTask<ParentSubprocessBridgeResult> TryBridgeAsync(
        ParentSubprocessBridgeRequest request,
        CancellationToken cancellationToken = default);
}
```

Suggested result cases:

- `ChildActive`
- `AcceptedChildOutputBridged`
- `NoGoChildOutputFound`
- `ChildCompletedWithoutAcceptedOutput`
- `NoMatchingChildRun`
- `BridgeInfrastructureFailure`

## Accepted child output validation

For `prepare-solution-skeleton`, bridge must accept:

- child step `setup-handoff` + artifact expectation `setup-handoff-packet`,
- child step `setup-handoff-after-repair` + artifact expectation `setup-handoff-packet-after-repair`.

Bridge must reject/no-go:

- child step `setup-repair-escalation` + artifact expectation `setup-repair-escalation-packet`.

## Parent managed artifact synthesis

When accepted child output is found, write a parent managed artifact for the parent step, for example:

```text
artifacts/process-runs/<parent-run-id>/steps/prepare-solution-skeleton.md
```

It must contain:

- parent run id,
- parent step id,
- parent step key,
- parent artifact expectation key/title,
- child run id,
- child accepted step key,
- child artifact expectation key/title,
- exact child managed ref,
- child validation/build proof refs if available,
- product root and solution/project/test paths if available,
- content hash.

This file should be marked as runtime-synthesized bridge evidence, not as a fake agent-written artifact.

## Adapter changes

In `AgentFrameworkProcessExecutionAdapter`:

- before calling `workspaceService.ExecuteRunAsync`, check whether this is a runtime-owned subprocess step;
- call the bridge/coordinator;
- return deferred/result directly when possible;
- do not invoke the agent just to launch a controlled child subprocess.

## Tests

Add tests equivalent to:

- `PrepareSolutionSkeleton_WhenNoChildExists_LaunchesChildAndDefersParent`
- `PrepareSolutionSkeleton_WhenChildRunning_DefersParent`
- `PrepareSolutionSkeleton_WhenChildCompletedWithSetupHandoff_WritesParentEvidenceAndCompletes`
- `PrepareSolutionSkeleton_WhenChildCompletedAfterRepair_WritesParentEvidenceAndCompletes`
- `PrepareSolutionSkeleton_WhenChildRepairEscalation_PropagatesConcreteNoGoBlocker`
- `PrepareSolutionSkeleton_WhenChildCompletedWithoutAcceptedOutput_BlocksWithChildOutputDiagnostic`

## Acceptance criteria

- Parent subprocess step cannot repeatedly call an agent only to rediscover the same child state.
- Accepted child output deterministically creates the parent produced slot.
- No-go child output is visible as a concrete blocker.
