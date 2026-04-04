
# Repository scope

Primary scope in this revision:

- `src/CanDoItAll.Modules.Workbench/*`
- `src/CanDoItAll.Modules.CrmHr/*`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- module-native responsibility touchpoints:
  - `src/CanDoItAll.Modules.Resources/*`
  - `src/CanDoItAll.Modules.Validation/*`
  - `src/CanDoItAll.Modules.TestLab/*`
- tests:
  - `tests/CanDoItAll.Tests.Integration/*`
  - `tests/CanDoItAll.Tests.Components/*`
  - relevant Playwright flows

Baseline context used for drift comparison:

- previous review bundle
- previous snapshot `CanDoItAll-canvas-drawing-refactor`

Repo inventory (current snapshot):

- `.csproj`: 42
- `.cs`: 599
- `.razor`: 337

Delta vs baseline snapshot:

- `.csproj`: +1
- `.cs`: +60
- `.razor`: +33
