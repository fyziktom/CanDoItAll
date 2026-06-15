# Final E2E Source Scenario Validation

## Purpose

This validation plan proves that final E2E coverage can load real and synthetic project-source scenarios through governed APIs while keeping the Process architecture generic.

The required scenario source seed is `evidence/e2e-source-project-structures/final-e2e-scenario-source-packs.json`. Future implementation may migrate that seed to final DTOs, but SB27/SB28 must preserve equivalent scenario source facts and prove loading through public APIs.

## Required Scenario Set

Final closure must run at least four scenarios:

| Scenario | Source type | Main proof |
| --- | --- | --- |
| `TetrisGame` | Captured from running `http://localhost:5032` project structure. | Project-structure source ingestion, process launch, subprocess completion/blocking, missing artifact recovery/escalation, browser proof. |
| `RecipePlannerPwa` | Scenario pack. | Blazor WASM PWA, local storage, import/export, print-friendly UI, no backend. |
| `IssueTriageDashboard` | Scenario pack. | Backend/API/persistence path, role-based views, audit behavior, candidate rights for data mutation. |
| `InvoiceApprovalPortal` | Scenario pack. | Security-sensitive approval workflow, approval authority readiness, artifact export and audit history. |

## API Loading Proof

For every scenario, future implementation must prove:

- scenario source loaded through public APIs,
- no direct database writes,
- no test-only runtime bypass,
- project-structure source data created or imported through governed project-structure APIs,
- process definition/template loaded through Process APIs,
- launch plan created through Process APIs,
- candidate readiness assessed through Process APIs,
- run executed through Process APIs,
- run detail, step detail, assignments, artifacts, escalations, and projections read back through Process APIs.

## Codex Skill Proof

SB27 must update or create the Process API Codex skill. SB28 must use that skill or quote its route workflow in the final execution report.

The skill must cover:

- access checks,
- OpenAPI discovery,
- definition/template import,
- launch plan creation,
- candidate readiness,
- project-structure launch/link,
- run readback,
- artifact lineage,
- manager/escalation operations,
- scenario loading,
- validation and stop conditions.

## Genericity Leak Scan

Run forbidden-term scans over generic Process projects after scenario E2E tests:

```text
TetrisGame|Tetris|falling-piece|score storage|RecipePlannerPwa|recipe|meal plan|shopping list|IssueTriageDashboard|SLA badge|InvoiceApprovalPortal|invoice approval
```

Allowed matches:

- scenario pack files,
- tests named for scenario proof,
- evidence files,
- validation reports,
- screenshots,
- docs that explicitly discuss scenario data.

Forbidden matches:

- generic Process Core,
- Runtime,
- Dispatcher,
- Builder,
- Manager generic control loop,
- Artifact ledger contracts,
- Monitoring event/snapshot contracts,
- Template/Git core contracts,
- UI projection contracts,
- broad software-development or .NET driver contracts.

## Required Negative Tests

Future implementation must include negative checks proving:

- importing `RecipePlannerPwa` does not require any Tetris-specific field,
- importing `IssueTriageDashboard` does not require frontend-only assumptions,
- importing `InvoiceApprovalPortal` does not require score/local-storage/game behavior assumptions,
- generic branch routing does not special-case scenario words,
- candidate readiness findings do not contain scenario-specific hardcoded rules,
- project-structure writeback preserves scenario source data as data, not as runtime code paths.

## SB28 Closure Rule

SB28 cannot close unless the final report contains:

- the four scenario results,
- API commands or test names used to load each scenario,
- Playwright screenshot paths for browser-facing assertions,
- process run ids,
- required artifact status summary,
- escalation/recovery summary where applicable,
- domain leak scan command and result,
- Codex Process API skill proof.
