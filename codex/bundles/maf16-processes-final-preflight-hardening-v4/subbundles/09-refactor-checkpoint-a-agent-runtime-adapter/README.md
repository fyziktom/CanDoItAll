# SB09: 09-refactor-checkpoint-a-agent-runtime-adapter

## Status

- Status: Blocked
- Behavior-changing: False until execution proves otherwise.

## Objective

Clean up agent runtime adapter code only after runtime proof is stable.

## Covered Inputs

- RQ03

## Prerequisites

- SB04 through SB08 completed or blocked with explicit downstream decision.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs
- repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB09/manifest.md and bundle://proof/SB09/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Supporting phase: targeted source/test proof is sufficient unless implementation changes behavior.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB09/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Remove duplication or brittle adapter logic only where proof shows the safe boundary.
- Do not alter behavior without a failing-first test.
- Run dependent MAF/runtime tests after cleanup.

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

- bundle://proof/SB09/transcripts/failing-first.txt
- bundle://proof/SB09/transcripts/passing.txt
- bundle://proof/SB09/transcripts/source-assertions.txt
- bundle://proof/SB09/transcripts/anti-stub-audit.txt
- bundle://proof/SB09/transcripts/changed-file-hashes.txt
- bundle://proof/SB09/manifest.md
- bundle://proof/SB09/semantic-invariants.md

## Browser Validation Logging

- N/A: adapter cleanup proof.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB09 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
