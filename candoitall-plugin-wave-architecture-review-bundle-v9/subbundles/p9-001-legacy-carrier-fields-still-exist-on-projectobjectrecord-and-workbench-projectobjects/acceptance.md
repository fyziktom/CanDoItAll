# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
ProjectObjectRecord no longer exposes the legacy binding fields, Workbench_ProjectObjects no longer stores them, and no active code path reads or writes them.
