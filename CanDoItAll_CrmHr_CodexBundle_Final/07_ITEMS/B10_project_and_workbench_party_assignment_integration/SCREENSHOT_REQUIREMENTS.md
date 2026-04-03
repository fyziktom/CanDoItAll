# Screenshot requirements

## Required route coverage

- `/projects`
- `/projects/{ProjectId}/structure`
- `/projects/{ProjectId}/calendar`
- `/crm-hr/assignments`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- Projects show primary related parties on list or detail surfaces.
- Workbench participant creation can pick existing parties or create new ones.
- Meeting and work-item editors can assign central parties.
- Project-local-only participants remain supported.
- No existing structure flow is broken by central-party integration.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
