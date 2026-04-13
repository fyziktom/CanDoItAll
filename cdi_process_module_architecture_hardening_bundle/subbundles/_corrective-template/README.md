# Corrective subbundle template

Use this template immediately whenever a review gate or proof step fails.

## Mandatory fields

- **Root cause**: what specifically failed and why.
- **Triggering gate or proof**: the exact gate or command that exposed the defect.
- **Scope of correction**: exact files, tests, docs, and artifacts to update.
- **Stop rule**: no downstream subbundle may begin until this corrective subbundle is complete and reviewed.
- **Validation rerun list**: every command or browser check that must be repeated.
- **Closure evidence**: links to updated files, regenerated reports, screenshots, and test results.
- **Unblock condition**: the exact condition that allows the failed gate to be rerun and then passed.

## Minimum corrective lifecycle

1. Capture the failing gate and evidence.
2. Isolate the architectural or implementation root cause.
3. Apply the smallest correction that truly closes the gap.
4. Rerun the failed validation and any dependent validations.
5. Update the traceability matrix and architecture gate log.
6. Only then unblock the downstream queue.
