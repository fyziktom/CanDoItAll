# Current state

## Overall assessment
Most of bundle9's structural refactor work is materially present, but bundle9 is **not actually closed** because the hottest structure read seam still performs deletes and persists them during reads.

## Bundle9 area-by-area status

| Bundle9 area | Status | Notes |
|---|---|---|
| P9-001 legacy carrier retirement | Closed | `ProjectObjectRecord` no longer persists the legacy carrier fields and the schema/migrations retire them. |
| P9-002 binding layer no longer hydrates carrier fields | Closed | Runtime binding state is composed through `ProjectNodeBindingState` and `[NotMapped]` state instead of writing back to node carrier fields. |
| P9-003 marker single truth | Mostly closed | Persisted truth is `MarkersJson`, but read-only compatibility fallback from legacy metadata still exists. |
| P9-004 manifest-driven editors | Closed with proof gap | Shared `ConnectorConfigFieldEditor` exists, but current tests exercise only known plugins. |
| P9-005 bogus legacy enum identity for custom plugins | Closed | Save flows persist `LegacyResourceKind` / `LegacyProviderKind` only from the plugin, not synthesized from the editor model. |
| P9-006 open-world node references | Mostly closed | Core persistence rows are open-world string/string rows, but runtime still has typed helpers and metadata fallback. |
| P9-007 read path write-on-read retirement | **Open blocker** | `LoadAsync` still deletes stale projection rows and stale layouts during reads. |
| P9-008 generic connector command boundary | Closed | Generic connector outbox/command boundary is present and appears materially implemented. |

## Why this blocks the next plugin wave
The next plugin wave increases the number of readers, projection contributors, and structure reads. A read path that still mutates state creates:
- invisible side effects during reads,
- concurrency surprises,
- harder debugging and audit trails,
- accidental operator-data cleanup happening at unpredictable times,
- another false sense of closure because the old gate script stays green.
