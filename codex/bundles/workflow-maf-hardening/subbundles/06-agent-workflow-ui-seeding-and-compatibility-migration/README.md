# 06-agent-workflow-ui-seeding-and-compatibility-migration

## Status

- `Completed`

## Objective

Update Agents/Workflows UI, managed seeding, and compatibility migration so users can see and control the hardened workflow runtime without data loss.

## Success Criteria

- UI surfaces validation diagnostics before execution.
- UI shows executor availability, plugin capability warnings, approval requirements, runtime backend, and preview-vs-production state.
- Managed seed refresh remains safe and idempotent.
- Existing user-managed definitions are preserved.
- Any schema migration is versioned, idempotent, and covered by tests.

## Covered Inputs

- R03, R08, R09, R11, R13, R14, R15

## Prerequisites

- SB02, SB03, SB04, and SB05 contracts stable.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework`
- `repo://src/CanDoItAll.AgentFramework.Components`
- `repo://src/CanDoItAll.Components`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://src/CanDoItAll.AgentFramework.Persistence`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://tests/CanDoItAll.Tests.Playwright`

## Deliverables

- UI validation and runtime status improvements.
- Seed/migration tests.
- Browser proof for key workflow screens if UI changed.
- Documentation update for workflow authoring and plugin executor constraints.

## Dependency Impact

- SB07 final review depends on UI, seed, and migration behavior matching the contracts from SB02 through SB05.
- User data safety depends on this phase preserving non-managed workflow definitions and managed seed markers.
- Any schema migration or seed refresh defect must be treated as a blocker before final closure.

## Validation Depth

- Critical data-safety phase with semantic proof required under `proof/SB06/manifest.md` and `proof/SB06/semantic-invariants.md`.
- Requires seed/migration tests proving user-managed definitions survive refresh, plus browser proof when Razor/UI surfaces change.
- Requires source assertions showing runtime logic is consumed from services rather than duplicated in Razor components.

## Implementation Steps

1. Update UI view models to consume validator/compiler diagnostics rather than duplicating validation logic.
2. Display executor registry status and missing plugin warnings.
3. Show runtime policy: in-process preview vs durable production.
4. Show approval-required markers for tool/plugin nodes.
5. Keep managed seed marker/version behavior intact.
6. Add migration tests for old definitions if schemas changed.
7. Run browser/Playwright proof when UI changes.
8. Update proof and execution report.

## Scope Exceptions

- Do not redesign the whole Agents UI unless needed to expose runtime hardening state.

## Do Not Do

- Do not put business/runtime logic into Razor pages.
- Do not overwrite non-managed workflow definitions.
- Do not hide missing plugin executors until execution time if they can be detected earlier.

## Acceptance Checklist

- UI can explain why a workflow cannot run.
- UI can distinguish preview and durable production mode.
- User-managed definitions survive seed refresh tests.
- Browser proof captures relevant screens if changed.

## Proof Required

- Unit/integration migration tests.
- Playwright/browser screenshots or a documented no-UI-change rationale.
- Execution report update.

## Browser Validation Logging

- Required if UI changes are made: log workflow route, large-screen viewport, narrow-width viewport when layout changes, Playwright actions/assertions, screenshots, overlay/open-state checks if applicable, and pass/fail result.
- If no UI files change, record a no-UI-change rationale in the browser analytics row.

## Progression Gate

- SB07 may start after UI/seeding/migration surfaces are consistent with runtime contracts and SB06 closure proof cites `proof/SB06/manifest.md` plus `proof/SB06/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB06 only. Update UI, seed, and migration surfaces to reflect hardened workflow runtime contracts without duplicating runtime logic or overwriting user definitions.
```
