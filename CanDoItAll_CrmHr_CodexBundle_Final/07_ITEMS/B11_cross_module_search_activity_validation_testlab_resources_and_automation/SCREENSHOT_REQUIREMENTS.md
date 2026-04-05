# Screenshot requirements

## Required route coverage

- `/activity`
- `/resources`
- `/validation`
- `/test-lab`
- `/automation`
- `/crm-hr`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- CRM/HR entities appear in global search where safe.
- Major CRM/HR actions appear in Activity.
- Resources/Validation/Test Lab can reference responsible parties.
- Automation workspace can show CRM/HR reminder jobs or equivalent status.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
