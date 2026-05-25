# Implementation prompt for Codex

You are a senior C#/.NET architect working in the CanDoItAll repository.

Target branch: `db-remove-sqlite`.

Execute `codex/bundles/db-process-runtime-final-hardening-followup-bundle-v5` phase by phase.

Main objective:
Finalize the PostgreSQL-only process runtime by closing remaining process DB canonicality and throughput gaps.

Critical constraints:
- PostgreSQL remains the only persistent runtime database.
- Normal runtime contexts must use canonical pooled `AppDbContext`.
- Profile-specific contexts are maintenance-only.
- A non-expired lease is canonical ownership unless owner-death is explicitly proven.
- No stale worker may write process step, process outbox, connector outbox, or automation delivery final state.
- Throughput improvements must preserve partitioned canonical ownership.
- Comments in source code must be in English.

Execute subbundles in order:
SB01 -> SB02 -> SB03 -> SB04 -> SB05 -> SB06 -> SB07 -> SB08.

Do not skip semantic proof. Do not claim success with focused tests only unless broad failures are classified with evidence.
