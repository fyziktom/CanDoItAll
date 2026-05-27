# SB03: 03-maf16-runtime-symbol-contract-tests

## Status

- Status: Completed
- Behavior-changing: False until execution proves otherwise.

## Objective

Keep runtime symbol contract tests aligned with actual loaded MAF/A2A assemblies.

## Covered Inputs

- RQ02

## Prerequisites

- SB02 completed with truth table decisions.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs
- repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
- repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB03/manifest.md and bundle://proof/SB03/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Supporting phase: targeted source/test proof is sufficient unless implementation changes behavior.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB03/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Run/update reflection tests for required and intentionally unavailable MAF/A2A symbols.
- Record direct API, fallback, and deferred feature assertions.
- Keep failures actionable when package contents change.

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

- bundle://proof/SB03/transcripts/failing-first.txt
- bundle://proof/SB03/transcripts/passing.txt
- bundle://proof/SB03/transcripts/source-assertions.txt
- bundle://proof/SB03/transcripts/anti-stub-audit.txt
- bundle://proof/SB03/transcripts/changed-file-hashes.txt
- bundle://proof/SB03/manifest.md
- bundle://proof/SB03/semantic-invariants.md

## Browser Validation Logging

- N/A: reflection/unit test proof only.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB03 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
