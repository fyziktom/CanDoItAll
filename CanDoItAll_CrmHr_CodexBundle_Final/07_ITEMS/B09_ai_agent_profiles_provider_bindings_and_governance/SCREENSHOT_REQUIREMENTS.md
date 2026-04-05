# Screenshot requirements

## Required route coverage

- `/crm-hr/agents`
- `/crm-hr/directory`

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- An AI agent can be created as a party and linked to a provider profile.
- AI agent detail shows capabilities, owner, execution mode, and review state.
- The same AI agent can later be used by project integration flows.
- No duplicate provider registry is introduced.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
