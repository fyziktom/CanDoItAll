# QA gate

A CRM/HR bundle item cannot be closed until all of these are true:

- required files changed in the correct module area
- acceptance criteria are met
- appropriate automated tests pass
- UI bundles have Playwright evidence
- screenshots were semantically reviewed
- no new shell, project, or workbench regression is visible
- story traceability remains valid

The whole bundle cannot be closed until:

- all subbundles are complete,
- the critical path bundles are implemented,
- project/workbench integration is proven,
- privacy/audit controls are present,
- and final QA sign-off is written.
