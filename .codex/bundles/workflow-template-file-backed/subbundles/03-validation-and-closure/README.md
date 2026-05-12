# validation-and-closure

## Status

- `Completed`

## Objective

- Validate the file-backed workflow-template migration, update durable proof, and close the raw user request.

## Covered Inputs

- R1 through R6.
- Original user request in `inputs/00-original-request.md`.

## Prerequisites

- Subbundle 01 closure gate passed.
- Subbundle 02 closure gate passed.
- No reopened loader or seeding blockers remain.

## Exact Source References

- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj

## Deliverables

- Targeted test results recorded.
- Bundle execution report updated with subbundle gate results and raw-note closure.
- Bundle completed-stage validator passes.
- Any residual risks are explicit follow-ups, not hidden as prose.

## Dependency Impact

- This is the closure phase; weak proof here means the user request is not done.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run focused unit tests for workflow template loading and seed service behavior.
2. Run targeted build/test commands for affected projects.
3. Inspect source to confirm no compiled default workflow graph builders remain.
4. Update execution report rows and raw-note closure.
5. Run completed-stage bundle validation.

## Scope Exceptions

- Browser proof remains N/A because no UI behavior changes.
- Future catalogue/sharing marketplace work remains out of scope.

## Do Not Do

- Do not close the bundle if any default template does not validate.
- Do not close the raw note as solved if compiled default workflow templates remain.
- Do not treat skipped tests as proof.

## Acceptance Checklist

- Focused tests pass.
- Targeted build passes or any failure is unrelated and documented with evidence.
- Raw user request is closed as solved or explicitly partial.
- Completed bundle validator passes.

## Proof Required

- `dotnet test` focused workflow test command.
- `dotnet build` targeted affected project command.
- `validate_bundle.py --stage completed --profile initiative`.

## Browser Validation Logging

- N/A. Backend/template storage change with no browser-visible behavior.

## Progression Gate

- Bundle can close only after tests/builds and completed-stage validation agree with the implementation status.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Run the focused tests/builds, inspect that compiled default templates are gone, update the bundle proof rows, close the raw request, and run the completed-stage validator.
```
