# SB05 Performance And EF Hardening

## Status

- `Ready`

## Objective

Fix the targeted plugin runtime performance and EF query issues found during preparation, without changing user-visible behavior or hiding earlier architectural problems behind caching.

## Success Criteria

- Latest connection queries are selected in EF instead of materializing all candidates first.
- OAuth workflow connection resolution reduces/orders candidates before materialization.
- Executor catalog/descriptor availability does not perform repeated sync database reads per descriptor.
- Installed manifest scanning remains direct-root only.
- Tests or source inspection prove every finding in `analysis/03-performance-and-ef-scan.md` is resolved or explicitly deferred with justification.

## Covered Inputs

- PRH-009 Performance And EF Hardening
- FIND-002, FIND-008, FIND-009, FIND-010

## Prerequisites

- SB01 progression gate passed.
- SB02 completed if log queries are included in optimization scope.
- Read `analysis/03-performance-and-ef-scan.md`.
- Read the `Performance EF Scan` rows in `inventories/plugin-runtime-architecture-hardening-checklist.xlsx`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\OAuth\PluginOAuthService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs`

## Deliverables

- Query fixes for latest connection and OAuth connection resolution.
- Batch/cached plugin grant availability path for executor catalog construction.
- Tests preserving connection selection semantics.
- Updated scan notes in execution report.

## Dependency Impact

- SB06 depends on this work to ensure Docker package install and executor catalog rendering remain smooth when Docker is installed at runtime.

## Validation Depth

- `Performance and EF correctness hardening`

## Implementation Steps

1. Reconfirm scan findings against current source after SB01/SB02 changes.
2. Move latest connection ordering/selection into EF in `FindFirstByKeyAsync`.
3. Refactor OAuth workflow connection resolution to order/reduce candidates before materialization while preserving scope matching semantics.
4. Replace per-descriptor sync grant evaluation with a scoped batch/cached availability model.
5. Keep any unavoidable in-memory JSON scope filtering bounded and documented.
6. Confirm installed manifest discovery remains direct-root after SB01.
7. Add or update tests for selection ordering and availability behavior.
8. Rerun targeted searches for the original anti-patterns.
9. Update execution report with findings resolved/deferred.

## Scope Exceptions

- Do not introduce a broad caching layer across unrelated modules.
- Do not change plugin grant semantics.
- Do not defer a finding without naming the risk and downstream impact.

## Do Not Do

- Do not remove `AsNoTracking` from read-only queries.
- Do not use sync EF calls from async request paths.
- Do not materialize unbounded query sets for latest-row selection.

## Acceptance Checklist

- [ ] `FindFirstByKeyAsync` query orders/selects in EF.
- [ ] OAuth connection resolution narrows candidates before materialization.
- [ ] Executor descriptor availability avoids repeated sync DB reads.
- [ ] Direct-root manifest enumeration remains intact.
- [ ] Tests cover ordering and availability behavior.
- [ ] Execution report maps every PERF finding to resolved/deferred status.

## Proof Required

- Targeted unit/integration tests for query semantics.
- `rg`/inspection output summary showing no recursive manifest scan and no targeted in-memory latest selection remains.
- Build/test command summaries in execution report.

## Browser Validation Logging

- N/A unless implementation changes browser-visible loading states or plugin page/canvas rendering.

## Progression Gate

- SB06 may proceed only after all high/medium targeted performance findings are resolved or explicitly accepted with a defensible reason in the execution report.

## Suggested Agent Prompt

```text
Implement SB05 only from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup.
Fix the targeted EF/performance findings from analysis/03-performance-and-ef-scan.md. Preserve behavior, add query/availability tests, rerun targeted searches, and update reviews/01-execution-report.md with resolved/deferred status for each finding.
```
