# SB04: 04-agent-tool-loop-context-finalizer-e2e

## Status

- Status: Blocked
- Behavior-changing: True until execution proves otherwise.

## Objective

Prove agent tool-loop, context injection, and required finalizer behavior through runtime tests.

## Covered Inputs

- RQ03

## Prerequisites

- SB03 completed.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB04/manifest.md and bundle://proof/SB04/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Critical foundation: requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB04/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Verify MessageAIContextProvider/context contribution is active.
- Verify tool loop and finalizer capture cannot be bypassed.
- Add or run negative and positive runtime tests around required finalizer sequencing.

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

- bundle://proof/SB04/transcripts/failing-first.txt
- bundle://proof/SB04/transcripts/passing.txt
- bundle://proof/SB04/transcripts/source-assertions.txt
- bundle://proof/SB04/transcripts/anti-stub-audit.txt
- bundle://proof/SB04/transcripts/changed-file-hashes.txt
- bundle://proof/SB04/manifest.md
- bundle://proof/SB04/semantic-invariants.md

## Browser Validation Logging

- N/A: runtime integration proof; browser not directly affected.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB04 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
