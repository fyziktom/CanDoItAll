# Finding closure matrix

| Finding | Severity | Owner | Required closure evidence | State |
|---|---|---|---|---|
| F-001 — Conversation unit of work is not one transaction | Critical | SB01 | `subbundles/SB01-canonical-transaction-and-persistence-repair/proof-manifest.json` | Closed by SB01 PostgreSQL rollback proof |
| F-002 — Conversation title and transcript metadata have duplicate writable truth | Critical | SB01 | `subbundles/SB01-canonical-transaction-and-persistence-repair/proof-manifest.json` | Closed by SB01 canonical ownership and migration proof |
| F-003 — Turn admission and evidence are split across commits | Critical | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed at `be36fedb2ce329af6021cd2330eb6162d8ef2db4` by atomic admission rollback proof |
| F-004 — Assistant commit and terminal operation state are not atomic | Critical | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed by atomic success-finalization rollback proof |
| F-005 — Compensation exhaustion is swallowed | Critical | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed by exact compensation and RecoveryRequired escalation proof |
| F-006 — Committed cancellation can still become success | Critical | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed by monotonic cancellation and pre/post regression proof |
| F-007 — Idempotent replay is gated by later mutable lifecycle state | High | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed by identity-first replay-after-archive proof |
| F-008 — Profile fence does not cover the complete use case | Critical | SB03 | `subbundles/SB03-whole-use-case-profile-fencing/proof-manifest.json` | Closed at `96f054905eecd33e04228e7837ae7850e3eeeeb4` by public-interface scope capture, atomic commit fencing, and PostgreSQL switch proof |
| F-009 — Running ownership and cancellation are process-local | High | SB04 | `subbundles/SB04-durable-dispatch-lease-and-multi-instance-cancellation/proof-manifest.json` | Closed at `7389daff6c21a4568895e514debe110434908d67` by PostgreSQL competing-owner and remote-cancellation proof |
| F-010 — HTTP request lifetime owns paid execution | High | SB04 | `subbundles/SB04-durable-dispatch-lease-and-multi-instance-cancellation/proof-manifest.json` | Closed by expected-red historical timeout and final 202-before-provider-completion proof |
| F-011 — Attempt audit does not represent actual attempts consistently | High | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | SB02 closes deterministic outcome parity; provider retry-attempt expansion revalidated by SB07 |
| F-012 — Timeout and cancellation reduce differently before and after restart | High | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed by Failed/DeadlineExceeded audit and shared reducer proof |
| F-013 — Conversation and transcript reads are unbounded or in-memory paged | High | SB05 | `subbundles/SB05-bounded-transcript-queries-and-pagination/proof-manifest.json` | Pending |
| F-014 — Archive can race active work | High | SB02 | `subbundles/SB02-atomic-turn-state-machine-and-recovery/proof-manifest.json` | Closed by row-locked active/nonterminal archive exclusion |
| F-015 — Provider contracts and drivers have no true streaming path | High | SB07 | `subbundles/SB07-provider-neutral-streaming-contracts-and-drivers/proof-manifest.json` | Pending |
| F-016 — HTTP origin is caller-controlled and dedicated LLM scopes are missing | Medium | SB10 | `subbundles/SB10-api-security-and-external-client-contract/proof-manifest.json` | Pending |
| F-017 — Committed closure and branch provenance are not release-ready | High | SB00 | `subbundles/SB00-baseline-sync-and-proof-reconciliation/proof-manifest.json` | Pending |
