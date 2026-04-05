# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
Only one canonical marker representation remains persisted. Read paths do not call ResolveLegacyJson/HydrateLegacyFields, and LoadAsync is marker-read-only.
