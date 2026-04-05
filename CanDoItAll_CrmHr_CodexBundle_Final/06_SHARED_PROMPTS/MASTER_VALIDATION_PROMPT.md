# Master validation prompt

Validate the CRM / HR implementation bundle rigorously.

## Validation order

1. Static review against architecture and acceptance criteria
2. Unit/component/integration test execution
3. Browser validation with Playwright
4. Screenshot capture and semantic review
5. Cross-module sanity checks
6. Traceability check

## Required checks

### Build and targeted tests

Run relevant test projects or filtered tests for the bundle. By the end of the full implementation, run at least:

```powershell
dotnet build CanDoItAll.slnx
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj
```

### Browser validation

Open the relevant CRM/HR routes and verify:

- shell navigation works,
- list/detail state behaves correctly,
- save flows persist after reload,
- project integration shows party context,
- no `#blazor-error-ui` is visible.

### Screenshot validation

For every UI-changing bundle:

- capture the required screenshots from the bundle folder,
- review them semantically,
- confirm there is no clipped text, layout collapse, wrong route, missing data, or hidden error overlay.

### Cross-module checks

At the end of full implementation confirm:

- parties appear in global search where safe,
- activity entries are written for important actions,
- projects show relationship context,
- workbench participant flows still work,
- AI-agent profiles bind to provider profiles,
- sensitive notes are not broadly indexed.

### Traceability

Re-run `05_TRACEABILITY/validate_bundle.py` or its equivalent evidence checks after implementation artifacts are updated.

## Failure rule

Do not accept validation that only says “tests passed”.  
UI evidence must be interpreted, not just collected.
