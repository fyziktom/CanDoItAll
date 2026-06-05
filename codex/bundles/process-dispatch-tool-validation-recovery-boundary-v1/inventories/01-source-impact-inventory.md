# Source Impact Inventory

| Source | Expected role | Movement policy |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | Primary hotspot | Reduced to 1700 lines; delegates receipt, required-tool, critical-failure, completion-decision, and blocker-summary facts to local helpers. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Secondary consumer | Left unchanged by this bundle; final transitions remain in the dispatcher layer. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs` | Secondary recovery helper | Left unchanged by this bundle; directive text behavior covered by recovery tests. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs` | Secondary recovery helper | Reduced to 437 lines; retry packet categorization now consumes `ProcessRecoveryRetryDecisionRules` facts while persistence stays in the dispatcher. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationReceiptObservationHelper.cs` | Existing receipt observation seam | Now delegates normalization, successful receipt projection, family grouping, and failed-receipt detection to `ProcessToolReceiptFacts`. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessToolReceiptFacts.cs` | New module-local fact helper | 92 lines; owns normalized receipt facts and critical workspace-process receipt classification. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRequiredToolValidationRules.cs` | New module-local rule helper | 141 lines; owns required-tool missing calculations, carry-forward filtering, process mock satisfaction, and dotnet scaffold equivalence. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCriticalToolFailureRules.cs` | New module-local rule helper | 51 lines; owns latest unresolved critical workspace-process failure selection and stack-inapplicable dotnet suppression. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionBlockerRules.cs` | New module-local rule helper | 64 lines; owns typed aggregation of completion blocker summaries. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionDecisionRules.cs` | New module-local rule helper | 47 lines; owns terminal run-state, pending-approval, and failed-outcome completion decisions. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoveryRetryDecisionRules.cs` | New module-local rule helper | 66 lines; owns retry failure facts and missing/critical failure reason categories. |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Regression anchor | Added boundary helper architecture tests, dispatcher delegation checks, and proof-path policy scan. |
