# SB06 Evidence And Merge Readiness

## Status

Ready after `SB05`.

## Objective

Record final evidence, close raw notes, and make the package update reviewable without hiding validation gaps.

## Covered Inputs

- `bundle://inputs/original-prep/docs/04-codex-execution-plan.md`
- `bundle://inputs/original-prep/docs/05-validation-and-regression-plan.md`
- `bundle://inputs/original-prep/checklists/pre-merge-checklist.md`
- `bundle://reviews/01-execution-report.md`

## Prerequisites

- `SB05` focused validation is complete or blocked with exact reason.
- All critical subbundle manifests exist.
- Package decisions and test results are recorded.

## Exact Source References

- `repo://docs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- `docs/maf-1.13-update-evidence.md`.
- Final package before/after table.
- Restore/build/test result summary.
- A2A/Mem0 decision summary.
- Source scan summary.
- Raw note closure table.
- Residual risk or blocker list.

## Dependency Impact

- Final merge/readiness decision depends on this subbundle.

## Validation Depth

- Evidence consistency review.
- Final source scans.
- Final raw-note closure.
- Completed-stage validator after implementation.

## Implementation Steps

1. Create or update `docs/maf-1.13-update-evidence.md`.
2. Summarize exact commands and outcomes.
3. Record package before/after and preview package decisions.
4. Run final source scans and `git diff --check`.
5. Close each raw note as `Solved`, `Partially solved`, or `Not solved`.
6. Run final bundle validation after implementation.

## Scope Exceptions

- Do not claim optional tests passed when they were skipped.
- Do not close unresolved package incompatibilities as residual prose; represent them as blockers or follow-up subbundles.

## Do Not Do

- Do not alter production code in this closure phase unless earlier subbundles are reopened.
- Do not squash or hide validation failures.
- Do not broaden documentation into new feature adoption.

## Acceptance Checklist

- Evidence doc exists and matches execution report.
- Final source scans pass or historical matches are explained.
- Raw notes are closed with statuses.
- Residual risks are concrete.
- Completed-stage validator plan is recorded.

## Proof Required

- Final scan transcripts.
- Evidence doc source assertion.
- Raw-note closure proof.
- `git diff --check` transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- Include any browser analytics from `SB05`; N/A if no browser proof was required or feasible.

## Progression Gate

- Bundle closes only when final evidence, raw-note closure, and validators agree.

## C# Architecture Impact

- Confirms architecture gate evidence is reflected in final merge evidence.

## Boundary Ownership

- Final docs must state that process direct tools and API expansion were not introduced.

## Dependency Direction

- Final evidence must mention project-reference changes if any; otherwise state none.

## Pattern Decision

- Final evidence must name any introduced helper/adapter pattern and link to proof.

## Testability Contract

- Final evidence must list tests and their behavior intent.

## Partial Class Policy

- Final evidence must state whether any partial files were added; expected answer is none.

## Architecture Proof Required

- Final evidence links to architecture gate result and source scans.

## Suggested Agent Prompt

Execute `SB06` only. Produce the final evidence note and close the execution report. Run final scans, close raw notes honestly, and do not make source changes unless you reopen earlier subbundles.
