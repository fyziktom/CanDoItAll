# Current Hotspots

| File | Observed issue | Next action |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.Dispatch.cs` | Still ~1998 lines after route/hydration work. `LoadDispatchCandidateAsync` still constructs candidates inline. | Extract candidate factory / construction boundary. |
| `ProcessRunAutomationDispatchService.Cooperation.cs` | Cooperation/workspace-profile classification is driver-adjacent but still dispatcher-owned. | Move to local helper and document driver-readiness. |
| `ProcessDispatchCandidateHydrationLoader.cs` | Good readback seam, still returns EF entities. | Do not move to Core; keep module-local. |
| `ProcessDispatchTechnicalAgentBindingCoordinator.cs` | Correctly owns side effect. | Keep explicit; do not hide behind pure factory. |
| `ProcessDispatchRecoveryQueryHelper.cs` | Good start, but helper delegates recoverable execution selection back to dispatcher wrapper. | Optionally consume `ProcessAutomationExecutionRunSelection` directly if safe. |
