# Source Impact Inventory

| Source | Expected role | Movement policy |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Primary hotspot | Migrate pure rule/fact consumers gradually. |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Secondary consumer | Do not move final transitions; use for parity tests only unless SB13 proves narrow facts. |
| `ProcessRunAutomationDispatchService.RecoveryDirective.cs` | Secondary recovery helper | Do not move persistence; only inspect pure text/fact helpers. |
| `ProcessRunAutomationDispatchService.RecoveryPackets.cs` | Secondary recovery helper | Do not move packet persistence/creation unless explicitly proven pure and local. |
| `ProcessAutomationReceiptObservationHelper.cs` | Existing receipt observation seam | Reuse where possible; do not duplicate receipt parsing. |
| `ProcessArtifact*ValidationRules.cs` | Existing validation helpers | Reuse as examples of local rule-boundary style. |
| `ProcessRunAutomationDispatchServiceTests.cs` | Regression anchor | Add focused tests for required-tool, critical failure, completion and recovery facts. |
