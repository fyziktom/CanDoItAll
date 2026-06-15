# Process APIs, Codex Skill, And E2E Source Scenarios

## Design Intent

The rewritten Process module must be operable by Codex through typed APIs. Future implementation agents must not patch database rows, copy JSON into internal stores, or use UI-only workflows to load final E2E scenarios.

The module must expose a stable HTTP API and a complementary Codex skill that document how to create, import, launch, inspect, recover, and validate processes. Project-structure source data may be loaded through project-structure APIs, but process definitions, templates, launch plans, runs, assignments, artifacts, manager directives, and scenario-linked process behavior must be available through Process APIs.

## API Boundary Requirements

The new Process module must expose typed HTTP APIs for:

| API family | Required capabilities |
| --- | --- |
| Definition authoring | List, get, save, publish, delete/archive, export, import, lint, validate, and version definitions. |
| Template catalog | List templates, get template detail, get canonical JSON envelope, import template, list baseline scenarios, list live-run profiles, and report migration/version status. |
| Launch planning | Create launch plan, read launch plan, run HR candidate discovery, run deterministic readiness assessment, select candidates, submit approval, decide approval, provision missing items, reassess readiness, and execute governed launch. |
| Runtime runs | Start run, stop/cancel run, list runs, read run detail with include flags, read steps, read one step, transition a step, rerun/recover a step, and read health/invariant diagnostics. |
| Assignments | Resolve run/step assignments, read assignment detail, record assignment changes, and expose candidate readiness hashes copied from launch. |
| Artifacts | Record artifacts, read run artifacts, read step artifacts, read artifact detail, preserve lineage, preserve storage refs, and report satisfaction status. |
| Manager and operator control | Create manager directive, create direct message, list/create/assign/resolve/reopen/rework escalations, and record operator approval decisions. |
| Project-scoped process integration | Link a process definition to a project-structure node, launch from a project-structure node, write back run projections, and read project-scoped process projections. |
| E2E scenario loading | Load scenario source through public definition/template/project-structure/launch APIs or through a high-level scenario import API that internally uses the same public command handlers and records an audit trail. |
| Analytics and monitoring | Query live/history projections, run metrics, provider usage, projection freshness, and time-window filtered activity. |

No E2E scenario API may bypass normal validation, builder plan compilation, launch gates, authorization, candidate readiness checks, artifact lineage, event emission, or projection writeback.

## Codex Skill Requirement

Future implementation must include or update a Codex skill equivalent to `candoitall-api-processes` for the rewritten module.

The skill must document:

- access and authorization rules,
- OpenAPI discovery location,
- route table,
- enum/string ID guidance,
- definition/template import and export examples,
- launch plan and candidate readiness examples,
- project-structure launch examples,
- run detail readback workflow,
- artifact lineage rules,
- manager directive and escalation workflows,
- final E2E scenario loading workflow,
- direct tool/API parity matrix,
- validation commands and required readbacks.

The skill must warn Codex agents not to:

- patch database rows,
- create process JSON by hand in internal stores,
- bypass launch readiness,
- bypass builder plan compilation,
- use seeded transitions or seeded artifacts as live-run proof,
- introduce scenario-specific logic into generic Process code.

## E2E Scenario Pack Shape

Future final E2E source packs should be JSON documents under version control. JSON remains canonical.

The v3 bundle includes an architecture-seed scenario source pack at `evidence/e2e-source-project-structures/final-e2e-scenario-source-packs.json`. Future implementation may evolve the exact DTO names and route shape, but it must preserve the represented source facts, scenario diversity, and genericity guards.

Recommended envelope:

```text
ProcessE2EScenarioPack
  SchemaVersion
  ScenarioKey
  DisplayName
  Purpose
  DomainTags[]
  ProjectStructureSource
    Project
    Nodes[]
    Links[]
    Assets[]
  ProcessSource
    TemplateKey
    DefinitionImportRef
    LaunchProfile
    ExpectedRoles[]
    ExpectedCandidateReadinessCases[]
  RuntimeExpectations
    ExpectedRunState
    ExpectedStepStates[]
    ExpectedArtifacts[]
    ExpectedEscalations[]
    ExpectedProjectionWritebacks[]
  BrowserProof
    Routes[]
    Viewports[]
    Assertions[]
  GenericityGuards
    ForbiddenGenericTerms[]
    AllowedScenarioFolders[]
```

The scenario pack may contain app-specific text, but the loader and runtime must treat it as data. Scenario text never becomes a generic enum, branch type, driver model concept, or hardcoded runtime rule.

## Scenario Loading Workflow

Future Codex agents must load final E2E source scenarios through APIs in this order:

1. Check `/api/access/status`.
2. Read OpenAPI from `/swagger/v1/swagger.json`.
3. Create or update the project through project APIs.
4. Create project-structure nodes/assets/links through project-structure APIs or a governed scenario import endpoint that uses the same command handlers.
5. Import or resolve the process definition/template through Process APIs.
6. Link the definition to the project-structure target node through governed Process/project-structure integration APIs.
7. Create a launch plan through Process APIs.
8. Run candidate discovery and deterministic candidate readiness assessment.
9. Select candidates only after readiness findings are visible.
10. Submit approval/provisioning/reassessment where required.
11. Execute the launch through Process APIs.
12. Read run detail, step detail, artifacts, assignments, escalations, and projections through Process APIs.
13. Capture Playwright proof for browser-facing expectations.
14. Run genericity leak scans before marking the scenario passed.

## Final E2E Scenario Set

Final E2E must include at least:

- `TetrisGame`, sourced from the running instance and recorded in `analysis/09-final-e2e-project-structure-source-scenarios.md`.
- `RecipePlannerPwa`, an offline-first Blazor WASM PWA with local storage and import/export.
- `IssueTriageDashboard`, a Blazor/web app with backend API, persistence, roles, and audit behavior.
- `InvoiceApprovalPortal`, a security-sensitive web app with approval authority and exportable audit history.

The draft source data for these scenarios is in `evidence/e2e-source-project-structures/final-e2e-scenario-source-packs.json`.

The scenarios should reuse the same generic Process machinery:

- project source ingestion,
- definition/template import,
- launch planning,
- candidate readiness,
- builder plan compilation,
- runtime/subprocess orchestration,
- artifact lifecycle,
- manager escalation,
- project-structure writeback,
- live/history projections,
- browser proof.

## Domain Leak Inspection

Future agents must run leak scans after SB03, SB05, SB06, SB07, SB09, SB10, SB11, SB21, SB27, and SB28.

Minimum forbidden term set for generic projects:

```text
TetrisGame
Tetris
falling-piece
score storage
RecipePlannerPwa
recipe
meal plan
shopping list
IssueTriageDashboard
SLA badge
InvoiceApprovalPortal
invoice approval
```

These terms may appear only in scenario packs, tests, evidence, validation documents, and concrete app-source fixtures. They must not appear in generic Process projects or broad software-development/.NET driver contracts.
