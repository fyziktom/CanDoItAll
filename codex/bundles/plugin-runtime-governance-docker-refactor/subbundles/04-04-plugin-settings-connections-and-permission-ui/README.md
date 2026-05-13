# SB04 Plugin Settings Connections And Permission UI

## Status

- `Ready`

## Objective

- Turn the plugins page and API into a real settings surface where users can install, enable, configure connections, and explicitly grant or revoke plugin capabilities.

## Success Criteria

- Users can inspect manifest-declared capabilities separately from granted permissions.
- Grant and connection changes persist, audit, and handle concurrency.
- Browser proof shows permission controls and denied/default states are understandable and functional.

## Covered Inputs

- `N009`: user explicitly controls files, PowerShell, and tools in plugin settings.
- `N006`: Docker plugin needs host-tool permissions visible to users.
- Requirements `R005`, `R016`, `R017`, `R018`, and `R019`.

## Prerequisites

- SB02 grant model is complete.
- SB03 host-tool capability contracts are stable enough to display recipe grants.
- Existing UI component patterns and Radzen/component-library conventions are identified before editing.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\PluginsApi.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Persistence\PluginInstallationRecord.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginConnectionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\PluginSecretBroker.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginSecretBrokerIntegrationTests.cs

## Deliverables

- Plugin settings application service for catalog state, connection settings, grant state, and health state.
- API endpoints or command handlers for grant changes, connection CRUD, health checks, install, enable, and disable actions.
- UI that shows requested vs granted capabilities, risk labels, grant status, connection status, actor/timestamp, and reason when unavailable.
- UI controls for granting/revoking file access, host-tool recipes, PowerShell recipes, Docker recipes, HTTP/network, storage, and secrets when declared.
- Browser validation for plugin settings and permission management.

## Dependency Impact

- SB05 workflow editor diagnostics depend on the same user-visible grant state.
- SB06 Docker sample depends on users being able to grant Docker recipe access.
- SB07 relies on this phase for connection/grant persistence patterns and audit metadata.

## Validation Depth

- `Critical UI and API foundation`

## Implementation Steps

1. Add or extend application services for plugin settings and grant management.
2. Add connection persistence/service behavior if not already completed by SB02.
3. Add API endpoints that derive actor identity from trusted context, not request body.
4. Update plugin settings UI using existing component patterns.
5. Add validation states for missing capability declaration, missing grant, revoked grant, missing connection, and unavailable recipe.
6. Add integration tests for grant/connection API behavior.
7. Add browser validation for settings route, grant toggles, denied state, and concurrency/error handling where visible.
8. Update execution report with commands, browser artifacts, and SB04 gate result.

## Scope Exceptions

- No workflow editor integration in this subbundle beyond reusable service state.
- No Docker plugin workflow execution in this subbundle.

## Do Not Do

- Do not put business rules directly in Razor component lifecycle methods.
- Do not trust actor strings from client DTOs.
- Do not use UI-only state as the source of permission truth.
- Do not hide dangerous grants behind vague labels.

## Acceptance Checklist

- Installed/enabled plugin displays denied grants by default.
- Grant changes persist and audit with trusted actor and timestamp.
- Connection settings validate schema and do not expose secret values.
- UI shows risk labels for host commands, PowerShell, Docker, files, HTTP, storage, and secrets.
- Browser screenshots and DOM assertions prove the controls work.

## Proof Required

- API/integration test command and result.
- Component/unit test command and result where available.
- Browser screenshot of plugin settings default denied state.
- Browser screenshot after a grant change.
- Execution report browser analytics row with route, viewport, actions, assertions, screenshots, and visual review.

## Browser Validation Logging

- Route: `/plugins` or the concrete plugin settings route implemented by this subbundle.
- Viewports: maximized large-screen pass and narrower-width follow-up when layout is affected.
- Playwright actions: navigate, inspect plugin card/settings, change a grant, save, reload, assert persisted state, assert risk labels.
- Screenshots: one default-denied screenshot and one changed-grant screenshot.
- Review questions: text must fit controls, risk labels must be visible, dangerous grants must not appear as harmless toggles, and UI must not imply install equals permission.

## Progression Gate

- SB05 may rely on permission state only after UI/API tests and browser proof show grants are persisted and accurately displayed.

## Suggested Agent Prompt

```text
Implement SB04 only.
Add plugin settings, connection, and permission UI/API on top of the SB02 grant model and SB03 recipe contracts. Capture browser proof for denied-by-default and grant-change flows. Do not implement workflow bridge or Docker plugin behavior.
```
