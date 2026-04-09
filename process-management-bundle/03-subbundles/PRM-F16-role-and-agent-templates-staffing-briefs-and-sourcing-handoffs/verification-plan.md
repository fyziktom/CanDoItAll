# Verification plan — PRM-F16

## Expected verification outcomes

- Role / agent templates can be created and reused.
- Process roles can reference templates and retain snapshots.
- Process-linked staffing briefs can be opened without losing context.
- Eligible pool / fallback metadata survives persistence and re-open.

## Automated tests

- Unit tests for template validation, snapshotting, and fallback-rule invariants
- Integration tests for CRM-HR / Processes template linking and staffing-brief flows
- Component tests for template picker and staffing-status UI
- Playwright tests for create-template -> use-template -> open-staffing-brief happy path

## Manual verification checklist

1. Create a reusable template in CRM-HR.
2. Open the process designer and select the template for a role.
3. Publish the process and confirm the snapshot is visible.
4. Trigger an unresolved staffing gap and open the linked staffing flow.
5. Re-open the process/run and verify template metadata remains stable.

## Regression concerns to watch

- Duplicate staffing catalogs emerging inside Processes
- Template edits rewriting old process versions or runs
- AI role templates bypassing CRM-HR identity ownership
- SQLite-only assumptions that break PostgreSQL or vice versa
