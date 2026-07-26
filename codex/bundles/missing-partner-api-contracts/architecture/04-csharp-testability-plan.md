# C# Testability Plan

## Characterization

- Preserve current local path import behavior and current agent/workflow route behavior.
- Prove existing internal workflow idempotency service returns one run before exposing it.
- Record existing CRM-HR interview response shape so the adjacent contract is not broken.

## Isolated Unit Tests

- Archive inspection bounds and blocked entries.
- Import-mode identity/version policy.
- External-key normalization, payload canonicalization, fingerprint conflict.
- Portable JSON Schema bounds, canonical hash, and validation outcomes.
- Workflow stable-key resolution and ambiguous/stale results.
- Workflow launch fingerprint and idempotency-key lookup.
- Agent interview target validation and readiness projection.

## Behavioral Integration Tests

- Multipart upload through protected HTTP boundary.
- Parallel external-key upsert.
- Agent execution with valid and invalid portable schema.
- Stable workflow lookup and pinned version.
- Concurrent identical workflow starts plus changed replay conflict.
- Agent recruiting attempt/review/readiness with missing and cross-scope evidence.
- OpenAPI response schema references plus runtime payload deserialization.

## Shallow-Pass Negatives

- A renamed ZIP containing `../`, symlink metadata, executable entry, oversize expansion,
  or secret field must fail before mutation.
- Same display name with different external key must not count as idempotency.
- Accepted schema metadata without validating provider output must fail the invalid-output
  test.
- Display-name workflow lookup must not satisfy the stable-key test.
- Sequential workflow retry must not stand in for concurrent claim proof.
- Automated score alone must never produce human-authorized readiness.
