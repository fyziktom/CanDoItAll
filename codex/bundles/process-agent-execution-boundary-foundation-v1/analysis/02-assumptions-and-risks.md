# Assumptions And Risks

## Assumptions

- The previous provider hardening bundle is accepted as the baseline.
- Direct MAF product-tool references must remain forbidden.
- The process dispatcher is too large to rewrite in one bundle.
- The next bundle may introduce a minimal contracts/abstractions project, but must not move broad process runtime behavior into a new core.
- Existing public runtime tool names and access policies must remain stable.

## Critical Path Risks

- If the execution client/facade changes AgentFramework run semantics, process automation may falsely pass or fail steps.
- If execution run detail mapping is attempted too aggressively, receipt/artifact validation can regress.
- If the new contracts project starts absorbing EF entities or UI view models, it will become another module-level dependency knot.
- If architecture tests only check project references and not source-level `using`/type references, hidden coupling may return.
- If Codex uses mobile screenshots again, it will waste time and create irrelevant proof noise.

## Validation Risks

- Process-filtered integration tests may be slow and require PostgreSQL profile readiness.
- Execution paths include failure recovery, concurrent run adoption, stranded run recovery, structured output repair, and active-run observation; compile-only proof is not sufficient.
- Receipt and required-tool projection are easy to break while refactoring calls behind a facade.

## Reopen Triggers

- MAF regains any direct `CanDoItAll.Modules.Processes`, `Projects`, or `Workbench` dependency.
- `ProcessRunAutomationDispatchService.Execution.cs` still contains direct `workspaceService.ExecuteRunAsync` after SB06.
- Any public process runtime tool name changes without an explicit approved feature requirement.
- Any process receipt or artifact lineage smoke test fails.
- Any small/medium/mobile screenshot appears in proof for this bundle without explicit user-approved scope change.
