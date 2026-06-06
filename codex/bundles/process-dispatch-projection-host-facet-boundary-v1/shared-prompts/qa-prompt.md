# QA prompt

Review the implementation for behavior preservation. Specifically verify:

- source-family order is unchanged;
- no coordinator depends on the broad host after migration gates;
- no coordinator depends directly on `ProcessRunAutomationDispatchService` unless explicitly allowed by a temporary exception;
- candidate state mutation is centralized;
- side effects are named and explicit;
- all projection tests pass;
- no Core/driver/UI drift exists.
