# Core API Governance

## Rules

- Every new public Core type/member requires owner classification.
- Every public Core change must update the architecture API snapshot test.
- Every Core addition must pass forbidden dependency scans.
- Core should prefer records/enums/static pure rules over services.
- No side-effect vocabulary should imply ownership of execution.

## Candidate Future Core Families

Allowed candidates:
- evidence descriptor normalization,
- diagnostic reason descriptors,
- deterministic matching,
- deterministic eligibility,
- read-only decision explanations.

Denied candidates:
- any runtime service,
- any EF query,
- any storage/workspace/file access,
- any finalizer application,
- any execution/retry/repair action,
- any driver runtime.
