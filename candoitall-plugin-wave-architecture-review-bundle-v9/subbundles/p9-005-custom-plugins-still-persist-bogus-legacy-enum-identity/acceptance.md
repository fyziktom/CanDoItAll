# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
Saving and reloading a custom provider/resource plugin requires only ConnectorPluginKey and config state; no fallback enum assignment exists in active save flows.
