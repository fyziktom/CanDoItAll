# SB17: 17-refactor-checkpoint-b-process-runtime-cleanup

## Status

- Status: Blocked
- Behavior-changing: False until execution proves otherwise.

## Objective

Clean process runtime code after parity, recovery, smoke, and generic regressions are stable.

## Covered Inputs

- RQ06, RQ07, RQ08, RQ09, RQ11

## Prerequisites

- SB10 through SB16 completed or explicitly blocked.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB17/manifest.md and bundle://proof/SB17/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Supporting phase: targeted source/test proof is sufficient unless implementation changes behavior.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB17/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Remove duplicated mapping or helper code introduced during parity work.
- Keep behavior unchanged unless covered by tests.
- Run dependent process runtime tests after cleanup.

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

- bundle://proof/SB17/transcripts/failing-first.txt
- bundle://proof/SB17/transcripts/passing.txt
- bundle://proof/SB17/transcripts/source-assertions.txt
- bundle://proof/SB17/transcripts/anti-stub-audit.txt
- bundle://proof/SB17/transcripts/changed-file-hashes.txt
- bundle://proof/SB17/manifest.md
- bundle://proof/SB17/semantic-invariants.md

## Browser Validation Logging

- N/A unless UI cleanup occurs.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB17 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
