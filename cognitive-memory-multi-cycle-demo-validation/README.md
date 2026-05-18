# Cognitive Memory Multi-Cycle Demo Validation

This bundle is a coordination and execution package for `cognitive-memory-multi-cycle-demo-validation`.

## Profile

- `initiative`

## Mission

- Validate Cognitive Memory over several forced ingestion/consolidation cycles using richer staged demo-project data, source traceability, review decisions, duplicate analysis, backward memory-quality analysis, and AI-agent chat probes.

## Outcome Contract

- Requested outcome: prepare and execute a multi-cycle Cognitive Memory demo validation where additional project data is loaded in stages, memory is forced through its consolidation/dreaming cycle after each stage, review recommendations are approved or rejected, and final chat answers prove whether useful project memories are retained.
- Hard constraints: use PostgreSQL for all execution proof, load data through APIs and project structure surfaces, keep staged sample data in this bundle rather than automated test code, track every source file in the XLSX workbook, and create on-the-fly repair subbundles when execution discovers faulty memory behavior.
- Evidence required before closure: API status, staged loader output, cycle snapshots before/after approvals, review decision logs, duplicate/contradiction analysis, memory-quality analysis tied back to the XLSX tracker, AI chat transcripts, browser proof where UI review is used, and final bundle validation.
- Known blockers or explicit scope exceptions: none. The final execution path used PostgreSQL database `candoitall_cognitive_memory_multicycle_20260517_03`; SQLite was not used for behavioral proof.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `sample-data/` staged Markdown source packets and the XLSX source tracker
- `validation/` helper scripts for corpus/tracker generation and verification
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-staged-demo-corpus-and-trace-workbook`
2. `subbundles/02-api-stage-loader-and-cycle-observation`
3. `subbundles/03-review-approval-and-memory-quality-analysis`
4. `subbundles/04-ai-chat-memory-validation-and-repair-loop`
5. `subbundles/05-repair-recall-lexical-activation`
6. `subbundles/06-repair-agent-chat-persistence-and-project-marker-memory`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If execution discovers faulty chunking, weak summaries, incorrect source references, duplicate handling failures, or poor chat recall, create a new repair subbundle under `subbundles/` before continuing the later proof.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, `sample-data/trackers/cognitive-memory-demo-source-tracker.xlsx`, and `reviews/01-execution-report.md` as the durable state.

## Prepared Artifacts

- Staged source files: `sample-data/staged-sources/`
- Source manifest: `sample-data/source-manifest.json`
- XLSX tracker: `sample-data/trackers/cognitive-memory-demo-source-tracker.xlsx`
- Corpus builder: `validation/scripts/build-demo-corpus.mjs`
- Tracker verifier: `validation/scripts/verify-demo-tracker.mjs`

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
- Primary execution evidence: `validation/evidence/20260517-181521`
- Post-repair recall evidence: `validation/evidence/20260517-181521-post-repair-recall-20260517-183324`
- Automatic project-marker chat evidence: `validation/evidence/20260517-181521-agent-chat-project-marker-20260517-190859`
- UI evidence: `validation/evidence/20260517-181521/browser`
