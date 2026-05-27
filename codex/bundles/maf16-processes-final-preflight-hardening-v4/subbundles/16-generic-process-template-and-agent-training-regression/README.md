# SB16: 16-generic-process-template-and-agent-training-regression

## Status

- Status: Completed
- Behavior-changing: False until execution proves otherwise.

## Objective

Protect generic process templates and agent-training processes from runtime-specific regressions.

## Covered Inputs

- RQ11

## Prerequisites

- SB10 through SB15 completed or SB15 blocked with explicit no-go.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateLibraryService.cs
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs
- repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills/blazor-ssr-delivery.md
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB16/manifest.md and bundle://proof/SB16/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Supporting phase: targeted source/test proof is sufficient unless implementation changes behavior.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB16/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Run generic process/template regression tests.
- Verify business and agent-training processes are not special-cased.
- Document any intentionally unsupported template behavior.

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

- bundle://proof/SB16/transcripts/failing-first.txt
- bundle://proof/SB16/transcripts/passing.txt
- bundle://proof/SB16/transcripts/source-assertions.txt
- bundle://proof/SB16/transcripts/anti-stub-audit.txt
- bundle://proof/SB16/transcripts/changed-file-hashes.txt
- bundle://proof/SB16/manifest.md
- bundle://proof/SB16/semantic-invariants.md

## Browser Validation Logging

- N/A unless template UI rendering changes.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB16 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
