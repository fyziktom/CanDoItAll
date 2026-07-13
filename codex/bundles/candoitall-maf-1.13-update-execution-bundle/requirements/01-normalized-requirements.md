# Normalized Requirements

| ID | Requirement | Acceptance Signal | Owner |
|---|---|---|---|
| `RQ-001` | Preserve the original prep bundle and raw request inside this execution bundle. | `inputs/original-prep` exists and source artifact index cites each source file. | `SB01` |
| `RQ-002` | Capture baseline branch, git status, package graph, and pre-existing restore/build/test state before changes. | Execution report includes command summaries and separates pre-existing failures. | `SB01` |
| `RQ-003` | Update stable MAF packages to the 1.13 line only. | Package refs show `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` at `1.13.0` where targeted. | `SB02` |
| `RQ-004` | Align direct dependency-floor packages only where needed for MAF 1.13. | `Microsoft.Extensions.AI.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions` changes are justified by restore/build output, not latest-version chasing. | `SB02` |
| `RQ-005` | Decide preview A2A and Mem0 package handling from NuGet CLI output. | Evidence records A2A candidate, Mem0 availability, and exact action taken. | `SB02` |
| `RQ-006` | Fix package-induced compile breaks inside existing MAF/workflow adapter seams. | Build succeeds; changed files are bounded to allowed adapter surfaces unless a documented gate approves more. | `SB03` |
| `RQ-007` | Preserve approvals, finalizers, structured output, provider gates, context manifests, runtime tool ownership traces, telemetry, and session compatibility. | Focused tests and source assertions cover each invariant. | `SB03`, `SB05` |
| `RQ-008` | Block direct process runtime tool or process API expansion. | Source scans show no new `ProcessAgentRuntimeToolProvider` and no route expansion. | `SB04`, `SB06` |
| `RQ-009` | Avoid large runtime refactors, new partial-class expansion, or fake separation during compile fixes. | Architecture gate passes with diff review, dependency direction review, and partial-class policy review. | `SB04` |
| `RQ-010` | Validate app behavior as before or better with focused and broad tests. | Focused unit/integration tests pass or failures are proven pre-existing; broad tests and optional UI/service smokes are recorded. | `SB05` |
| `RQ-011` | Produce durable evidence for package decisions, commands, scans, changed files, and skipped validations. | `docs/maf-1.13-update-evidence.md` and `reviews/01-execution-report.md` agree. | `SB06` |
| `RQ-012` | Provide a detailed phase checklist workbook. | `checklists/maf-1.13-phase-checklists.xlsx` contains phase, package, validation, architecture, risk, and evidence tabs. | Preparation |
