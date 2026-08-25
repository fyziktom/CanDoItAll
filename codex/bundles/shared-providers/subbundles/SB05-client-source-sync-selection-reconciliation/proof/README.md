# SB05 proof artifacts

State: `COMPLETE`

This directory contains governed proof for safe source transport, source lifecycle, conditional
synchronization, deterministic selection/reconciliation, stable local identity, and non-destructive
failure/recovery.

- `proof-manifest.json` is the machine-readable result and exact 18/22/16 selection record.
- `manifest.md`, `semantic-invariants.md`, `semantic-changed-files.md`, `changed-files.md`, and
  `hashes.sha256` inventory the durable evidence.
- `architecture/` records before/after references and CodeAnalytics, public/partial review, and
  independent cross-review.
- `behavior/` records the URI/network matrix and reconciliation semantics.
- `security/` records source-secret, SSRF, TLS, and logging containment.
- `transcripts/` preserves entry, failing-first, builds, exact discovery/runs, downstream
  revalidation, audits, and closure validation, including superseded attempts for honest chronology.

No broad, browser, multi-instance, live-provider, paid-provider, UI, or runtime-connector lane ran.
The single broad aggregate remains SB12-owned.
