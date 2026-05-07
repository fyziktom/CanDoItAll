# 08-reclosure-proof

## Status

- `Completed`

## Objective

Re-run the corrected proof loop, update workbook coverage, and close the raw feedback note by note.

## Covered Inputs

- All original bundle inputs plus the 2026-05-05 correction.

## Prerequisites

- Subbundles 05-07 completed or explicitly blocked with a concrete reason.

## Exact Source References

- `C:\repositories\CanDoItAll\.codex\bundles\api-swagger-jwt-dev-control-plane\requirements\user-stories.xlsx`
- `C:\repositories\CanDoItAll\.codex\bundles\api-swagger-jwt-dev-control-plane\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web`

## Deliverables

- Updated `requirements/user-stories.xlsx`.
- Updated execution report, browser/API proof, subbundle gates, raw-note closure, and residual risks.
- Completed-stage validator pass.

## Dependency Impact

- This is the final correction gate; weak proof here reopens the relevant implementation subbundle.

## Validation Depth

- Final closure.

## Implementation Steps

1. Regenerate or update the workbook with corrected route and command coverage.
2. Run targeted integration tests and web build.
3. Run completed-stage bundle validator.
4. Update raw-note closure and residual risks.

## Do Not Do

- Do not mark partially implemented commands as solved.
- Do not hide missing proof inside residual risks.

## Acceptance Checklist

- Workbook and execution report match the shipped API.
- The corrected raw notes are closed note by note.
- Completed-stage validator passes.

## Proof Required

- Web build.
- Targeted integration tests.
- Bundle validator completed stage.

## Proof Captured

- Regenerated `requirements/user-stories.xlsx` with `User Stories`, `API Commands`, and `Summary` sheets.
- Verified workbook sheet list and formula-error scan.
- Ran `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal`; passed with existing package vulnerability warnings.
- Ran `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ApiIntegrationTests|FullyQualifiedName~ProjectStructureAgentApiIntegrationTests" -v:minimal`; 9 tests passed.
- Ran completed-stage bundle validator after marking all correction subbundles complete; passed.

## Browser Validation Logging

- Settings route smoke or browser proof is required only if Settings UI changed materially.

## Progression Gate

- The bundle can close only after completed-stage validator and proof commands pass.

## Suggested Agent Prompt

```text
Run the corrected proof loop, update workbook/report closure, and verify the bundle can close honestly.
```
