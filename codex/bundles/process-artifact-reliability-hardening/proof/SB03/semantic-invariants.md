# SB03 Semantic Invariants

## Status

Completed.

## Invariants

- Invariant ID: `SB03-INV-001`
- Source raw note: N002, N006, and N007 require manager recovery to be evidence-bound and explicitly authorized.
- Expected behavior: Manager recovery may run only through assigned manager authority or explicit artifact recovery capability, and recovery output is revalidated before completion.
- Disallowed shallow implementation: Selecting any agent whose name contains `lead`, `manager`, or `orchestrator` and treating manager prose as valid artifact proof.
- Failing-first test: Pre-change source assertion in `bundle://proof/SB03/transcripts/failing-first.txt` shows `lead` was accepted by fallback token logic.
- Passing test: `ResolveManagerArtifactRecoveryAgent_rejects_single_generic_lead_fallback_agent` and `ResolveManagerArtifactRecoveryAgent_allows_single_explicit_artifact_recovery_manager` in `bundle://proof/SB03/transcripts/passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `ContainsExplicitArtifactRecoveryCapability`, `single-explicit-recovery-option`, `single-explicit-recovery-agent`, and `ProcessStepCompletionExecutorKind.ManagerArtifactRecovery` are asserted by `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Red-team negative case: A single generic `Delivery Lead` agent is not selected for artifact recovery.
- Downstream dependency check: SB05 stranded recovery receives finalizer validation instead of relying on mutable candidate satisfaction state.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| ManagerArtifactRecoveryAgent | `ResolveManagerArtifactRecoveryAgent` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Manager recovery directive and finalizer integration in process dispatch code | Exists only after explicit capability/assignment resolution; source proof is `bundle://proof/SB03/transcripts/source-assertions.txt` | Generic lead rejection test proves weak fallback is blocked |
| ProcessStepCompletionExecutorKind.ManagerArtifactRecovery | Stranded artifact recovery branch in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Finalizer validation and transition application | Recovery completion runs through validation before transition; verified by `bundle://proof/SB03/transcripts/source-assertions.txt` | Pre-change source proof shows weaker manager fallback |

## Red-Team Negative Cases

- A generic `Delivery Lead` cannot satisfy manager artifact recovery eligibility.
- Explicit `process-artifact-recovery-manager` capability is required for single-option fallback.
