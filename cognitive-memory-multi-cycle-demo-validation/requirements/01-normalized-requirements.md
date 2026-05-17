# Normalized Requirements

## R1 PostgreSQL-Isolated Multi-Cycle Runtime

Execution must create and use a fresh PostgreSQL database for this follow-up validation.

Success criteria:
- API status and database selection report PostgreSQL.
- The database name and connection string are recorded.
- The final live instance, loader, review decisions, and chat validation all use the same database.
- No validation proof depends on SQLite.

## R2 Staged Detailed Demo Corpus

The bundle must contain detailed staged source data for the six demo projects.

Success criteria:
- Four stage folders exist under `sample-data/staged-sources`.
- Each stage contains one detailed Markdown source file per project.
- Stage coverage includes baseline detail, operational updates, contradictions/decisions, and email/instruction assets.
- The source files include enough domain detail to evaluate useful chunking and summaries.

## R3 XLSX Source Traceability

Every source file must be referenced in an XLSX tracker that execution can extend.

Success criteria:
- `sample-data/trackers/cognitive-memory-demo-source-tracker.xlsx` exists and opens.
- The workbook maps source id, project, stage, path, expected memory signals, expected chat question, approval guidance, and quality checks.
- The workbook includes sheets for source manifest, cycle plan, chat probes, memory analysis, and repair log.

## R4 API-Only Staged Loading

Execution must load every stage through APIs and project structure surfaces, not direct database writes.

Success criteria:
- Each source file upload has API evidence.
- Project Markdown asset node creation/update has API evidence where used.
- Each stage records ingestion operation ids and final status.

## R5 Forced Memory/Dreaming Cycles

After each stage, execution must force the memory processing cycle instead of waiting for a scheduler.

Success criteria:
- Each stage runs project/process ingestion as appropriate.
- Each stage runs consolidation/dreaming through the available Cognitive Memory API.
- Each stage captures before-review and after-review snapshots.

## R6 Review, Approval, Duplicate, And Contradiction Decisions

Between stages, execution must inspect review candidates and record decisions.

Success criteria:
- Useful recommendations are approved with notes.
- Duplicates are rejected, deferred, or marked needs-changes with notes.
- Contradictions and superseded claims are resolved deliberately.
- Review evidence includes proposed memory text, source excerpt, and decision result.

## R7 Backward Memory Quality Analysis

Execution must analyze whether stored memories remain useful and source-grounded after multiple cycles.

Success criteria:
- Approved memory records are traced back to source files and expected signals in the XLSX tracker.
- Summaries/chunks are scored for usefulness, scope, duplication, and source correctness.
- The analysis explicitly checks vector/projection context where available, and records provider limitations where unavailable.
- Cross-project leakage and wrong-source references are treated as defects.

## R8 AI Chat Validation

Execution must test project-specific AI chat behavior using questions derived from the tracker.

Success criteria:
- Chat probes ask about each project and stage.
- Answers are checked against expected source evidence and memory references.
- Failures distinguish missing memory, wrong memory, cross-project leakage, unsupported confidence, and chat integration defects.
- Direct recall-only testing is allowed only as a fallback with an explicit blocker for chat API access.

## R9 On-The-Fly Repair Subbundles

If execution discovers faulty memory behavior, the agent must create repair subbundles during the run.

Success criteria:
- A discovered defect is not hidden as residual risk when it blocks useful memory behavior.
- The repair subbundle states the observed evidence, owning files, planned fix, and proof.
- Execution reruns the affected cycle or chat probe after repair.
- Final closure lists every created repair subbundle and its status.

## R10 Closure Evidence

The bundle cannot close until staged loading, repeated cycles, review decisions, backward analysis, chat validation, and final bundle validation are recorded.

Success criteria:
- `reviews/01-execution-report.md` contains stage-by-stage evidence.
- The XLSX tracker is updated or accompanied by exported analysis evidence.
- Browser artifacts are captured for review UI steps when UI is used.
- The completed-stage validator passes.
