# Finding-to-subbundle map

| Finding ID | Finding | Owning subbundle(s) | Earliest gate that can prove direction | Proof focus |
| --- | --- | --- | --- | --- |
| `F001` | Dual dependency representation | `02`, `03`, `04` | Gate A | Canonicality and compatibility quarantine |
| `F002` | Validation mutates state | `03`, `04` | Gate A | Pure validation proof |
| `F003` | Destructive graph persistence | `05`, `06`, `07` | Gate B | Stable IDs and atomic save |
| `F004` | Missing optimistic concurrency | `05`, `07` | Gate B | Conflict tests and error translation |
| `F005` | Publish/version race windows | `08`, `11` | Gate C | Publish conflict hardening |
| `F006` | Runtime orchestration hotspot | `09`, `11` | Gate C | State-machine/policy extraction |
| `F007` | Read-side small-data assumption | `10`, `11` | Gate C | Query-service and projection proof |
| `F008` | Cross-module duplication | `12`, `15` | Gate D | Shared extraction without over-centralization |
| `F009` | Workspace monolith risk | `13`, `15`, `16` | Gate D | Smaller components + browser proof |
| `F010` | Schema/config long-file sprawl | `14`, `15`, `16` | Gate D | Split files and migration coherence |

## Corrective mapping

| Failed gate | Default corrective playbook |
| --- | --- |
| Gate A | `subbundles/_corrective-foundation-stabilization` |
| Gate B | `subbundles/_corrective-persistence-and-concurrency-reset` |
| Gate C | `subbundles/_corrective-runtime-and-query-reset` |
| Gate D | `subbundles/_corrective-workspace-and-shared-infrastructure-reset` |
