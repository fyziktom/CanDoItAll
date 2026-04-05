# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
There is a generic connector command record + processor + tests for retry, idempotency, replay, and failure visibility, and write-side plugins execute through that boundary rather than directly from UI or workbench services.
