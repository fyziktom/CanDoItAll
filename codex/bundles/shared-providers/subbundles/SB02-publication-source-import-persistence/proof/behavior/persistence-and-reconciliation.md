# SB02 persistence and reconciliation behavior

## Transition and lifecycle proof

| Behavior | Positive proof | Negative proof |
| --- | --- | --- |
| Publication identity | creation defaults unpublished; publish/unpublish is idempotent and preserves `PublicId` | empty public ID and `PublicId == ProviderProfileId` rejected; database check reinforces it |
| Source identity | first authoritative catalog pins service identity and ETag; source edit invalidates the conditional cache | different service identity enters mismatch without replacing the trusted pin |
| Source failure | source status records a transient failure while trusted identity/ETag and imports remain intact | transient failure cannot invoke authoritative missing/unpublished transitions |
| Import creation | selection creates one import and one linked profile; repeated reconciliation is idempotent | unique `(SourceId, RemotePublicationId)` and unique profile relationship reject duplicates |
| Local intent | remote refresh updates remote-owned fields while preserving profile ID, alias, and enabled intent | authoritative catalog data cannot overwrite the local alias/enabled fields |
| Authoritative absence | missing/unpublished state preserves the import and local profile; reappearance reuses both identities | no hard deletion or new profile ID is permitted |
| Source materialization | source URI/secret-reference edit updates every linked effective profile cache atomically | stale source token returns a typed conflict and rolls back the mutation |
| Invocation metadata | begin is request-id idempotent; completion is truthful and idempotent; usage/pricing can remain incomplete | mismatched publication/profile owner is rejected; completion cannot fabricate missing usage as zero |
| Provider deletion | both Workspace and AgentFramework production paths delete unreferenced profiles | both paths return typed publication/import references; PostgreSQL `Restrict` remains authoritative |
| Transfer | unreferenced transfers retain existing behavior | shared-provider references or a target-source secret collision are rejected before mutation |

## Exact executable evidence

- `SharedProviderStateModelTests`: 18 discovered, 18 passed.
- `SharedProviderPersistenceIntegrationTests`: 14 discovered, 14 passed against real PostgreSQL.
- `SharedProviderDeletionReferenceIntegrationTests`: 6 discovered, 6 passed through both
  production deletion surfaces.

The persistence lane includes clean migration, actual unique-conflict rejection, concurrent
singleton/publication creation, stable identity, optimistic concurrency, metadata-only audit,
ownership mismatch, and observer-after-commit proof. The deletion class has exactly six tests;
additional transfer preflight assertions live inside the existing persistence scenarios and do
not inflate its governed cardinality.

