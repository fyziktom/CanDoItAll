# SB01 — Baseline Discovery and 1.13 Compatibility Fixtures

## Status

- `Complete`

## Objective

Establish a reproducible 1.13 baseline and capture every cross-version artifact required to distinguish MAF regressions, application defects, and intentional behavior changes before any package edit.

## Success Criteria

- Actual branch head and drift from the pinned snapshot are recorded.
- Full discovery scripts complete with every match classified.
- Direct/transitive package graph and warning baseline exist.
- Current build/test status is recorded without concealing inherited failures.
- Sanitized 1.13 chat-session, approval, handoff, attachment, provider-history, and workflow-checkpoint fixtures exist where applicable.
- Current file-tool inventory and runtime lifecycle evidence exist.
- A rollback state snapshot procedure is documented.
- A1 gate passes.

## Covered Requirements

- R01, R04, R12, R13, R14, R15, R16, R17, R18, R20, R22

## Prerequisites

- clean or intentionally classified working tree;
- access to the exact `agents-loading-refactor` branch;
- ability to run current 1.13 tests and a deterministic fake provider;
- no MAF package edits.

## Exact Source References

- `Directory.Build.props`
- all `*.csproj`, `*.props`, and `*.targets`
- `src/MAF/**`
- `src/Integration/CanDoItAll.FileTools.Integration/**`
- all test projects from `CanDoItAll.slnx`
- current persistence/session models and stores
- current A2A endpoint mapping
- existing agent-preload architecture bundle

## Deliverables

- `proof/SB01/repository-head.txt`
- `proof/SB01/discovery/`
- `proof/SB01/package-graph/`
- `proof/SB01/build-and-test-baseline/`
- `proof/SB01/fixtures/maf-1.13/`
- `proof/SB01/file-tool-inventory.json`
- `proof/SB01/runtime-lifecycle.md`
- `proof/SB01/warning-baseline.txt`
- `proof/SB01/a1-decision.md`
- updated repository evidence index and execution report

## Implementation Steps

1. Verify repository, branch, head SHA, status, SDK, and OS.
2. Run `machine/grep-discovery.ps1` or `.sh`.
3. Classify every result by production/test/sample/dead/documentation and direct/transitive usage.
4. Capture `dotnet restore` and package graphs for all direct MAF projects.
5. Build the MAF adapter, workflow adapter, hosting, relevant test projects, and solution.
6. Run relevant existing tests and classify inherited failures.
7. Locate provider factories and record effective chat-client middleware construction.
8. Locate response snapshot/streaming runner and document every update transform.
9. Locate pending approval models/store/API/UI and document authority, integrity, and consumption.
10. Locate session scrubber and checkpoint bridge.
11. Capture sanitized 1.13 fixtures listed in `plan/03-test-plan.md`.
12. Capture direct inner, depth-guard, and full-runtime handoff outputs.
13. Capture file-tool inventories for representative agent definitions.
14. Capture A2A card/message baseline.
15. Record state-store backup/rollback method.
16. Hash fixtures and complete A1 review.

## Do Not Do

- do not change NuGet versions;
- do not refactor production behavior;
- do not regenerate request IDs;
- do not skip fixtures because current tests pass;
- do not commit credentials, raw provider secrets, or sensitive attachments;
- do not “fix” inherited failures without first recording them.

## Acceptance Checklist

- [x] branch drift classified
- [x] discovery report complete
- [x] provider pipeline proven
- [x] package graph captured
- [x] warning baseline captured
- [x] 1.13 fixtures captured and sanitized
- [x] handoff path comparison captured
- [x] file-tool inventory captured
- [x] A2A baseline captured
- [x] rollback snapshot documented
- [x] A1 GO

## Proof Tier

- `Governed`
- Critical foundation for every later subbundle.

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

No package file may change until `proof/SB01/a1-decision.md` records `GO`.

## Reopen Triggers

- branch changes in MAF/runtime/session/tool/hosting code;
- a new provider factory is discovered;
- fixture cannot be reproduced;
- a later migration failure lacks a matching 1.13 artifact.

## Suggested Agent Prompt

```text
Implement SB01 only. Do not edit package versions or production behavior. Run the discovery scripts, classify every MAF integration, capture sanitized 1.13 cross-version fixtures and current build/test/package/warning evidence, update the execution report, and stop unless A1 can honestly pass.
```
