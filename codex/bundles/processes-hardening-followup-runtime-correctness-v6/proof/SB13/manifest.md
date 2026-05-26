# SB13 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs` defines `ProcessRuntimeInvariantDiagnosticKind`, `ProcessRuntimeInvariantDiagnosticViewModel`, and run-health invariant diagnostic count/recommended action fields.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeInvariantAuditor.cs` audits alias conflicts, weak artifact records, blocked recovery state, duplicate lineage identity, and manual transition validation failures through typed diagnostic kinds.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs` exposes `ListRuntimeInvariantDiagnosticsAsync` and includes diagnostics in run details.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` records manual transition validation failures as `RuntimeInvariantViolationRecorded` journal entries with deterministic evidence keys.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs` rolls diagnostics into run health and recommended action.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor` renders invariant diagnostics generically in the operator console.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor` surfaces diagnostic counts and recommended action on selected-run health.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs` proves service diagnostics and journaled manual transition failures.
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs` proves the operator console renders diagnostics and recommended action.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB13 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs | bundle://proof/SB13/manifest.md | bundle://proof/SB13/transcripts/passing.txt | bundle://proof/SB13/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB13/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB13/transcripts/failing-first.txt`

## Passing Proof

Transcripts:
- `bundle://proof/SB13/transcripts/passing-integration.txt`
- `bundle://proof/SB13/transcripts/component-proof.txt`
- `bundle://proof/SB13/transcripts/regression-integration.txt`
- `bundle://proof/SB13/transcripts/regression-components.txt`

Source assertion transcript: `bundle://proof/SB13/transcripts/source-assertions.txt`

Diff check transcript: `bundle://proof/SB13/transcripts/diff-check.txt`

Closure gate transcript: `bundle://proof/SB13/transcripts/closure-gate.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB13/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB13/transcripts/changed-file-hashes.txt`

- `0EDD89EDF082A9CA59EC9DBFF26F3D292E75B422D3B76E8B84494ACD8B140368` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
## Validation

- Failing-first proof captured missing diagnostic service/model compile path in `failing-first.txt`.
- Focused integration SB13 tests passed: 2 passed.
- Focused component SB13 test passed: 1 passed.
- Integration regression filter passed: 19 passed.
- Component regression filter passed: 3 passed.
- Anti-stub audit passed.
- No SQLite runtime/migration dependency introduced.

## Blockers

None.




