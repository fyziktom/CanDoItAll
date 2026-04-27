# Base process prompt flexibility

## Status

- `Completed`

## Objective

Make `BuildExecutionPromptCore` and adjacent retry guidance platform-neutral while preserving generic process execution discipline.

## Covered Inputs

- `N001`: Base prompt is overfit to .NET/calculator work.
- `N006`: Atomic prompt-shape validation must come before process handoff tests.

## Prerequisites

- Bundle readiness gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.RecoveryDirective.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedRules.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Base execution prompt no longer emits calculator, Blazor scaffold, or .NET project-layout instructions.
- Generic rules for real work, required artifacts, failed validation handling, governed evidence, and final outcome comment remain.
- Focused tests prove the prompt is neutral for non-code and coding-like scenarios.

## Dependency Impact

- Subbundles 02-04 depend on this. Weak proof here invalidates every downstream real-agent/process scenario because all agents inherit the base prompt.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Remove or relocate technology-specific implementation guidance from `ExecutionPrompt.cs`.
2. Review retry guidance in `RecoveryDirective.cs` and remove prompt-level calculator assumptions when they are not guarded by specialized agent context.
3. Keep generic evidence and outcome rules.
4. Add focused tests for absence of .NET/calculator phrases and presence of generic process rules.
5. Run targeted tests for prompt construction.

## Scope Exceptions

- Existing low-level implementation-proof validators may remain if they inspect actual tool receipts for known historical failures; they must not be emitted in the base prompt for unrelated work.

## Do Not Do

- Do not remove the `PROCESS_STEP_OUTCOME` contract.
- Do not remove required artifact creation rules.
- Do not add a second generic prompt builder.
- Do not move calculator-specific instructions into another globally applied base prompt.

## Acceptance Checklist

- Prompt for a business-plan or analysis step contains no `Calculator`, `CalculatorEngine`, `Home.razor`, `workspace_dotnet_new`, or Blazor scaffold critical path.
- Prompt still tells the agent to complete actual work before writing summary artifacts.
- Prompt still requires durable artifacts and explicit validation failure handling.
- Relevant prompt tests pass.

## Proof Required

- Targeted `dotnet test` command for `ProcessRunAutomationDispatchServiceTests`.
- Execution report row with changed files and test result.

## Completion Proof

- Base prompt inspection found no `Calculator`, `Blazor`, `workspace_dotnet_new`, `CalculatorEngine`, `Home.razor`, or `net10.0` matches in `ProcessRunAutomationDispatchService.ExecutionPrompt.cs`.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~AgentFrameworkWorkspaceSeedIntegrationTests|FullyQualifiedName~BusinessPlanProcessPostgresIntegrationTests|FullyQualifiedName~LiveSpecialistAgentScenarioIntegrationTests" --no-restore` passed, including the prompt tests.

## Browser Validation Logging

- N/A. This subbundle affects generated prompt text and tests, not browser-visible UI.

## Progression Gate

- Downstream work may proceed only after prompt-shape tests pass and manual inspection confirms no global .NET/calculator guidance remains in the base execution prompt.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Make the base process execution prompt domain-neutral, preserve generic evidence rules, and add focused prompt-shape tests.
```
