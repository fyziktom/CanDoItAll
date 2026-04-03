# Screenshot requirements

## Required route coverage

- `/crm-hr/directory`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- A party can hold multiple contact methods and addresses.
- Parent-child and reporting relationships can be created and edited.
- Duplicate merge preserves related history instead of orphaning it.
- Import/export flows are available and validated in browser evidence.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
