# 06-agent-workflow-ui-seeding-and-compatibility-migration

## Status

- `Prepared`

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

- `src/CanDoItAll.Modules.AgentFramework/`
- Workflow pages/components found by SB01.
- `WorkflowExampleCatalogSeedService`
- `Templates/Workflows/manifest.yaml`
- Persistence/migration surfaces found by SB01.

## Deliverables

- UI validation and runtime status improvements.
- Seed/migration tests.
- Browser proof for key workflow screens if UI changed.
- Documentation update for workflow authoring and plugin executor constraints.

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

## Progression Gate

SB07 may start after UI/seeding/migration surfaces are consistent with runtime contracts.

## Suggested Agent Prompt

```text
Implement SB06 only. Update UI, seed, and migration surfaces to reflect hardened workflow runtime contracts without duplicating runtime logic or overwriting user definitions.
```
