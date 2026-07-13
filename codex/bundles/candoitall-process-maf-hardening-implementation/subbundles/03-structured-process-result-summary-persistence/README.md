# SB03 - Structured Process Result Summary Persistence

## Status

- `Completed`
- Critical foundation: yes

## Objective

Ensure process-bound AgentFramework executions persist a compact structured process result summary that projections and manager rework can parse reliably.

## Covered Inputs

- F01, F10.
- GPTPro B05.
- R02, R15.

## Prerequisites

- SB02 packet categories and exact observation path complete.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs`

## Deliverables

- Structured result summary record for process-bound runs.
- Persist final validated/repaired `ProcessStepOutcomeResult` projection into `ExecutionRunRecord.ResultSummary` or a dedicated process summary field if existing model supports it.
- Failure summary JSON for runs that fail before structured output.
- Parser/update path in operator diagnostics that consumes the structured summary.

## Dependency Impact

- SB06 and SB09 require reliable result summaries for artifact and final proof. SB02 benefits from richer summaries but must still handle absence.

## Validation Depth

- Critical foundation.
- Requires failing-first and passing persistence/parser tests.

## Implementation Steps

1. Define compact summary fields: status, reason, branch outcome key/title, evidence refs, next actions, failed tools, primary managed artifact ref, diagnostic code.
2. Persist validated/repaired structured output, not raw model text, for process-bound runs.
3. Preserve raw model text separately if needed.
4. Persist compact blocked/failure JSON when execution fails before structured output exists.
5. Update diagnostics parser tests.
6. Ensure non-process AgentFramework runs remain compatible.

## Scope Exceptions

- Do not change finalizer contract semantics beyond summary persistence.
- Do not rely on ResultSummary for all proof; runtime receipt fallback from SB02 remains required.

## Do Not Do

- Do not store sensitive values in summaries.
- Do not make diagnostics depend on raw prose response text.
- Do not hide structured-output validation failures.

## Acceptance Checklist

- [ ] Successful process run stores parseable summary JSON.
- [ ] Repaired/normalized structured output is what gets stored.
- [ ] Failure before structured output stores compact blocked/failure JSON.
- [ ] Operator diagnostics parse the stored summary.
- [ ] Existing non-process execution summaries remain compatible.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- Failing-first transcript for missing/unparseable process summary.
- Passing transcripts for completed and failure summary tests.
- Source assertions for persistence path.
- Changed-file hashes.
- Anti-stub audit.
- Production Behavior Artifact Matrix for structured process result summary.

## Browser Validation Logging

- `N/A`.

## Progression Gate

- SB06 and SB09 may rely on process result summaries only after persistence and parser tests pass.

## C# Architecture Impact

Touches AgentFramework execution persistence and process diagnostics contract.

## Boundary Ownership

AgentFramework owns persisted execution record fields; process adapter/application owns process-specific projection content.

## Dependency Direction

Do not make AgentFramework core depend on process template implementation.

## Pattern Decision

Use Adapter/Projection record for process summary. Keep it compact.

## Testability Contract

Tests must use fake execution response/output and inspect persisted record without live provider.

## Partial Class Policy

No new large execution-service partial. Add focused helper if behavior grows.

## Architecture Proof Required

- Source assertion for focused summary projection.
- Tests proving process and non-process paths.

## Suggested Agent Prompt

```text
Execute SB03 only. Persist parseable structured process summaries and failure summaries. Keep runtime receipt fallback intact. Add tests and proof artifacts before moving to artifact or bridge work.
```
