# 03-settings-token-ui

## Status

- `Completed`

## Objective

Add a Settings API access tab that shows API/JWT status and issues bearer tokens when JWT authorization is active.

## Covered Inputs

- N008 Settings JWT section and token creation when active.

## Prerequisites

- `01-01-api-foundation-auth-swagger` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\_Imports.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components`

## Deliverables

- New `api-access` Settings tab.
- UI fields for subject, display name, scopes, and lifetime.
- Token generation available only when auth is enabled and issuer is configured.
- UI masks token guidance and shows generated token only once.

## Dependency Impact

- Final closure depends on this subbundle for user-visible token creation.

## Validation Depth

- UI and component-test proof.

## Implementation Steps

1. Inject token service/options into Settings page.
2. Add tab key and route handling.
3. Add API access panel using existing BaseLib patterns.
4. Add token issue handler and notifications.
5. Add component/browser validation.

## Scope Exceptions

- This subbundle does not edit appsettings at runtime.
- Token revocation and persisted token registry are not included.

## Do Not Do

- Do not show signing keys.
- Do not enable token generation when JWT is disabled.
- Do not add raw `div`-only UI when existing BaseLib wrappers fit.

## Acceptance Checklist

- Tab appears in Settings.
- Disabled JWT state explains that the API is anonymous and token creation is unavailable.
- Enabled JWT state can create a token with configured issuer/audience/scopes/lifetime.
- Generated token is not persisted.

## Proof Required

- Component or browser proof for `/settings?tab=api-access`.
- Screenshot if browser launch works.
- Build web project.

## Browser Validation Logging

- Route: `/settings?tab=api-access`.
- Viewport: large-screen first, narrower width if layout changed.
- Actions: navigate, verify disabled/enabled state, generate token when configured.
- Screenshots: `evidence/settings-api-access-desktop.png` if browser proof runs.
- Review: ensure no overlapping/clipped text, generated token area is readable, and UI matches existing Settings patterns.

## Progression Gate

- Close only after Settings proof is recorded or an exact browser blocker is documented with component-level substitute proof.

## Suggested Agent Prompt

```text
Implement only the Settings API access tab and token generation UI. Use the foundation token issuer and existing BaseLib Settings patterns.
```
