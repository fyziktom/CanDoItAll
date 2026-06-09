# SB012 Proof Manifest

Status: Passed.

## Scope

Gate D covers the source-backed process template catalog and UI launch affordance map for `P04: Process UI route and template catalog inventory`.

No production UI, API, template, runtime, driver, or process mutation code was changed in SB010-SB012. The only source change for this gate is the integration test coverage in `repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs`.

## Command Transcripts

- `bundle://proof/SB010/transcripts/process-ui-route-template-inventory.txt`
- `bundle://proof/SB011/transcripts/template-catalog-test-source-assertions.txt`
- `bundle://proof/SB012/transcripts/focused-template-catalog-visibility-test.txt`
- `bundle://proof/SB012/transcripts/anti-stub-audit-template-catalog-test.txt`
- `bundle://proof/SB012/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB012/transcripts/changed-file-hashes.txt`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor` maps `/processes` to `ProcessWorkspace`.
- `repo://src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor` maps `/projects/{ProjectId:guid}/processes` to `ProcessWorkspace ProjectId`.
- `repo://src/CanDoItAll.Modules.Processes/Pages/LiveProcessesPage.razor` maps `/processes/live` and `/projects/{ProjectId:guid}/processes/live`.
- `repo://src/CanDoItAll.Web/Composition/ShellNavigation.cs` exposes `/processes` and `/processes/live`.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` exposes stable template-launch test ids.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.TemplateLibrary.cs` imports projected template envelopes through `ProcessesService.ImportAsync`.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeOperations.cs` starts runs through `ProcessesService.StartRunAsync`.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs` creates and executes launch plans, then navigates to the project process workspace with process/run query ids.
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs` maps the project-structure process-start route.
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs` maps template list, baseline scenario, live-run profile, detail, envelope, and mermaid routes.
- `repo://Templates/Processes/manifest.json`, `baseline-scenarios.json`, and `live-run-profiles.json` register the required software, Blazor app, business plan, baseline, and fresh-run entries.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ApplicationStartupIntegrationTests"` passed with 2 tests:

- `Web_app_startup_SB009_INV_001_starts_current_composition_with_process_module_registered`
- `Process_template_catalog_SB012_INV_001_exposes_required_templates_to_api_and_ui_launch_surfaces`

The SB012 test starts a real `WebApplication`, maps production app/API/component routes, sends HTTP requests to the started server, and asserts exact catalog/API/UI visibility for:

- `software-delivery`
- `blazor-app-delivery`
- `business-plan-development`
- `baseline-software-delivery`
- `baseline-business-plan-development`
- `baseline-blazor-wasm-pwa-app`
- `generic-blazor-wasm-pwa-app`

## Anti-Stub And Adversarial Proof

- The anti-stub audit confirms the test uses `WebApplication.CreateBuilder`, `builder.Build`, production API/component mappings, `app.StartAsync`, and an HTTP client against the started server.
- The test rejects missing/duplicate required templates with `Assert.Single`.
- The test rejects shallow projected envelopes with `Assert.NotEmpty(envelope.Definition.Steps)`.
- The test verifies the stable projection format and source warning for each required process template.
- The test verifies shell navigation and the process template library service, so API-only visibility cannot satisfy the gate.

## Forbidden Drift

`bundle://proof/SB012/transcripts/forbidden-drift-scan.txt` confirms:

- no transient bundle path dependency was added to the integration test;
- no generic runtime driver host, registry, selector, or driver DI registration was added by this gate;
- no production UI/API/template catalog files were changed, so browser validation remains deferred to SB013-SB015.

## Changed-File Hashes

See `bundle://proof/SB012/transcripts/changed-file-hashes.txt`.

## Downstream Dependency Check

SB013-SB015 can rely on the mapped large-screen routes and template launch affordances. SB016-SB018 can rely on the project-structure launch route inventory. Runtime and driver phases remain untouched by this gate.
