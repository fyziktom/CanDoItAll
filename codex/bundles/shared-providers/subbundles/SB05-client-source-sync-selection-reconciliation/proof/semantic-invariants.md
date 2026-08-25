# SB05 semantic invariants

State: `PASS`.

1. Only a successful, validated, authoritative catalog absence may move a selected import to
   `Missing`; transient, authorization, contract, or trust failures never do.
2. Source identity, canonical base URI, network policy, and credential reference are source-owned.
   Imported profiles reuse them and never persist credential values.
3. A local import/profile identity is stable across repeat sync, remote refresh, missing/reappearance,
   retirement/reactivation, source edits, and transient recovery.
4. Local alias and enabled intent are operator-owned; remote display, model, endpoint, secret
   reference, and supported capability fields are reconciliation-owned.
5. Conditional GET is safe only in authoritative source/import state. A stale 304 cannot convert an
   unhealthy selection into a false no-op.
6. Remote source identity is pinned after the first trusted success. Mismatch blocks reconciliation
   until an explicit concurrency-checked reset.
7. Source disable prevents synchronization before secret or HTTP access; re-enable restores it.
8. Provider-profile observers run only after a successful commit and never for 304/identical plans.
9. Sync never silently substitutes a source, provider, publication, model, or credential and never
   hard-deletes a referenced profile.
