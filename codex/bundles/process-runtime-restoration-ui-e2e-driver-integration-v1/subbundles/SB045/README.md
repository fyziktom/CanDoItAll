# SB045 — Gate O: generic process core boundary remains clean.

## Status

Prepared.

## Objective

Implement the `Gate O: generic process core boundary remains clean.` slice as part of `P15: Process Core genericity audit`.

## Covered Inputs

- Raw user request in `bundle://inputs/raw-request.md`
- Requirements in `bundle://requirements/01-normalized-requirements.md`
- Phase plan in `bundle://plan/01-phase-plan.md`

## Prerequisites

Previous subbundles in the phase must be completed. If this is a critical gate, all prior subbundles in the phase must have source-backed proof.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.*`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `repo://src/CanDoItAll.Web`
- `repo://src/CanDoItAll.Composition`

## Deliverables

- Source changes or proof artifacts required by this slice.
- Tests or scans proving behavior.
- Updated execution report row for `SB045`.

## Dependency Impact

Downstream phases depend on this subbundle preserving process runtime behavior and Core/driver boundaries. If this subbundle changes API shape, update the relevant source-backed guards.

## Validation Depth

Critical foundation: requires build/test/source-scan proof, semantic adequacy proof, changed-file hash manifest, and red-team proof.

## Implementation Steps

1. Re-read the exact current source files before editing.
2. Make the smallest complete change for this slice.
3. Add or update tests before relying on manual proof.
4. Run focused validation.
5. Record command transcript paths and source assertions.

## Scope Exceptions

Do not implement generic runtime driver host, registry, selector, DI registration, manager command, scheduler/workflow driver hook, shell execution, Office/Graph calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, or retry scheduling.

## Do Not Do

- Do not add tests that depend on `codex/bundles/<bundle-name>`.
- Do not weaken architecture guards by deleting them without replacement.
- Do not use small/medium/mobile browser proof.
- Do not make driver verification mutate process state.

## Acceptance Checklist

- [ ] Source changes are source-backed and minimal.
- [ ] Tests/scans prove the intended behavior.
- [ ] No transient bundle path dependency added.
- [ ] No forbidden runtime/driver/Core/UI drift.
- [ ] Execution report row updated.

## Proof Required

Create `proof/SB045/manifest.md` and `proof/SB045/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note closure, changed-file hashes, command transcripts, and production behavior artifact matrix if new signals are introduced.

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes browser-visible UI. If UI files change unexpectedly, fail and re-scope.

## Progression Gate

Do not proceed to dependent subbundles if this proof fails or if any hard-stop condition in `analysis/02-assumptions-and-risks.md` is triggered.

## Suggested Agent Prompt

Implement `SB045 — Gate O: generic process core boundary remains clean.`. Preserve all hard constraints. Produce source-backed proof and update the execution report.
