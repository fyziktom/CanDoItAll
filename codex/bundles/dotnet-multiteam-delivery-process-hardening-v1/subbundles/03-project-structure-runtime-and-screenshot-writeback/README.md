# Project-structure runtime and screenshot writeback

## Status

- `Ready`

## Objective

Add subprocesses that create project-structure runtime command nodes and UI screenshot assets under process-run parent nodes.

## Covered Inputs

- R07, R08

## Prerequisites

- SB02 closure gate passed.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json`
- `repo://Templates/Agents/teams/visual-automation-templates/members/screenshot-review-storage-agent/instructions.md`
- `repo://Templates/Agents/teams/delivery-platform/members/delivery-manager/instructions.md`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- New `dotnet-runtime-command-writeback` process template.
- New `dotnet-ui-screenshot-writeback` process template.
- Parent `software-delivery` steps invoking both subprocesses after QA acceptance.
- Tests asserting `Run command`, `Run app`, `Run tests`, and `Screenshots` process-run targets.

## Dependency Impact

- Release approval and user-led process tests depend on durable writeback. If this phase is weak, the run may finish without the project-structure evidence the architect requested.

## Validation Depth

- Critical foundation

## Implementation Steps

1. Add runtime command writeback process with resolve/write/verify steps.
2. Add UI screenshot writeback process with applicability, capture/review/store, and handoff steps.
3. Wire both subprocesses into `software-delivery` after accepted QA.
4. Update screenshot storage instructions to prefer `Screenshots` under process run node when the process asks for it.
5. Add tests for operation contracts and target text.

## Scope Exceptions

- No live screenshot capture is performed by this implementation.

## Do Not Do

- Do not change project-structure runtime APIs unless template instructions cannot express the required target.
- Do not store screenshots under arbitrary route/delivery nodes when the process-run `Screenshots` target is available.

## Acceptance Checklist

- Runtime command writeback requires external-action permission, not product mutation.
- Screenshot writeback requires external-action permission and capture/runtime proof as needed, not product mutation.
- Both subprocesses are default-importable.
- Backend-only path can complete screenshot subprocess with explicit no-UI applicability evidence.

## Proof Required

- Changed-file hashes in SB03 proof manifest.
- Source assertions for writeback target strings.
- Targeted test transcript.
- Anti-stub audit.

## Browser Validation Logging

- N/A for implementation. The subprocess itself must require browser proof when run against a UI app.

## Progression Gate

- Downstream validation may continue only when tests prove the process-run writeback targets and non-mutating contracts.

## Suggested Agent Prompt

```text
Implement SB03 only. Add runtime-command and UI-screenshot writeback subprocesses and wire them into software-delivery without product mutation rights.
```
