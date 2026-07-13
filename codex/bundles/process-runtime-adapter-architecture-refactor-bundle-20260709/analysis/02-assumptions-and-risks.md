# Assumptions And Risks

## Assumptions

- Existing process driver contracts are the preferred seam for domain-specific process behavior.
- `CanDoItAll.Processes.Drivers.Abstractions` can be extended with narrow interfaces when the contract is genuinely reused by runtime/adapter/driver implementations.
- `CanDoItAll.Modules.Processes` remains the integration/composition layer for MAF-specific adapter wiring.
- Some .NET-specific implementation may remain in `CanDoItAll.Modules.Processes` during migration if it is exposed through a driver-owned interface and no generic runtime/dispatcher code depends on .NET symbols.
- The generic MAF tool catalog can know tool protocol names, but receipt lifecycle classification for process/domain behavior must be provided through a classifier/policy seam instead of hardcoded one-off conditionals in receipt writing.

## Critical Path Risks

- The following risks must be treated as gate conditions during implementation, not as passive notes.

| Risk | Impact | Mitigation |
|---|---|---|
| Extracting services without deleting adapter behavior creates duplicate logic. | Divergent behavior and test confusion. | Each subbundle requires source assertions that moved behavior no longer remains in the adapter partial cluster. |
| Moving contracts into the wrong project creates cycles. | Runtime build breaks or broad shared project emerges. | SB02 must inspect project references before adding contracts and must run CodeAnalytics dependency proof after changes. |
| Domain driver seam becomes a service locator. | Runtime remains coupled indirectly to all drivers. | Driver policy selection must use typed driver catalog/factory contracts and explicit registrations. |
| Receipt writer refactor breaks generic tool receipt behavior. | Tool audit evidence may degrade. | Characterization tests must lock existing generic receipt output before extracting lifecycle classification. |
| Partial files are left as compatibility wrappers. | Fake modularity persists. | Final gate blocks closure unless no new partials were added and old partial responsibility files are deleted or have a dated removal subbundle. |
| Template audit is skipped because the observed incident was Tetris/calculator. | Other templates still trigger the same escalation class. | SB07 includes process-template and artifact-template coverage proof. |

## Validation Risks

- 5032 instance state may not be reproducible exactly after code changes. The implementation bundle must include both automated semantic regression tests and a manual/equivalent 5032 validation note.
- Existing tests may instantiate `AgentFrameworkProcessExecutionAdapter` for too many behaviors. New direct tests must prove extracted services independently.
- CodeAnalytics class diagram exports were truncated for large modules. Dependency/cycle results and exact symbol search are reliable enough for preparation, but implementation must refresh the snapshot after source changes.

## Reopen Triggers

Reopen this bundle before implementation closure if any of the following occurs:

- A new `AgentFrameworkProcessExecutionAdapter.*.cs` partial file is added.
- A new generic runtime/dispatcher condition checks a .NET, Blazor, Tetris, Calculator, QA, or software-delivery-specific string.
- A new contract project references an implementation/module/UI project.
- Tests for extracted behavior still construct the original adapter or full app host.
- A fix weakens completion gates or required receipts instead of routing failures correctly.
- A process template migration only adds prompt text when a typed execution contract is required.
