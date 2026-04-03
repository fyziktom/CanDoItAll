# Screenshot requirements

## Required route coverage

- no direct user route in this bundle

## Required proof set

- initial route load
- primary create/edit/assign/convert flow for this bundle
- persisted state after save
- reload or navigation-return proof
- cross-module proof if this bundle changes another surface

## Bundle-specific screenshot expectations

- Fresh app startup creates the CRM/HR tables without manual intervention.
- The solution builds after module registration changes.
- Integration tests prove schema creation and at least one round-trip save/load for the Party aggregate.
- No existing module startup path is broken by the new module registration.

## Review notes must mention

- what route is visible
- what entity or workflow is visible
- whether labels and actions are readable
- whether the expected CRM/HR context is shown
- whether any defect is visible
