# Corrective subbundle template

Use this template immediately whenever a review gate or validation step fails.

## Mandatory fields
- **Root cause**: what specifically failed and why.
- **Scope of correction**: exact files, tests, scripts, and docs to update.
- **Stop rule**: no downstream subbundle may begin until this corrective subbundle is complete and reviewed.
- **Validation rerun list**: all commands or checks that must be repeated.
- **Closure evidence**: links to updated files, regenerated reports, and test results.

## Minimum corrective lifecycle
1. Capture the failing gate and evidence.
2. Isolate the architectural or implementation cause.
3. Apply the smallest correction that truly closes the gap.
4. Rerun the failed validation and any dependent validations.
5. Update the traceability matrix and architecture review memo.
6. Only then unblock the downstream queue.
