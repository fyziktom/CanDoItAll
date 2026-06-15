# Final E2E Project Structure Source Scenarios

## Purpose

This file records source information obtained from the running development instance at `http://localhost:5032/` and converts it into future final E2E source scenarios for the Process rewrite.

The `TetrisGame` data is evidence from the current application. It is not a generic-domain model. Future implementation may use it as E2E input data, but no Tetris-specific term or rule may leak into generic Process Core, Runtime, Dispatcher, Builder, Manager, Artifact, Monitoring, Template, Projection, or API contracts.

A concrete architecture-seed JSON pack is stored at `evidence/e2e-source-project-structures/final-e2e-scenario-source-packs.json`. That file is not the final API DTO contract; it is source material that future SB27/SB28 implementation must convert into public API imports or tests without bypassing validation, launch planning, candidate readiness, artifacts, events, projections, or authorization.

## Live Instance API Evidence

Commands used against the running instance:

```http
GET http://localhost:5032/api/access/status
GET http://localhost:5032/api/project-structure/projects
GET http://localhost:5032/api/project-structure/projects/3324868f-66e2-478a-bb8f-14f32a5db1e9/hierarchy
POST http://localhost:5032/api/project-structure/projects/3324868f-66e2-478a-bb8f-14f32a5db1e9/structure/read
GET http://localhost:5032/api/processes/definitions/a9bf7ad8-a5d9-4fdc-af3d-7462c44576cd/export
GET http://localhost:5032/api/processes/runs/82ebf8f9-de88-4748-b67b-5fb46f29159d
```

The access status response showed API enabled, OpenAPI enabled, and authorization disabled in this local development instance.

## TetrisGame Source Snapshot

Project summary:

| Field | Value |
| --- | --- |
| Project id | `3324868f-66e2-478a-bb8f-14f32a5db1e9` |
| Project name | `TetrisGame` |
| Status | Active |
| Current phase | `WIP` |
| Parent projects | None |
| Child projects | None |
| Structure read options | `includeLinks=true`, `includeMetadata=true`, `includeNotes=true`, `take=200` |
| Node count observed | 13 |
| Link count observed | 14 |

Important project-structure nodes:

| Node id | Parent id | Type / subtype | Title | Status | E2E relevance |
| --- | --- | --- | --- | --- | --- |
| `project:3324868f-66e2-478a-bb8f-14f32a5db1e9` | none | Project | `TetrisGame` | Active | Project root. |
| `custom:94212a9f6dbe4014aadb1b5ee7b3513a` | project root | ProjectBlock / research | `main customer request` | Draft | Customer-intake source group. |
| `custom:99f53b6f1f594af4a87b73949545d9d8` | customer request | Workflow | `Example: Office365 Category Email Summary To Project` | Completed | Source workflow that produced the request summary. |
| `custom:f7ac7bb86956464abb2bcc2b5cf749d0` | workflow | File / markdown | `Office365 category email summary` | Draft | Primary customer request evidence. |
| `custom:f09c64048bc946f3a52d0aa51854ae25` | project root | ProjectBlock / architecture | `Main Architecture` | Draft | Architecture source group. |
| `custom:cfdf1309669842bea91475acb0b6ad1c` | Main Architecture | ProjectBlock / architecture | `Blazor WASM PWA app shape` | Draft | App-type architecture constraint. |
| `custom:b6a01bdeb6e34281bb1f25876fd0326e` | Main Architecture | ProjectBlock / architecture | `Game loop and board behavior` | Draft | Domain-specific game behavior. Scenario data only. |
| `custom:204ad6b854f449c19155e78f7656b6ac` | Main Architecture | ProjectBlock / architecture | `IndexedDB score storage` | Draft | Local persistence constraint. |
| `custom:833333d5f35b4b5cbe2fbc3ac3b51f2a` | Main Architecture | ProjectBlock / architecture | `Responsive game screen and acceptance` | Draft | Browser UI acceptance constraint. |
| `custom:bd8169fc3fa944dbafd13998fb167fe8` | Main Architecture | ProjectBlock / delivery | `Output folder` | Draft | External artifact destination constraint. |
| `custom:cfd406780f034384a70ea6b87507422a` | project root | ProjectBlock / delivery | `Main App` | Draft | Project-structure target node for process launch. |
| `process-definition:a9bf7ad8-a5d9-4fdc-af3d-7462c44576cd` | Main App | Process definition | `Multi-team software delivery and release governance` | Published | Linked process definition. |
| `process-run:82ebf8f9-de88-4748-b67b-5fb46f29159d` | Main App | Process run | `Main App / Multi-team software delivery and release governance` | Blocked | Current run projection. |
| `process-run-output:82ebf8f9-de88-4748-b67b-5fb46f29159d:59bdcc3ed8db` | process run | File/folder | Run output folder | Stored | Process artifact folder. |

Primary customer request facts from the markdown source:

- Build a simple browser-based Tetris game.
- Website is the immediate target; mobile app is possible later and must not be treated as current scope.
- Controls must support keyboard input.
- The app must save the last maximum score.
- No backend is wanted.
- Static web hosting is the desired deployment target.
- Delivery target is one week.
- Open gaps include scoring details, levels, pause/restart, game-over behavior, visual design, brand assets, and mobile scope.

Architecture constraints from project-structure blocks:

- App type: Blazor WebAssembly PWA, frontend-only, static-host friendly, offline-friendly shell.
- Game mechanics: falling-piece loop belongs in a game engine/service layer, not Razor event handlers; include spawn, move, rotate, lock, clear-line, collision, and game-over behavior.
- Storage: IndexedDB is the only persistence mechanism for the last maximum score; no remote storage or API dependency.
- UI acceptance: board and HUD must fit within viewport without horizontal or vertical scrolling; responsive sizing must scale the game down.
- Output destination: final app must be placed in `C:\programovani\dotnet\output`.

Linked process evidence:

| Field | Value |
| --- | --- |
| Process definition id | `a9bf7ad8-a5d9-4fdc-af3d-7462c44576cd` |
| Definition name | `Multi-team software delivery and release governance` |
| Definition source format | `CanDoItAll.ProcessDefinition/v2` |
| Definition shape | 7 roles, 20 steps |
| Run id | `82ebf8f9-de88-4748-b67b-5fb46f29159d` |
| Run status | Blocked |
| Run operating mode | Assisted execution |
| Completed steps | 2 of 20 |
| Blocked steps | 1 |
| Missing artifact count | 2 |
| Completed subprocess | `.NET architecture design and review subprocess`, 4 of 4 steps complete |
| Blocked subprocess | `.NET implementation slice subprocess`, 0 of 6 steps complete, 1 blocked |
| Missing blocked-step artifacts | `Implementation change set`, `Migration and rollout preparation checklist` |
| Open escalations observed | Two open operator-review escalations for the blocked implementation subprocess |

E2E behaviors represented by this source:

- Project-scoped process link from a project-structure node.
- Source request ingestion through workflow-produced markdown.
- Process launch from project structure.
- Process run projection written back under the project node.
- Process output folder projection.
- Subprocess completion and subprocess blocking.
- Missing artifact detection.
- Manager/operator escalation for blocked subprocess.
- Rework packet and rerun directive preservation.
- Project-structure source constraints affecting process artifacts and manager decisions.

## Additional Final E2E Scenarios

Final E2E must include at least three additional app scenarios. They are intentionally similar enough to reuse the same software-delivery process patterns, but different enough to catch Tetris-shaped or game-shaped implementation shortcuts.

### Scenario A: RecipePlannerPwa

Purpose: Offline-first meal planner and recipe organizer.

Project-structure source:

- Root project: `RecipePlannerPwa`.
- Customer request block: build a static-hosted Blazor WASM PWA for recipes, weekly meal plans, and shopping lists.
- Architecture blocks:
  - frontend-only Blazor WASM PWA,
  - IndexedDB recipe and plan storage,
  - JSON import/export,
  - responsive print-friendly shopping list,
  - no backend for the first release.
- Process target node: `Main App`.

Expected E2E checks:

- Same process template can ingest the source without game-specific assumptions.
- Artifact expectations mention recipe, meal-plan, import/export, and print proof only as scenario data.
- Browser proof validates CRUD, persistence, import/export, and print-friendly layout.
- Generic code contains no `RecipePlannerPwa`, recipe-specific, or meal-planning branches.

### Scenario B: IssueTriageDashboard

Purpose: Project issue triage and prioritization dashboard.

Project-structure source:

- Root project: `IssueTriageDashboard`.
- Customer request block: build a dashboard for viewing issues by severity, owner, SLA, and release impact.
- Architecture blocks:
  - Blazor Web App or Blazor Server/SSR,
  - backend API with persistence,
  - seed/import issue data,
  - role-based reviewer and maintainer views,
  - audit log for triage decisions.
- Process target node: `Main App`.

Expected E2E checks:

- The same process architecture handles backend/API/database work without frontend-only assumptions.
- Candidate readiness checks include data mutation rights, repository access, build/test tools, and approval rights.
- Browser proof validates filtering, owner assignment, SLA badges, and audit entry creation.
- Generic code contains no issue-dashboard-specific branches.

### Scenario C: InvoiceApprovalPortal

Purpose: Lightweight invoice review and approval portal.

Project-structure source:

- Root project: `InvoiceApprovalPortal`.
- Customer request block: build a secured portal for uploading invoice metadata, reviewing exceptions, and approving or rejecting invoices.
- Architecture blocks:
  - .NET web app with authenticated roles,
  - file metadata and approval records,
  - security review required for sensitive business data,
  - exportable approval history,
  - no public anonymous access.
- Process target node: `Main App`.

Expected E2E checks:

- The same process architecture handles security-sensitive domain constraints through policy, not hardcoded invoice behavior.
- Role candidate readiness must surface missing approval authority or security-review rights.
- Artifact lifecycle validates approval history, security review evidence, and export proof.
- Generic code contains no invoice-specific branches.

## Genericity Rules For Scenario Data

Allowed locations for scenario-specific vocabulary:

- `codex/bundles/**/evidence/**`
- `codex/bundles/**/analysis/**`
- `codex/bundles/**/validation/**`
- future E2E scenario pack files,
- future tests named for scenario coverage,
- screenshots and Playwright proof labels,
- generated project-structure source data loaded as test input.

Forbidden locations for scenario-specific vocabulary:

- Process Core contracts,
- Runtime state machines,
- Dispatcher contracts,
- Builder/plan compiler contracts,
- Manager generic loop contracts,
- Artifact ledger contracts,
- Monitoring event/snapshot contracts,
- Template/Git core contracts,
- UI projection contracts,
- shared application interfaces that must remain domain-neutral.

Scenario-specific terms are also forbidden in broad software-development and .NET driver contracts unless they are only sample data in tests. The .NET driver may know about generic software-development concepts such as app type, build command, test command, browser proof, static hosting, database migration, package output, security review, and release boundary. It must not know about `TetrisGame`, recipes, issues, invoices, score rules, meal plans, SLA screens, or invoice approvals as model concepts.
