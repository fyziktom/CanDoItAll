# Evidence map

- **F1 / P10-001**
  - `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:135`
    - `LoadAsync(...)` calls `RetireLegacyProjectionRowsAsync(...)`.
  - `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:167-175`
    - `LoadAsync(...)` deletes stale layout rows and saves changes.
  - `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:361-388`
    - helper deletes stale system-managed rows and saves changes.

- **F2 / P10-003**
  - `candoitall-plugin-wave-architecture-review-bundle-v9/scripts/gate_check_phase9.py`
    - checks only the old normalization symbol shapes and misses the actual write-on-read behavior.

- **F3 / P10-003**
  - `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:294-392`
    - current test proves no binding/reference backfill for one legacy case,
    - but does not cover stale projection row or stale layout deletion.

- **F4 / P10-004**
  - `tests/CanDoItAll.Tests.Components/SettingsPageProvidersTests.cs`
  - `tests/CanDoItAll.Tests.Components/ResourcesPageTests.cs`
  - `tests/CanDoItAll.Tests.Integration/ConnectorPluginIntegrationTests.cs`
  - `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs:144-214`
    - current proof uses only built-in manifests.

- **Advisory compatibility seams**
  - `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:77-82`
  - `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:390-408`
  - `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs:391-395`
- **False-green baseline proof**
  - `inventories/06-phase9-gate-false-green-baseline.txt`
    - the old phase9 gate still reports no hard-gate failures on the current repo.

