# Execution Driven Architecture Repairs And Refactor

## Status

- `Ready`

## Objective

- Convert the real weaknesses exposed by the serious run into code, template, and architecture repairs, including splitting oversized files when the evidence shows current composition is blocking correctness or maintainability.

## Covered Inputs

- `N007`
- `N008`
- `N009`

## Prerequisites

- `subbundles/04-live-agent-delivery-run-and-observation` closed with observed weaknesses recorded in the execution report

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAssemblyService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.ScenarioSeeder\AgentShowcaseCalculatorSeeder.Workflow.cs`
- `C:\repositories\CanDoItAll\units-converter-agent-delivery-bundle\reviews\01-execution-report.md`

## Deliverables

- Repairs for the concrete defects exposed during the serious run.
- Template or orchestration updates driven by observed missing steps or weak handoffs.
- Architectural refactors or file splits where the live evidence shows current files are too entangled.
- Updated tests and execution-report entries tied to each repaired weakness.

## Dependency Impact

- The final rerun depends on this phase to eliminate the real causes of runtime weakness rather than masking symptoms. Weak proof here invalidates the closure audit.

## Validation Depth

- `Process-critical closure`
- `UI, component-test, and browser-proof` when affected surfaces are browser-visible

## Implementation Steps

1. Translate live-run findings into an explicit repair list in the execution report.
2. Implement the smallest correct fixes for each confirmed weakness.
3. Split oversized files only when the evidence shows real boundary or maintainability pressure.
4. Add or update regression tests for each repaired weakness.
5. Revalidate affected UI or workbench surfaces with Playwright when the repairs change visible behavior.

## Scope Exceptions

- Do not invent speculative refactors unrelated to live-run evidence.

## Do Not Do

- Do not refactor large files just because they are large; tie each split to a concrete observed weakness or boundary problem.
- Do not leave repair decisions only in chat; record them in the execution report.

## Acceptance Checklist

- Every confirmed weakness from subbundle `04` has a corresponding repair, explicit defer decision, or reopened note.
- Regression tests cover repaired defects.
- Oversized-file splits, if any, improve boundaries instead of adding indirection for its own sake.

## Proof Required

- Code diffs tied to observed failures.
- Targeted regression tests.
- Updated execution-report mapping from observed weakness to repair.
- Browser proof for any repaired visible surface.

## Browser Validation Logging

- Target routes: only the routes affected by the observed and repaired weaknesses
- Required viewports: `1600x900` plus smaller layout follow-up when the repaired surface is responsive
- Required Playwright MCP actions: reproduce the pre-repair failure when possible, verify the repaired behavior, capture screenshots
- Expected evidence paths: execution-report entries tied to each repaired visible weakness
- Screenshot review questions: does the repaired surface now behave correctly, and did the repair introduce a new visual regression

## Progression Gate

- Do not start subbundle `06` until each confirmed live-run weakness is either repaired with proof or explicitly reopened as a blocking issue.

## Suggested Agent Prompt

```text
Implement only subbundle 05. Convert the serious-run findings into concrete code, template, and architecture repairs. Split oversized files only where live evidence proves the current boundaries are too entangled, add regression coverage, and record the repair mapping in the execution report before advancing.
```
