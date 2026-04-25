# 02 process template qa repair model

## Status

- `Completed`

## Objective

Create a deterministic calculator process model that actually exercises QA rejection, developer repair, QA recheck, approval, and release progression through the process graph.

## Covered Inputs

- REQ-004: deterministic calculator process model.
- REQ-005: branch outcome keys compatible with mock QA outputs.

## Prerequisites

- Subbundle 01 progression gate must pass.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\ai-assisted-change-delivery\definition.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackResourceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeProgressionPlanner.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplateProjectionServiceTests.cs`

## Deliverables

- A deterministic calculator process definition or template fixture with these steps:
  - scope
  - architecture
  - first implementation
  - QA first review with `repairs-required` and `approved` branch outcomes
  - repair implementation depending on `repairs-required`
  - QA recheck with `approved`
  - release notes depending on approval
- Required artifact expectations for each automated step.
- Process graph tests proving dependencies, branch outcome IDs, skipped non-selected paths, and required artifact enforcement.

## Dependency Impact

- This is a critical foundation for subbundles 04 and 05.
- Dispatcher and E2E proof are not meaningful unless the process graph itself contains the requested loop.

## Validation Depth

- Critical foundation.
- Process graph and template/projection closure.

## Implementation Steps

1. Decide whether the deterministic calculator process should be a test-only definition builder, a template-pack process, or both.
2. Model the flow with explicit branch outcomes `repairs-required` and `approved`.
3. Ensure the repair path depends on QA first review selecting `repairs-required`.
4. Ensure the release path depends on QA recheck selecting `approved`.
5. Add artifact expectations that can be produced by the mock runtime without title ambiguity.
6. Add process-service tests that complete the graph manually with recorded artifacts to prove progression independent of AgentFramework.
7. If using the template pack, add/update projection tests so the current-module projection preserves the branch and artifact model.

## Scope Exceptions

- Do not mutate `software-delivery` into a calculator-only scenario unless explicitly chosen during implementation.
- Do not require real application build or browser proof here.

## Do Not Do

- Do not simulate QA repair outside the process graph.
- Do not use branch keys other than the mock runtime contract unless the mock runtime is changed in the same phase.
- Do not leave required artifact expectations pathless if that makes artifact matching ambiguous.

## Acceptance Checklist

- The calculator process graph includes the QA reject/repair/approve loop.
- Branch outcome keys match mock runtime outputs.
- Required artifacts can be mapped deterministically to process steps.
- Manual transition tests prove non-selected branch paths are skipped or inactive as intended.

## Proof Required

- Focused process-service graph tests for the calculator repair model.
- If template-pack backed: focused `ProcessTemplateProjectionServiceTests` and `ProcessTemplatePackLoaderTests`.
- Updated execution report rows with exact command output summaries.

## Browser Validation Logging

- N/A. Backend process definition/template modeling only.

## Progression Gate

- Subbundle 04 may proceed only after the calculator process graph is proven without AgentFramework and all branch/artifact expectations are deterministic.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add the deterministic calculator QA repair process model and tests proving graph progression, branch outcomes, and required artifact expectations. Do not touch dispatcher completion yet except where tests require existing public APIs.
```
