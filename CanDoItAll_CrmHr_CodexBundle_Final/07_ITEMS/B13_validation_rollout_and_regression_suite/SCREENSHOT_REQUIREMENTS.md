# Screenshot requirements

## Required route coverage

- `/crm-hr`
- `/projects`
- `/activity`
- `/resources`
- `/validation`
- `/test-lab`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- Component, integration, and Playwright tests exist for the final CRM/HR surface.
- Evidence folders contain screenshots plus semantic review notes.
- Fresh-db startup and seeded defaults are proven.
- The final QA gate can be executed repeatably.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
