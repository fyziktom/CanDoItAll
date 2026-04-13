# Persistence and concurrency strategy

## Recommended concurrency pattern

Use an **application-managed concurrency token** configured through EF as a concurrency token.

### Why this pattern
- It is provider-agnostic.
- It fits the current solution providers better than a SQL Server-specific rowversion approach.
- It allows aggregate-level lost-update detection.

### Minimum aggregates that need protection
- `ProcessDefinition`
- `ProcessDefinitionVersion`
- `ProcessRun`
- `ProcessStepRun`

Reassess whether more entities need tokens after the differential-persistence split, but these are the minimum required roots for this initiative.

## Transaction boundaries

### Definition save
Must be one explicit transaction that covers:
- aggregate load,
- validation/normalization boundary handling,
- differential graph persistence,
- concurrency token update,
- final save.

### Publish
Must be one explicit transaction that covers:
- aggregate and version checks,
- publish mutation,
- next-draft creation,
- concurrency token update,
- final save.

### Step transition
Must be one explicit transaction that covers:
- aggregate load,
- transition guard/policy evaluation,
- runtime mutation,
- journaling/improvement writes,
- concurrency token update,
- final save.

## Conflict translation

Any `DbUpdateConcurrencyException` or uniqueness conflict relevant to the operation must be translated into the module’s existing result/error contract with an explicit conflict meaning.

Do not leak raw DB exceptions as the normal control path.

## Differential persistence rule

A no-op save must be observably stable:
- same logical children keep the same IDs,
- unchanged relationships are not recreated,
- unaffected children are not touched unnecessarily.

## Search indexing note

If search indexing is outside the DB transaction, keep that explicit and honest. This bundle focuses first on DB correctness. Search-index eventual consistency can be a follow-up concern if proof shows it is unsafe.
