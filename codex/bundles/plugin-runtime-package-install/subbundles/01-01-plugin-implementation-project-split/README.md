# 01-plugin-implementation-project-split

## Status

- `Completed`

## Objective

Move concrete Docker, Gmail, Office365, and shared email plugin implementation code out of `CanDoItAll.Modules.Plugins` into projects under `src/plugins`, then wire those projects from composition while preserving existing public namespaces and behavior.

## Covered Inputs

- `N002`, `N007`, `N008`
- Requirements: `R002`, `R003`, `R004`, partial `R015`

## Prerequisites

- Bundle preparation gate passed.
- Existing current-state source references are still present before the move.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Services\PluginsModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerBundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerHostToolService.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerPluginConstants.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowSettings.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Email\EmailPluginModels.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Email\EmailWorkflowPayloadResolver.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailApiClient.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailBundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailPluginConstants.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365BundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365GraphClient.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365PluginConstants.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAll.Composition.csproj`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`

## Deliverables

- New `src/plugins` project area with Docker, Email, Gmail, and Office365 projects.
- Plugin implementation files moved into the new projects.
- Composition project references the new plugin projects.
- Composition calls explicit bundled plugin registration extension methods.
- `CanDoItAll.Modules.Plugins` no longer directly registers concrete plugin implementation types.

## Dependency Impact

- SB02 depends on this split because runtime package scanning should be added to a plugin runtime module that is not already hard-wired to every concrete plugin implementation.
- SB03 depends on SB02 and indirectly on this project boundary.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create the new plugin project folders and project files under `src/plugins`.
2. Move Docker, Email, Gmail, and Office365 implementation files into those projects.
3. Add service registration extension methods in plugin projects.
4. Remove concrete plugin registrations from `AddPluginsModule`.
5. Add plugin project references to composition and solution.
6. Build enough of the web/composition path to prove project references and DI registrations compile.

## Scope Exceptions

- Do not change plugin ids, package ids, executor ids, OAuth scopes, or grant semantics in this phase.
- Runtime package loading is SB02.

## Do Not Do

- Do not rewrite plugin descriptors or workflow executor behavior.
- Do not introduce a second plugin abstraction model.
- Do not weaken existing grant checks.

## Acceptance Checklist

- `src/plugins` contains plugin implementation projects.
- `CanDoItAll.slnx` contains those projects.
- `CanDoItAll.Modules.Plugins` compiles without concrete Docker/Gmail/Office365 registrations.
- Docker, Gmail, and Office365 remain registered through composition.

## Proof Required

- Targeted build of `src/CanDoItAll.Web/CanDoItAll.Web.csproj` or equivalent isolated output build.
- Existing plugin catalog/API tests still find bundled plugin descriptors.
- Execution report SB01 gate row updated.

## Browser Validation Logging

- `N/A` for this subbundle. It changes project boundaries and DI registration, not visible UI.

## Progression Gate

- Downstream work may continue only after the build passes and the catalog still exposes Docker, Gmail, and Office365 through composition.

## Suggested Agent Prompt

```text
Implement SB01 only. Move concrete plugin implementations into src/plugins projects, keep existing behavior, wire composition, build, update execution report, and stop if the moved projects cannot preserve existing catalog registration.
```
