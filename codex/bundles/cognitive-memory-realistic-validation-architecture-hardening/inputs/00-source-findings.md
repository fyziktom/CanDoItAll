# Source Findings

## Validation Evidence Inputs

- Source bundle: `codex/bundles/cognitive-memory-cluster-search-realistic-validation`
- Clean PostgreSQL database: `candoitall_cognitive_memory_cluster_validation_20260519`
- Active clean profile at runtime: `07100b18-c326-4b4e-b517-7458d059a6e1`
- Source profile copied from previous validation: `541fbd90-f0e1-4ed7-b3bd-220dc4dd1473`
- Qdrant validation collection: `candoitall-validation-cognitive-memory`

## Troubles To Solve

| ID | Finding | Evidence |
| --- | --- | --- |
| ARCH-01 | Static asset serving breaks in no-build/production-like local startup. | `proof/host/web-run-stdout.log` |
| ARCH-02 | Runtime database override is not transparent enough in UI/API proof. | `proof/api/clean-active-status.json` |
| ARCH-03 | Database transfer lacks first-class external file/data payload transfer. | `proof/api/database-transfer-preview.json` |
| ARCH-04 | Default consolidation excludes restricted source truth without an operator-visible reason. | `proof/api/consolidation-run-1.json` |
| ARCH-05 | Candidate budgets stop restricted consolidation before all project-structure source truth is evaluated. | `proof/api/consolidation-run-2-restricted.json` |
| ARCH-06 | Dream aggregates can be source-mapped but too generic to approve. | `proof/api/dream-aggregate-controlled-rejections.json` |
| ARCH-07 | Probe turns drop the restricted session policy. | `proof/api/probe-turn-restricted-ask.json` |
| ARCH-08 | Probe recall does not pass vector projection options. | `proof/api/probe-turn-restricted-ask.json` |
| ARCH-09 | Qdrant projection works only with explicit options; health/default configuration needs better diagnostics. | `proof/api/qdrant-projection-rebuild.json` |
| ARCH-10 | Long-running validation needs resumable cycle orchestration, approval checkpoints, and metrics. | `reviews/01-execution-report.md` |
