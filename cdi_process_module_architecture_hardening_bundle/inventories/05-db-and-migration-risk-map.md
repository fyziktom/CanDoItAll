# DB and migration risk map

| Operation | Current risk | Required hardening | Owning subbundle |
| --- | --- | --- | --- |
| Definition save | Destructive rewrite, no full explicit transaction, no optimistic concurrency | Transaction + concurrency token + diff persistence | `05`, `06` |
| Definition publish | Version race, clone coupling, uniqueness race | Transaction + conflict handling + separated clone engine | `08` |
| Step transition | Lost update risk under concurrency | Concurrency token + explicit conflict translation | `05`, `09` |
| Definition list / analytics | Broad loads and in-memory shaping | Query services and slimmer projections | `10` |
| Template loading | Duplicated file/json parsing logic | Consolidated shared helper | `12` |
| Entity configuration | Dense files and relationship-policy visibility issues | Split config and explicit relationship hygiene | `14` |

## Migration rule

Any change that alters the persisted model must:
- update both provider migrations or snapshots coherently,
- preserve existing data,
- avoid provider-specific assumptions unless intentionally justified.

## Snapshot rule

Do not hand-wave snapshot drift. If the model changes, ensure both provider snapshots are synchronized and included in the proof.
