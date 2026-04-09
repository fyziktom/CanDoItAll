# Assumptions And Risks

## Assumptions

- The root process-management bundle remains the source of truth for phase ordering and raw-note closure.
- Phase07 stays non-visual unless a later defect reopens editor or canvas surfaces around process-MCP workflows.
- The current session restart requirement is operational, not a product defect.

## Critical Path Risks

- If later work adds process-domain duplication inside the MCP instead of continuing to use `ProcessesService`, the architecture or canonical-model repair lanes must reopen.
- If the install scripts drift from the published artifact layout or config schema, the cross-repo convergence repair lane must reopen.

## Validation Risks

- The phase07 proof is non-visual, so weak closure would most likely come from config drift or install omission rather than from missing browser evidence.
- The unrelated `xUnit2031` warning in `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkforceProfileIntegrationTests.cs` remains outside this repair bundle scope.

## Reopen Triggers

- Reopen this bundle if `candoitall_processes` disappears from the published config files, if reinstall stops publishing the current process-MCP entrypoint, or if the MCP stops using canonical process services.
