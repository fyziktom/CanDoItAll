# Package index

## Starting point

[README](README.md) · [Version and limits](package.json) · [Changelog](CHANGELOG.md)

## Architecture documents

| File | Subject |
|---|---|
| [01-executive-recommendation.md](docs/01-executive-recommendation.md) | 01 · Executive recommendation |
| [02-current-state-and-evidence.md](docs/02-current-state-and-evidence.md) | 02 · Current state, provenance and audit coverage |
| [03-terminology-and-invariants.md](docs/03-terminology-and-invariants.md) | 03 · Terminology, identity and invariants |
| [04-target-architecture.md](docs/04-target-architecture.md) | 04 · Target architecture and dependency direction |
| [05-revision-boundaries.md](docs/05-revision-boundaries.md) | 05 · Revision boundaries and mutation semantics |
| [06-write-and-transaction-protocol.md](docs/06-write-and-transaction-protocol.md) | 06 · Write protocol, transaction modes and lock ordering |
| [07-outcomes-idempotency-and-retry.md](docs/07-outcomes-idempotency-and-retry.md) | 07 · Outcomes, durable operation identity and retries |
| [08-ui-drafts-and-conflicts.md](docs/08-ui-drafts-and-conflicts.md) | 08 · UI drafts, conflicts and postcommit reconciliation |
| [09-agent-bases-approvals-and-authority.md](docs/09-agent-bases-approvals-and-authority.md) | 09 · Agent decision bases, approval and changing authority |
| [10-projections-storage-and-external-effects.md](docs/10-projections-storage-and-external-effects.md) | 10 · Projections, files, external effects and compensation |
| [11-performance-and-capacity.md](docs/11-performance-and-capacity.md) | 11 · Performance, capacity and optimization plan |
| [12-foundation-prerequisites.md](docs/12-foundation-prerequisites.md) | 12 · Foundation prerequisites and improvement opportunities |
| [13-phase-plan.md](docs/13-phase-plan.md) | 13 · Phased plan, prerequisites and stopping boundaries |
| [14-projects-pilot-impact.md](docs/14-projects-pilot-impact.md) | 14 · Projects pilot: concrete change-impact map |
| [15-compatibility-migration-and-lifecycle.md](docs/15-compatibility-migration-and-lifecycle.md) | 15 · Compatibility, migration, import, restore and rollback |
| [16-test-strategy.md](docs/16-test-strategy.md) | 16 · Validation strategy and proof boundaries |
| [17-decoupling-and-iteration.md](docs/17-decoupling-and-iteration.md) | 17 · Coexistence with component decoupling and iterative review |
| [18-observability-and-operations.md](docs/18-observability-and-operations.md) | 18 · Observability, rollout and operational recovery |
| [19-decisions-and-open-questions.md](docs/19-decisions-and-open-questions.md) | 19 · Decisions, alternatives and open questions |
| [20-source-ledger.md](docs/20-source-ledger.md) | 20 · Source ledger |
| [21-test-catalogue.md](docs/21-test-catalogue.md) | 21 · Proposed test catalogue |
| [22-risk-register.md](docs/22-risk-register.md) | 22 · Risk register and traceability |

## Phase cards

- [P00 · Evidence refresh, writer perimeter and cost baseline](phases/P00.md)
- [P01 · Minimal foundation proof and prerequisite repairs](phases/P01.md)
- [P02 · Projects end-to-end protected edit pilot](phases/P02.md)
- [P03 · Narrow intentions and selective editor reconciliation](phases/P03.md)
- [P04 · One governed decision-basis and approval pilot](phases/P04.md)
- [P05 · Bounded owner adoption waves and structural invariants](phases/P05.md)
- [P06 · Storage, external delivery and projection integration](phases/P06.md)
- [P07 · Workflow and Process resume/publication integration](phases/P07.md)
- [P08 · Consolidation, measured optimization and release closure](phases/P08.md)

## Editable JSON registers and readable views

| Register | Readable view |
|---|---|
| [decisions.json](registers/decisions.json) | [decisions](registers/views/decisions.md) |
| [findings.json](registers/findings.json) | See architecture documents and JSON |
| [invariants.json](registers/invariants.json) | See architecture documents and JSON |
| [mutation_inventory.json](registers/mutation_inventory.json) | [mutation_inventory](registers/views/mutation_inventory.md) |
| [open_questions.json](registers/open_questions.json) | [open_questions](registers/views/open_questions.md) |
| [performance_scenarios.json](registers/performance_scenarios.json) | [performance_scenarios](registers/views/performance_scenarios.md) |
| [phases.json](registers/phases.json) | [phases](registers/views/phases.md) |
| [risks.json](registers/risks.json) | [risks](registers/views/risks.md) |
| [sources.json](registers/sources.json) | See architecture documents and JSON |
| [test_scenarios.json](registers/test_scenarios.json) | [test_scenarios](registers/views/test_scenarios.md) |
| [traceability.json](registers/traceability.json) | See architecture documents and JSON |

## Templates and tooling

- [BENCHMARK_REPORT.md](templates/BENCHMARK_REPORT.md)
- [NEXT_ITERATION_REVIEW.md](templates/NEXT_ITERATION_REVIEW.md)
- [PHASE_REVIEW.md](templates/PHASE_REVIEW.md)
- [rebuild_manifest.py](tools/rebuild_manifest.py)
- [refresh_evidence.py](tools/refresh_evidence.py)
- [render_register_views.py](tools/render_register_views.py)
- [test_package_tools.py](tools/test_package_tools.py)
- [validate_package.py](tools/validate_package.py)

## Integrity and package-only validation

[Package validation report](PACKAGE_VALIDATION.md) · [Content manifest](MANIFEST.json)

Application code was not built or benchmarked for this package. Proposed tests have no execution result.
