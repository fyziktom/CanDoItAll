# Structured Input

## Core Objective

- Convert the two input project packs into detailed, time-sliced CanDoItAll project structures and validate Cognitive Memory behavior over multiple ingest/consolidation/recall cycles.

## Success Criteria

- Both source packs are extracted into bundle-local derived text artifacts.
- Each project has at least five chronological source-truth groups, exceeding the minimum of four.
- Project structure is generated from analyzed source-truth headings, with parent/child depth beyond three layers.
- Financial plans, investment ramps, team growth, construction or facility assumptions, and risks are represented as project nodes.
- API evidence proves project creation, node creation, link creation, readback, external-source upload, ingestion, consolidation, review decisions, snapshots, and recall probes.
- Memory-quality analysis compares recall context against required source-truth facts.

## Hard Constraints

- Do not add source-pack content to application code.
- Do not load raw source files as a flat project structure.
- Use CanDoItAll APIs for project-structure and Cognitive Memory control.
- Treat source-truth documents in this bundle as the validation baseline.
- Approve, reject, or defer Cognitive Memory review items based on comparison to source truth.

## Allowed Side Effects

- Create and update bundle files under `codex/bundles/realistic-project-memory-validation`.
- Create projects, nodes, links, external source records, review decisions, and Cognitive Memory records through the local CanDoItAll API.
- Change C# implementation only if validation evidence exposes a concrete defect.

## Source Artifacts

- `C:\repositories\CanDoItAll\codex\bundles\input\AI kohoutek`
- `C:\repositories\CanDoItAll\codex\bundles\input\Glass_Recycle_Curacao_Master_Pack_Government_Submission_Checked_2026-04-06`
- `C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\inputs\extracted`
- `C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth`

## Input Coverage Signals

- AI Tap coverage must include water savings, safety, B2C/B2B markets, channels, product architecture, software constraints, prototype paths, development timing, manufacturing, team ramp, facilities, budget, product cost, funding, cash flow, and risks.
- Curacao coverage must include feedstock, market/offtake, corrections, process design, material balance, energy/water, storage, pre-FID gates, site/permitting, FEED/procurement, construction, commissioning, staffing, OPEX, EBITDA, scenarios, ancillary products, and risks.
- XLSX financial data is treated as first-class source truth, not optional appendix data.

## Dependency And Sequencing Signals

- Source extraction unlocks source-truth normalization.
- Source-truth normalization unlocks API loading.
- Project readback unlocks ingestion and consolidation validation.
- Snapshot/review evidence unlocks recall probing.
- Recall-analysis failures unlock implementation repair.

## Validation Expectations

- Validation must prove structure and behavior, not just successful API calls.
- Required recall terms in `source-truth/source-manifest.json` are the minimum source-truth comparison checks.
- Missing source locators, empty recall packs, or missing required terms are defects to investigate.

## Evidence Contract

- Prepared bundle validator output.
- `validation/evidence/<runId>/99-run-summary.json`.
- `validation/evidence/<runId>/95-memory-quality-analysis.json`.
- `validation/evidence/<runId>/96-memory-quality-analysis.md`.
- Any implementation repair must include build/test proof and before/after recall evidence.

## UI Validation Strategy

- N/A. This task validates API behavior and memory content, not browser-visible UI.

## Browser Validation Analytics

- N/A. Browser validation rows in the execution report remain explicit N/A entries.

## Working Assumptions

- The local CanDoItAll web API is available at `http://localhost:5032` unless overridden.
- PostgreSQL-backed Cognitive Memory is expected for the validation run.
- The normalized source-truth markdown is acceptable as derived validation data because it remains bundle-local and is loaded through APIs.

## Primary Risks

- Recall may omit facts because review decisions or projection behavior fail to promote source-truth candidates.
- Project-structure ingestion may overproduce duplicate memories from both nodes and files.
- If the active memory provider is not PostgreSQL, the validation must stop unless explicitly overridden.
