# Screenshot requirements

## Required route coverage

- `/crm-hr`
- `/crm-hr/directory`
- `/crm-hr/workforce`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- Confidential notes are stored and displayed separately from broad operational notes.
- Sensitive content is not indexed into global search.
- Audit trail entries exist for important lifecycle and data changes.
- Archive/reactivate flows preserve history.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
