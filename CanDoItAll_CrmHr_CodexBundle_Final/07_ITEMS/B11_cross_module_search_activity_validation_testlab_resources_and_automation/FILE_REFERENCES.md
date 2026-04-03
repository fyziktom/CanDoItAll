# File references

## Existing files to inspect first

- `src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs`
- `src/CanDoItAll.Modules.Activity/ActivityModels.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Validation/ValidationModels.cs`
- `src/CanDoItAll.Modules.TestLab/TestLabModels.cs`
- `src/CanDoItAll.Modules.Automation/AutomationModels.cs`

## New or changed files expected

- `src/CanDoItAll.Modules.CrmHr/Integration/CrmHrSearchDocumentFactory.cs`
- `src/CanDoItAll.Modules.CrmHr/Integration/CrmHrAutomationJobs.cs`

## Test files to add or update

- `tests/CanDoItAll.Tests.Integration/CrmHrSearchAndActivityIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/CrmHrAutomationIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/CrmHrCrossModuleFlowTests.cs`
