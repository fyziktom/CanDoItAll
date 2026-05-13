# Non-Goals And Scope Boundaries

## Non-Goals Before Foundation Review

- Do not create `CanDoItAll.Modules.Plugins`.
- Do not add plugin catalog APIs.
- Do not add plugin settings pages.
- Do not implement sample plugins.
- Do not introduce remote package installation.

## Non-Goals For MVP

- No arbitrary remote plugin code loading.
- No untrusted plugin Razor components.
- No OAuth2 SaaS provider implementation.
- No app-store/shop business logic.
- No plugin marketplace payments or licensing enforcement.
- No public package signing service implementation.
- No script execution plugin.

## Explicitly Allowed During MVP

- Static/bundled plugin registrations.
- Manifest/catalog/install-state model.
- Installed/enabled toggles.
- Schema-driven settings rendering.
- Bundled renderer components from trusted application assemblies.
- Plugin workflow executor bridge.
- Remote catalog/package contract design without executable-code loading.
