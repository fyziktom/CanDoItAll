# Structured Input

## Objective

Isolate the pre-execution guard and upstream artifact materialization path from the main dispatch orchestration while preserving behavior.

## Must preserve

- Candidate header ordering and filtering.
- Candidate hydration and route construction from the previous bundle.
- Database requirement block behavior and transition fields.
- Missing upstream artifact detection.
- Downstream blocking transition semantics.
- Materialization fingerprint shape and duplicate prevention.
- Process journal event type, correlation id, JSON details, and descriptions.
- Rerun request fields and manual recovery directive semantics.
- Logs and return semantics.
- No Process Core, no production driver API, no UI proof.

## Next safe seam

`TryRequestMissingUpstreamArtifactMaterializationAsync` and adjacent database/pre-execution guard helpers.
