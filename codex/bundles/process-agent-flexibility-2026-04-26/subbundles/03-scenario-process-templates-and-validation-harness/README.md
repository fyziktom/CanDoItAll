# Scenario process templates and validation harness

## Status

- `Completed`

## Objective

Add a default non-coding process template suitable for business-plan validation and add deterministic tests that load/project it before any real-agent run.

## Covered Inputs

- `N005`: Default non-coding processes are required.
- `N006`: Atomic artifact-shape checks must precede handoff tests.

## Prerequisites

- Subbundle 02 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\Templates\Processes\seed-catalog\baseline-scenarios.json`
- `C:\repositories\CanDoItAll\Templates\Processes\shared\roles`
- `C:\repositories\CanDoItAll\Templates\Processes\shared\artifacts`
- `C:\repositories\CanDoItAll\Templates\Processes\shared\prompts`
- `C:\repositories\CanDoItAll\Templates\Processes\processes`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- A business-plan process template with business strategy, finance, marketing, and final synthesis/review stages.
- Required artifacts that prove handoff shape, for example strategy brief, finance assumptions, marketing plan, and business plan package.
- Baseline scenario entry for deterministic seeding/testing.
- Template pack load/projection tests.

## Dependency Impact

- Subbundle 04 depends on this template for PostgreSQL-backed process validation and real-agent scenario selection.

## Validation Depth

- `Process-template foundation`

## Implementation Steps

1. Add process template JSON/markdown and flow diagrams under `Templates/Processes/processes`.
2. Add any shared role/artifact/prompt resources needed by the process.
3. Register the process in `manifest.json`.
4. Add a baseline scenario that uses business-plan trigger input.
5. Add focused tests that load and project the template.

## Scope Exceptions

- This subbundle does not need to create UI pages.

## Do Not Do

- Do not hard-code business-plan process definitions in C#.
- Do not modify unrelated existing templates except where shared resources need new entries.
- Do not create a coding-specific process as the only validation scenario.

## Acceptance Checklist

- Template pack loader includes the new process.
- Projection creates an import envelope without missing resources.
- The process has handoff artifacts between business, finance, marketing, and synthesis/review.
- Baseline scenario can be discovered by seed code.

## Proof Required

- Targeted template-pack load/projection tests.
- JSON validity through test execution.

## Completion Proof

- Added `business-plan-development` process with strategy, finance, marketing, integrated review, and routed approved/blocked terminal paths.
- Added baseline business-plan scenario and template projection/parity tests.
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj --no-restore` passed: 27 tests.

## Browser Validation Logging

- N/A. This subbundle affects template resources and tests.

## Progression Gate

- PostgreSQL validation may proceed only after template loading/projection passes.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add a non-coding business-plan process template and deterministic load/projection tests.
```
