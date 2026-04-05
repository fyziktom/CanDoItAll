# Forbidden patterns

The following patterns must not remain after the refactor for this item:

- any implementation shape that still reproduces the core problem described in `P7-008`
- ADR-only closure without code and tests
- hidden reintroduction through metadata, helper caches, or UI-only layers

## Local evidence to remove

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-748
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1038-1133
- tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425
