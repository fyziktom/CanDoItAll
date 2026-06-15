# Validation Checklist

## File Completeness

| Check | Result | Evidence |
| --- | --- | --- |
| Improved bundle folder exists. | Pass | `codex/bundles/process-module-architecture-v3/` |
| Versioned path used. | Pass | v3 folder preserves v1/v2 as historical evidence and does not overwrite them. |
| README states architecture-only. | Pass | `README.md#status` |
| Required files are non-empty. | Pass | Validated by prepared-stage bundle validator. |
| Implementation subbundles are prepared but not executed. | Pass | `subbundles/01-*` through `subbundles/28-*` |
| Traceability is complete. | Pass | `traceability/01-requirement-traceability.md` |
| Source prompt coverage exists. | Pass | `traceability/02-source-prompt-coverage.md` |
| Red-team review exists. | Pass | `reviews/02-red-team-gap-review.md` |
| Future subbundle documents exist. | Pass | `subbundles/01-*` through `subbundles/28-*` |

## Content Completeness

| Check | Result | Evidence |
| --- | --- | --- |
| Current-state analysis grounded in files. | Pass | `analysis/04-current-code-evidence-map.md` |
| Keep/adapt/drop/replace decision log exists. | Pass | `analysis/05-reuse-decision-log.md` |
| Project boundaries defined. | Pass | `architecture/01-target-solution.md`, `plan/03-project-by-project-rebuild-plan.md` |
| Forbidden dependency rules defined. | Pass | `architecture/03-core-model-and-invariants.md` |
| Domain-neutral vocabulary and anti-leak rules defined. | Pass | `architecture/03-core-model-and-invariants.md` |
| Instance plan semantics defined. | Pass | `architecture/04-builder-and-instance-composition.md` |
| Builder pipeline and failures defined. | Pass | `architecture/04-builder-and-instance-composition.md` |
| Driver discovery and strategy binding defined. | Pass | `architecture/06-driver-strategy-and-manager-model.md` |
| Runtime state machines defined. | Pass | `architecture/05-runtime-dispatcher-and-state-machines.md` |
| Dispatcher claim/lease/idempotency defined. | Pass | `architecture/05-runtime-dispatcher-and-state-machines.md` |
| Manager decision/recovery/escalation behavior defined. | Pass | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` |
| Artifact ledger/reference semantics defined. | Pass | `architecture/07-artifact-error-recovery-and-subprocess-model.md` |
| Branch/switch and loop protection covered. | Pass | `architecture/03-core-model-and-invariants.md`, `architecture/05-runtime-dispatcher-and-state-machines.md` |
| Event store/outbox/projection/snapshot behavior defined. | Pass | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` |
| Template JSON source and migrations defined. | Pass | `architecture/09-template-git-versioning-and-migrations.md` |
| Git wrapper and Git UI boundaries defined. | Pass | `architecture/09-template-git-versioning-and-migrations.md` |
| Security/governance and agent auditing defined. | Pass | `architecture/10-security-governance-and-agent-change-auditing.md` |
| UI projection contracts and live/history semantics defined. | Pass | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` |
| Phase 0 archive/removal plan is detailed. | Pass | `plan/02-phase-0-reference-archive-and-removal.md` |
| Test strategy is project-by-project. | Pass | `validation/02-architecture-test-plan.md`, `plan/03-project-by-project-rebuild-plan.md` |
| v3 subbundle roadmap exists. | Pass | `plan/04-future-subbundle-roadmap.md` |
| v3 hardening gates exist. | Pass | `plan/05-review-checkpoints-and-hardening-gates.md` |
| Runtime history compatibility plan exists. | Pass | `architecture/17-runtime-history-migration-and-readonly-compatibility.md` |
| Current user-story map exists. | Pass | `analysis/06-current-implementation-user-story-map.md` |
| User-story architecture coverage model exists. | Pass | `architecture/18-user-story-coverage-model.md` |
| User-story traceability and validation exist. | Pass | `traceability/04-user-story-coverage-map.md`, `validation/04-user-story-coverage-validation.md` |
| .NET performance guardrails exist. | Pass | `analysis/07-dotnet-performance-antipattern-review.md`, `architecture/19-dotnet-performance-guardrails.md`, `validation/05-dotnet-performance-antipattern-checklist.md` |

## Anti-Vagueness Review

The anti-vagueness scan found no unresolved design markers. The word `deferred` appears only to mark implementation work as out of scope for this architecture-only pass. The word `future` appears to distinguish implementation phases from this proposal.

## Domain Leakage Review

Domain examples can appear in source prompt preservation and driver examples. Generic core/runtime design sections must not use domain-specific tool or framework names as model concepts. This is enforced by planned vocabulary leak tests.

## Source Evidence Review

The bundle explicitly records:

- `ProcessRunAutomationDispatchService` mixes EF, agent execution, workflow execution, artifact projection, recovery, browser proof, implementation proof, provider fallback, and claim lifecycle.
- `ProcessesService.StartRunAsync` creates runs, assignments, step runs, work briefs, journal entries, outbox records, and project-structure sync in one service path.
- Current branch routing uses text heuristics in `ProcessBranchOutcomeRouting`.
- Current observation is cache/query-built through `ProcessObservationCache` and `ProcessObservationService`.
- Current templates are JSON-based but use sidecar Markdown, Mermaid, and current-module projections.
- Current recovery has useful no-progress fingerprinting but is too coupled to agent-centric recovery options.

## Git Review

- `.gitignore` contains exceptions for `codex/bundles/process-module-architecture*/**`.
- Product source code is not modified in this architecture-only pass.
- Generated zip exports are not included.
- v1 and v2 remain preserved; v3 is the improved architecture and future-subbundle roadmap bundle.
- Current UI evidence captured for story mapping is stored under `codex/bundles/process-module-architecture-v3/evidence/ui-current-state/`.
