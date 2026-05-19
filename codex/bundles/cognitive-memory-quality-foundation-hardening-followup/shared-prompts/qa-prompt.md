# QA Prompt

```text
Validate the current subbundle against its acceptance checklist and proof rules.

Do not accept intent as proof. Use the listed commands, tests, and artifacts.

For this bundle, pay special attention to repeat execution and policy safety. A first-run happy path is not enough. Check that tests fail against the old behavior when feasible, pass after the implementation, and cover idempotency, dry runs, failure status, unsupported modes, redaction, aggregate provenance, and reference expansion.

Record:

- command or browser route
- result
- artifact path
- unresolved risk or blocker
```
