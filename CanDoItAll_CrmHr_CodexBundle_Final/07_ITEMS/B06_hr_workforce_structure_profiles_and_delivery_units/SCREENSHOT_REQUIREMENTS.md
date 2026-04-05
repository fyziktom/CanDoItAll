# Screenshot requirements

## Required route coverage

- `/crm-hr/workforce`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- A person can have a workforce profile without losing CRM identity continuity.
- A delivery unit can be represented as a party with workforce semantics.
- Workforce detail shows home unit and manager relationships clearly.
- Component and Playwright tests prove profile editing.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
