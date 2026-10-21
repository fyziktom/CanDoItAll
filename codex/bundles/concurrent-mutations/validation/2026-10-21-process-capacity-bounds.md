# Governed payload and finite root capacity

Status: all 179 selected cases pass. Finite root completion passes; preparation
allocation cost remains open for the following buffer correction. Executor activation,
sustained capacity and the end-to-end bundle remain open.

The [design](../plan/P07-process-capacity-bounds.md) retains the 256 lifetime-allocation
ceiling. New governed preparation is bounded to 32 MiB of serialized UTF-8 and accepted
source projection to 16 MiB total. Existing 1 MiB metadata and 4 MiB input limits remain.
Ordinary preparation and historical reads preserve their existing behavior. Actual
PostgreSQL overflow cases leave the preparation intact and accept no partial runtime,
budget or source rows.

The 179 cases include exact serializer boundaries, escaping, overflow and ordinary
compatibility; source/input corruption and replay; initial rollback and acknowledgement
loss; shared-scope recovery/funding; and 15 isolated startup-outcome cases. The latter
close the pure identity-verifier follow-up from the startup-failure architecture review.

Persistence Release builds in 6.379 seconds. New test setup needed two compile repairs:
existing readonly identity constructors and a missing Runtime import. Every attempt
retained its source identity; final Unit and Integration builds pass in 41.740 and
38.776 seconds. Test-only builds use `--no-dependencies` after the direct production
build. Initial PreparedLaunch topic discovery found 44 cases across existing partial
files; exact filters select the intended main-file 15 methods/22 cases. Both discovery
records are retained. Final discovery and execution agree on 179 cases across 15 lanes.

| Shared members / allocations | Setup across owner commits | Final owner transaction | Complete-set queries |
| --- | ---: | ---: | ---: |
| 1 / 2 | 390.6 ms | 46.8 ms | 2 |
| 32 / 33 | 8,846.5 ms | 32.2 ms | 2 |
| 128 / 129 | 36,013.8 ms | 131.7 ms | 2 |
| 255 / 256 | 67,734.8 ms | 122.5 ms | 2 |

Each finish uses 17 reader commands, checks every member/allocation and reads no prompt
payload. Actual query plans confirm the complete expected row counts. All finite samples
pass the one-second reference; this is not a percentile or sustained-contention proof.

At 1024 small steps, bounded preparation serialization has a 73.730 ms median versus
66.484 ms for the existing serializer, but allocates 27,159,029 versus 12,055,995 bytes
per call. At 128 small steps allocation is 3,393,720 versus 1,518,568 bytes. This excess
growth allocation is reopened for correction with cleared pooled buffers. The retained
measurements describe the MemoryStream candidate, not that future correction.

Selected input stays at one query and 5,344–5,350 serialized bytes through 1024 steps.
Initial acceptance takes 75.7/297.1/4,732.0 ms at 1/128/1024 small steps, and 531.8 ms
at 128 steps with a 1 MiB neighboring prompt. The large acceptance cost is explicitly
separate from selected reads and does not imply 1024 funded children are supported.

Architecture snapshots `snap-20261021053914-77361fd7` and
`snap-20261021062953-77361fd7` retain seven project edges, six existing cycles,
112 registrations, 32 mapped entities and 760 findings. The two new types are the
serializer and its private platform-stream adapter. No schema, reference or runtime
partial changed. Existing analyzer diagnostics remain.

Full portability enforcement passes all 15,657 reviewed findings without a delta.
Rendering, documentation and whitespace checks pass. Source/runtime identity holds;
all 15 runtime commands were observed. No remaining process, database-name or temporary
path delta was found. The candidate is Released; historical cleanup denials remain.

- Retained proof: `proof/2026-10-19-planning-authority/c4-process-capacity-bounds.json`,
  487,771 bytes, SHA-256 `B95355808C13F4A34DFD09F6D4724DA0BE5926D222BCD35DF3FC87A66347499A`.
- 9,817 source inputs: `49E91F615D0E08A035A1C57FACB2E768DEBB940E8B43699873406B8649EAE362`.
- 2,595 runtime inputs: `85181112988D2C42B3B90B690826569FF69663E668663AC4682F5E694420B821`.
- The proof includes all 84 measurement records and actual query plans; secret scanning
  found zero findings.

Read-only adapter review also found that ordinary authority checks only the current
assignment policy. Removing it could bypass the originally governed route. Correct that
existing predicate against already loaded original evidence before any activation.
The signed root checkpoint still waits for its existing local Pinentry window. SPA
remains clean on `codex/concurrent-mutations`, without merge.
