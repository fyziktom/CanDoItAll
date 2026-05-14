# QA Prompt

Validate the DB/EF repair pass.

- Confirm patched queries still preserve ordering and selection semantics.
- Run targeted tests for touched modules where available.
- Run a scoped build or full solution build.
- Confirm no browser validation is required because no UI behavior changed.
- Update subbundle gate rows and raw-note closure rows with exact command outcomes.

