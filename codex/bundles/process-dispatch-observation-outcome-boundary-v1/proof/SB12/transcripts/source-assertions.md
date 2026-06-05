Command: rg -n "ProcessAutomationSessionObservation|ProcessAutomationExecutionLogObservation|ProcessAutomationObservationSnapshot|ProcessDeclaredStepOutcomeRules" src/CanDoItAll.Modules.Processes/Automation/Dispatch tests/CanDoItAll.Tests.Integration/ProcessAutomationObservationTests.cs tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
Exit code: 0
Result: Source assertions found the module-local observation snapshot helpers, declared outcome helper, focused tests, and architecture guardrail scan.

Command: (Get-Content src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs | Measure-Object -Line).Lines
Exit code: 0
Result: 793 lines after extraction, below the bundle's 1400-line target.

Invariant: SB12-OBSOUTCOME-001
