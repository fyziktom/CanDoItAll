# Requirement Traceability

| Requirement | Owning subbundle(s) | Bundle files | Planned proof |
| --- | --- | --- | --- |
| R01 | SB01, SB08 | `inputs/00-original-request.md`, `README.md` | Raw request closure table in execution report. |
| R02 | SB01 | `inventories/01-scope-inventory.md`, `architecture/00-csharp-current-state-inventory.md` | CodeAnalytics snapshot and source scans. |
| R03 | SB02, SB08 | `architecture/01-csharp-boundary-map.md` | Source assertion and facade delegation tests. |
| R04 | SB02 | `subbundles/02-turn-coordinator-and-runtime-facade/README.md` | Direct coordinator unit tests and integration smoke. |
| R05 | SB03 | `subbundles/03-streaming-finalizer-session-drivers/README.md`, `architecture/04-csharp-testability-plan.md` | Driver unit tests with negative cases. |
| R06 | SB04 | `subbundles/04-runtime-agent-factory-decomposition/README.md`, `architecture/03-csharp-pattern-selection-records.md` | Factory/build owner tests and source assertions. |
| R07 | SB05 | `subbundles/05-capability-composer-decomposition/README.md` | No final composer partial and direct capability owner tests. |
| R08 | SB06 | `subbundles/06-workspace-tool-family-extraction/README.md` | Workspace tool-set unit tests and host-visible smoke where applicable. |
| R09 | SB07 | `architecture/02-csharp-dependency-direction.md`, `plan/architecture-checkpoints.md` | `.csproj` table, build, CodeAnalytics dependency/cycle proof. |
| R10 | SB02-SB06 | `architecture/04-csharp-testability-plan.md` | Direct tests without `MafAgentRuntime`. |
| R11 | SB07-SB08 | `reviews/csharp-architecture-gate.md` | Architecture guard tests and source assertions. |
| R12 | SB01, SB08 | `analysis/02-assumptions-and-risks.md` | Timing transcripts and performance notes. |
| R13 | SB03, SB04, SB08 | `plan/01-phase-plan.md` | MAF handoff and focused unit/integration test transcripts. |

## Raw Note Closure

| Raw note | Closure plan |
| --- | --- |
| "It is still not finished well." | SB01 documents remaining hotspots; SB08 verifies old-class shrink and gate proof. |
| "new dotnet skills like csharp-modular-refactoring" | Skills are recorded in inputs and reflected in architecture sections/gates. |
| "root causes of troubles in architecture" | Current-state inventory and root cause in README/analysis. |
| "refactor it correctly" | Target boundary map, pattern records, and dependency-direction plan. |
| "proper isolations and testing" | Testability plan and direct-unit-test requirements in every critical subbundle. |
