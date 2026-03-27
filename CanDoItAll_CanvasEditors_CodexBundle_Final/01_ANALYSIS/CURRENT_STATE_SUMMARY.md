
# Current state summary

This document captures the verified codebase state that matters for the requested canvas-editor improvements.

## Repository snapshot

- Repository root used for analysis: `CanDoItAll-main`
- Solution entry point: `CanDoItAll.slnx`
- .NET SDK pinned in `global.json`: `10.0.200`
- Relevant test projects already exist for unit, component, integration, and Playwright validation.

## Key verified findings

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs` currently defines a relatively small `ProjectObjectType` set. Most requested new node families do **not** yet exist as first-class types.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` and `ProjectWorkbenchSchemaInitializer.cs` currently expose only a narrow set of per-node fields such as subtype, media, progress, marker, priority, and dates. There is no existing `MetadataJson` field or equivalent rich payload for node-specific data.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs` contains a hard-coded project structure create catalog. The current project structure create experience is inspector-driven and accordion-oriented, not a floating toolbox.
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs` currently biases child placement to the right. It does **not** satisfy the requested side-aware placement behavior.
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs` already contains a components catalog action using `SubmenuLayout = "toolbox-panel"`.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js` and `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css` already implement a toolbox-style panel with search, grouped sections, and preview behavior. This is the current wrong or incomplete Prompt Factory toolbox UX that should be redesigned rather than blindly duplicated.
- Prompt Factory also already has floating inspector patterns via `FloatingInspectorHost` and related canvas components. This is the strongest existing reuse point for the requested floating tool-window behavior.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` already contains reusable resource kinds such as `Repository`, `PowerShellScript`, `DockerCompose`, `Ssh`, `SecretLink`, and `PromptLink`.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` and `ProviderExecution.cs` already contain provider abstractions including `OpenAi`, `OllamaLocal`, and `OllamaRemote`. These should be reused for transcript and other LLM-backed actions.
- `tools/CanDoItAll.Manager/LaunchProfileSettingsResolver.cs` already parses `launchSettings.json` and is the correct reuse point for .NET runtime nodes.
- `tools/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs` already contains runtime command helpers relevant to script, watch, release, migration, and terminal-related items.
- There is no obvious existing CRM or people module. The participant requirement should stay intentionally lightweight.

## Existing tests worth extending

- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePlacementPolicyTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectCalendarPageTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryCatalogToolboxTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/PromptFactoryServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Unit/LaunchProfileSettingsResolverTests.cs`
- `tests/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Playwright/PromptLibraryVerificationTests.cs`

## High-value reuse opportunities

- Reuse `Resources` for repositories, SSH, Docker, scripts, secret links, and prompt links.
- Reuse `Workspace` provider abstractions for OpenAI and Ollama-backed transcript actions.
- Reuse `LaunchProfileSettingsResolver` and `WorkspaceRuntimeProcessTools` for .NET runtime nodes.
- Reuse `FloatingInspectorHost` and the general canvas workbench shell to build a shared floating toolbox host.

## Confirmed problem areas to address explicitly

- The data model is too narrow for the requested node variety unless a rich metadata strategy is introduced.
- The project structure catalog is not yet a floating, searchable toolbox.
- Prompt Factory has an existing but wrong or incomplete toolbox UI and should be intentionally redesigned.
- The intermittent “44 nodes” component-add bug needs dedicated instrumentation and regression testing.
- Many requested outcomes are visual and therefore require screenshot-based validation, not only passing tests.
