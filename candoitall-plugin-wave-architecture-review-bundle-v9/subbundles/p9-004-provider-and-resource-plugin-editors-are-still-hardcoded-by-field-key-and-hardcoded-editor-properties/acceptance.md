# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
A test plugin that declares previously unknown fields of types Text / Url / Number / Boolean / Json / SecretReference can be rendered, edited, saved, reloaded, and validated without changing ResourcesPage.razor or SettingsPage.razor.
