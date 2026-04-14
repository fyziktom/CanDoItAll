# 12 — Data Backfill Cleanup Refactor Gates And Final Closure

## Status

- `Ready`

## Objective

- Dokončit migrace, backfill a cleanup legacy duplicities.
- Uzavřít story coverage, raw note closure a trojí review.
- Zanechat po implementaci čistý, auditovatelný systém místo dočasně slepené integrace.

## Covered Inputs

- `IN-15`, `IN-16`, `IN-17`, `IN-18`, `RQ-02`, `RQ-23`, `RQ-27`, `RQ-29`, `US-26`, `US-27`, `US-28`

## Prerequisites

- `11-scenario-migration-real-e2e-validation-and-playwright-proof` closed.
- All previous critical subbundles closed.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/AiAgentProfileIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/StaffingAllocationIntegrationTests.cs

## Deliverables

- EF migrations/backfill scripts and final cleanup of legacy duplicate paths.
- Updated execution report with complete commands, screenshots, scenario evidence and raw note closure.
- Story coverage review and unresolved-gap resolution.
- Final QA, development manager and architect closure notes.

## Dependency Impact

- Je to finální closure a housekeeping subbundle. Bez ní by zůstaly v systému přechodné duplicity a nedořešené review concerns.
- Pokud se cleanup odbyde, budoucí maintenance velmi rychle znovu rozdvojí source of truth.

## Validation Depth

- `Final closure`
- Vyžaduje full targeted rerun, story review a triple sign-off.

## Implementation Steps

1. Dokončit EF migrations a backfill podle architektury a předchozích feature gates.
2. Odstranit nebo přepnout na read-only/redirect legacy surfaces a obsolete duplicate writes.
3. Projít workbook user stories proti reálnému UI a execution reportu; doplnit případné chybějící flowy.
4. Spustit bundle validator znovu, buildy, relevantní test suites a souhrnný browser review.
5. Sepsat závěrečné hodnocení z pohledu QA, development managerky a senior C# architektky.

## Scope Exceptions

- Žádná otevřená architecturally significant výjimka nesmí zůstat bez explicitního follow-up plánu.

## Do Not Do

- Nenechávat v systému aktivní legacy write paths jen proto, že „už nejsou v UI“.
- Nepřeskakovat final story coverage review.
- Nepovažovat bundle za hotovou jen proto, že build a testy jsou zelené.

## Acceptance Checklist

- Legacy duplicate paths jsou odstraněné, redirectované nebo jasně gated s plánem odstranění.
- Story coverage a raw note closure jsou explicitně uzavřené.
- Execution report obsahuje skutečné evidence, ne placeholders.
- Triple review neobsahuje blocker.

## Proof Required

- Final targeted build/test command set.
- Updated execution report with all required sections filled.
- Story coverage matrix reviewed against actual UI.
- Documented QA / manager / architect sign-off.

## Browser Validation Logging

- Cross-route sanity pass: `/agents`, `/crm-hr/agents`, `/processes`, `/collaboration`.
- Viewport: `1600x900` and any route-specific narrower pass flagged earlier.
- Actions: smoke through the final integrated navigation and verify no legacy duplicate entry remains.
- Screenshot review: final shell consistency and no orphaned legacy screens.

## Progression Gate

- Bundle is not done until execution report is fully populated and triple review is passed.
- If any review perspective raises a blocker, reopen the responsible subbundle instead of documenting the problem away.

## Suggested Agent Prompt

```text
Implement only subbundle 12.

Finish migrations, backfill and cleanup, then perform the final closure review. Verify story coverage against the real UI, remove legacy duplicate paths, populate the execution report fully, and close from QA, development manager and architect perspectives.
```

