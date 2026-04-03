# Screenshot requirements

## Required route coverage

- `/crm-hr/crm`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- Opportunities can move across stages and stage history is recorded.
- Won opportunity conversion creates or links a project and keeps party context.
- Lost opportunities keep loss reason and are still historically visible.
- Pipeline UI is readable and validated with screenshots.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
