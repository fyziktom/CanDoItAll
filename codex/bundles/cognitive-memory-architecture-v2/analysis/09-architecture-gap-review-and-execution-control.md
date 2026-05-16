# Architecture Gap Review And Execution Control

## Status

- Architecture-only repair added after manual review on 2026-05-16.
- No product implementation was performed.

## Evidence Used

- Bundle validator passed before this repair:
  `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2 --profile initiative --stage prepared`
- CodeAnalytics snapshot used for current source orientation:
  `snap-20260516150857-2c8fb8f3`
- Scoped source projects inspected:
  `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Core`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Workbench`, `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.Automation`, `CanDoItAll.Modules.SchedulerPlanner`, `CanDoItAll.Infrastructure`, `CanDoItAll.SharedKernel`, and focused unit/integration test projects.

## Architecture Weaknesses Found

### G-001 Execution State Is Too Text-Only

The bundle has strong phase ordering, but the durable execution state was mostly narrative. That is not enough for a long implementation where an agent may resume after compaction or hand off to another agent.

Repair:

- Added a structured workbook checkpoint contract under `checklists/`.
- Expanded the execution report to track every phase, not only recently added patch phases.
- Added a phase-handoff protocol to `plan/01-phase-plan.md`.

### G-002 First Vertical Slice Was Too Projection-Centric

The previous first vertical slice still read as source -> canonical item -> projection -> recall. That can tempt implementation to build recall before claim/evidence, mutation authority, score geometry, workspace, and answer gating are ready.

Repair:

- The first slice now explicitly flows through source/evidence anchors, atomic claims, context frames, mutation authority, score geometry, workspace focus/inhibition, and metamemory answer gate.
- Qdrant remains optional in the first slice; lexical/relational recall may prove source truth before vector projection is wired.

### G-003 Project Split Was Too Large For A First Implementation

The earlier recommended project shape listed many new projects. That is clean in theory, but high-risk in this repo because the first implementation still has to prove real boundaries against existing modular composition, EF registration, MAF contracts, and source adapters.

Repair:

- The target solution now recommends the smallest initial project shape:
  `CanDoItAll.Modules.CognitiveMemory` plus `CanDoItAll.CognitiveMemory.Abstractions` only when cross-project contracts require it.
- Dedicated Core/Rag/Semantics/Maf/Components projects are deferred until dependency pressure justifies them.

### G-004 Source Scope Undercounted AgentFramework.Core

Several critical contracts live in `CanDoItAll.AgentFramework.Core`, including context contribution and source snapshot contracts. The bundle referenced these files but the source scope summary did not name the project clearly enough.

Repair:

- The root source-inspection scope now includes `CanDoItAll.AgentFramework.Core`.
- Phase gates explicitly require validating those contracts before MAF or source ingestion work proceeds.

### G-005 Traceability Still Reflected Older Ownership In Places

Some requirements were mapped to older owning phases. Working memory belongs first to workspace/attention, not MAF integration. Episodic/procedural memory need replay/procedure phases, not consolidation alone.

Repair:

- Traceability was updated so working memory, episodic memory, procedural memory, reflection, and execution-control requirements point to the actual foundation phases.

### G-006 Memory Reconsolidation Was Implied But Not Operational Enough

The architecture correctly says memory is not overwritten directly, but long-lived cognitive memory needs an explicit reconsolidation rule: retrieved and corrected memory becomes labile, produces evidence/review/mutation commands, and only then updates active belief state.

Repair:

- Risks, acceptance criteria, and quality gates now require reconsolidation/revision lineage proof for corrections, stale source refresh, probe feedback, and learning outcomes.
- Soft forgetting remains explicit: dormant/stale/superseded/retired projection states are allowed; raw source deletion is not a memory-cleanup mechanism.

### G-007 UI Proof Depends On Existing Component Libraries

The UI phases must not drift into raw div-heavy screens. The current repo does not use Radzen, so UI implementation should use existing CanDoItAll BaseLib/CanvasLib/shared component patterns.

Repair:

- UI proof remains attached to browser evidence, but the execution checklist now tracks shared-component usage and design-system consistency as first-class phase checks.

## Execution-Control Rule

The workbook is not decorative. During implementation it is the durable phase ledger.

Before each phase:

- Set the phase row to `In Progress`.
- Confirm all prerequisite phase rows are `Passed`.
- Record target branch, active commit, and dependency evidence.

During the phase:

- Update checklist rows as work lands.
- Record proof paths as soon as they exist.
- Mark blockers explicitly instead of leaving blank cells.

Before closing the phase:

- Set every owned checklist item to `Passed`, `Deferred`, or `Blocked`.
- Add validation evidence paths.
- Add a handoff-log row.
- Update `reviews/01-execution-report.md`.

No downstream phase may start when the workbook and execution report disagree.
