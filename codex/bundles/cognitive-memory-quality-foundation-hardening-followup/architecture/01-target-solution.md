# Target Solution

## Target Shape

Keep the quality foundation inside `CanDoItAll.Modules.CognitiveMemory`, but split implementation responsibilities so each service can be reviewed and tested independently:

- Diagnostics: counts, warnings, shallow-run detection, and masked logging.
- Cluster planning: key generation, durable cluster upsert, source/member planning, and metrics.
- Dream orchestration: mode policy resolution, run lifecycle, transaction boundaries, dry-run behavior, and validation orchestration.
- Aggregate synthesis/provenance: candidate text generation, claim/source-map creation, validation inputs, and aggregate application.
- Recall synthesis/reference resolution: consumer-facing brief generation, per-statement references, and access/redaction enforcement.
- Shared support loading/text utilities: internal helpers only; no public surface unless needed by tests or real boundaries.

## Boundary Rules

- UI components do not own cognitive-memory business rules.
- EF entities remain infrastructure persistence records; public contracts remain typed DTOs.
- Dream mode selection must be represented by typed mode policies or named internal strategy objects, not switch fallthrough that treats every mode as eligible.
- Transaction and idempotency semantics belong in application services, not tests or callers.
- Redaction must be applied before generated aggregate or synthesis text leaves the service boundary.

## Persistence Direction

- Existing cluster records should be upserted by `(ProjectId, ClusterHash)`.
- Returned cluster plans must use the persisted ID when a cluster already exists.
- Dream runs should use explicit lifecycle states and failure details already present on `CognitiveMemoryDreamRunRecord`.
- Follow-up migrations should be added only when a real schema change is required; otherwise use service logic and tests.

## Testing Direction

- Add tests that fail against the current code first.
- Prefer integration tests for FK/idempotency/transaction behavior.
- Prefer unit tests for mode policies, validation decisions, redaction, and synthesis text behavior.
- Keep deterministic synthesis fakes so semantic provider availability is not required for CI.
