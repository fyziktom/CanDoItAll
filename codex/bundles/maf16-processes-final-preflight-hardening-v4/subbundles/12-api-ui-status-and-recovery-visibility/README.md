# SB12: 12-api-ui-status-and-recovery-visibility

## Status

- Status: Completed
- Behavior-changing: True until execution proves otherwise.

## Objective

Ensure operator/API/UI surfaces show invalid recorded artifact states with actionable diagnostics.

## Covered Inputs

- RQ07

## Prerequisites

- SB10 and SB11 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsArtifactsSection.razor
- repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessCanvasSelectionPanel.razor
- repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB12/manifest.md and bundle://proof/SB12/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Critical foundation: requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB12/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Expose status, diagnostic, attempted path, artifact record id, suggested action, and failure ownership where available.
- Map danger/warning tones for invalid artifact statuses.
- Add component/API tests or browser proof for affected UI surfaces.

## Scope Exceptions

- None planned. If execution discovers an unsupported path, record it here and in the execution report before closure.

## Do Not Do

- Do not replace missing proof with prose.
- Do not silently narrow all, every, must, or equivalent requirements.
- Do not run a full live process test before SB15 explicitly allows it.

## Acceptance Checklist

- Entry gate prerequisites are satisfied or explicitly blocked.
- Required implementation/proof steps are complete.
- Failing-first or adversarial proof is captured for behavior changes.
- Passing proof, source assertions, anti-stub audit, and changed-file hashes are captured.
- Execution report and raw-note closure rows are updated.

## Proof Required

- bundle://proof/SB12/transcripts/failing-first.txt
- bundle://proof/SB12/transcripts/passing.txt
- bundle://proof/SB12/transcripts/source-assertions.txt
- bundle://proof/SB12/transcripts/anti-stub-audit.txt
- bundle://proof/SB12/transcripts/changed-file-hashes.txt
- bundle://proof/SB12/manifest.md
- bundle://proof/SB12/semantic-invariants.md

## Browser Validation Logging

- Required if visible UI rendering changes; record route, viewport, actions, screenshots, and result in the execution report.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB12 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
