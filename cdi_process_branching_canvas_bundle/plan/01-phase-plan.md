# Phase Plan

## Phase Sequence

1. Repair the bundle from the live repository so the latest raw notes, screenshot cues, many-to-many concern, and persistence concern are reflected before more product code changes begin.
2. Rerun the prepared-stage validator after the bundle repair and before more implementation work.
3. Execute `01-scenario-definition-and-live-gap-reconciliation` first so the branch scenarios, join-style-input expectation, and persistence trouble list are explicit before more shared canvas refactoring begins.
4. Execute `02-advanced-canvas-node-contract` next because left-click connector authoring and exact badge-anchor geometry both depend on shared workbench interaction and rendering behavior.
5. Execute `03-process-branch-node-authoring-and-mapping` only after the shared contract is proven and still additive to legacy canvases; this phase owns process-side mapping, router badge completeness, many-to-many support or explicit blocker writeback, and canonical layout persistence.
6. Execute `04-software-development-branching-examples-and-regression-coverage` only after process authoring and persistence behavior work in the real process workspace.
7. Execute `05-browser-proof-and-final-closure` last, including left-click walkthrough, screenshot review, persistence round-trip proof, raw-note closure, bundle sync, and final validator passes.

## Subbundle Dependency Map

```mermaid
flowchart TD
    P["Prepared bundle gate"] --> S1["01 Scenario Definition And Live Gap Reconciliation"]
    S1 -->|Scenario map, join semantics, and persistence troubles log trusted| S2["02 Advanced Canvas Node Contract"]
    S2 -->|Left-click gesture and badge-anchor contract proven in tests and browser smoke| S3["03 Process Branch Node Authoring And Mapping"]
    S3 -->|Branch node authoring, many-to-many handling, and persisted layout proven on /processes| S4["04 Software Development Branching Examples And Regression Coverage"]
    S4 -->|Seeded scenarios and regression coverage trusted| S5["05 Browser Proof And Final Closure"]
    S5 --> C["Completed bundle gate"]
```

- `01` is the semantic and canonical-truth foundation.
- `02` is the shared interaction and rendering foundation.
- `03` is the feature-delivery and persistence foundation.
- `04` proves the feature on realistic software-development flows.
- `05` closes the original notes and validates browser truth.

## Critical Subbundles

- `subbundles/01-scenario-definition-and-live-gap-reconciliation`
  - This is a critical foundation because later implementation must not silently narrow the requested branch types, join semantics, or persistence expectations.
  - Required deeper validation before downstream work continues: explicit scenario inventory, explicit architecture trouble list, confirmed many-to-many and persistence assessment, and confirmed mapping from raw notes to implementation surfaces.
- `subbundles/02-advanced-canvas-node-contract`
  - This is a critical UI foundation because later screenshots and route proof are invalid if the connector gesture, port geometry, or fallback behavior is wrong.
  - Required deeper validation before downstream work continues: component or renderer tests plus one dependent browser smoke on `/processes`.
- `subbundles/03-process-branch-node-authoring-and-mapping`
  - This is a critical product foundation because later seeded examples depend on correct branch-node creation, join semantics, and persisted layout behavior.
  - Required deeper validation before downstream work continues: left-click connection proof, route persistence or rebuild proof, persisted move proof, and screenshot review of multi-port readability.

## Phase Gates

- Gate after preparation
  - Run `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle --profile initiative --stage prepared`.
  - Audit the bundle with the bundle-validator skill and repair any missing dependency, source-reference, or browser-proof planning defects.
  - If the repaired bundle changes requirements materially, do not continue until the prepared-stage validator passes again.
- Gate before each subbundle
  - Re-read the current subbundle README, confirm prerequisites, and verify that exact source references still match the repo.
  - Reopen an earlier critical foundation immediately if current observations contradict its proof.
- Gate after each subbundle
  - Update `reviews/01-execution-report.md` with command results, browser analytics, screenshot paths, and the subbundle gate row while evidence is fresh.
  - Run the closure check against the subbundle acceptance checklist and progression gate before starting downstream work.
- Gate before closure
  - Reopen the original raw request, including both follow-up messages, mark each raw note `Solved`, `Partially solved`, or `Not solved`, and cite code plus browser proof.
  - Run the completed-stage validator and pass the final bundle-validator gate before calling the workflow finished.
