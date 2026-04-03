# Screenshot requirements

## Required route coverage

- `/crm-hr`
- `/crm-hr/directory`
- `/crm-hr/crm`
- `/crm-hr/workforce`
- `/crm-hr/recruiting`
- `/crm-hr/agents`
- `/crm-hr/assignments`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- Navigating to `/crm-hr` and the child routes works without shell errors.
- The Directory page can create and edit a basic party record.
- All CRM/HR pages use BaseLib-first layouts and do not import canvas libraries.
- Playwright smoke flow proves navigation, save, and reload persistence.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
