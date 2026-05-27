# SB11: 11-readmodel-finalizer-parity-all-statuses

## Status

- Status: Completed
- Behavior-changing: True until execution proves otherwise.

## Objective

Make the read model consume all artifact validation diagnostics and never mark rejected artifacts as satisfied.

## Covered Inputs

- RQ06

## Prerequisites

- SB10 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB11/manifest.md and bundle://proof/SB11/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Critical foundation: requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB11/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Generalize content-unavailable diagnostic resolution to all finalizer validation statuses.
- Map StaleOrWrongRun, ContentHashMismatch, WrongProducerMode, InvalidFormat, PlaceholderOnly, and InsufficientEvidence to non-satisfied read-model states.
- Add tests for every status, including a stale/wrong artifact negative case.

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

- bundle://proof/SB11/transcripts/failing-first.txt
- bundle://proof/SB11/transcripts/passing.txt
- bundle://proof/SB11/transcripts/source-assertions.txt
- bundle://proof/SB11/transcripts/anti-stub-audit.txt
- bundle://proof/SB11/transcripts/changed-file-hashes.txt
- bundle://proof/SB11/manifest.md
- bundle://proof/SB11/semantic-invariants.md

## Browser Validation Logging

- N/A for read-model service proof; UI proof is owned by SB12.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB11 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
