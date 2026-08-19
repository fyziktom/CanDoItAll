# Architecture checkpoints

## CP0 — Baseline and proof

Blocks on:

- unsynchronized affected source;
- stale proof head;
- a branch-induced or unresolved prior failure;
- unclassified previous stable-gate failures;
- accidental broad-suite rerun.

## CP1 — Backend hardening

Review:

- canonical writable owner;
- real transaction boundaries;
- state-machine/reducer consistency;
- cancellation and compensation;
- profile scope;
- claim/lease/dispatcher;
- bounded reads;
- project references/cycles;
- direct and PostgreSQL proof.

Streaming remains locked unless every row is Ready.

## CP2 — Streaming API

Review:

- provider wire streams;
- attempt audit and retry boundary;
- durable event journal and coalescing;
- 202 request independence;
- SSE replay/gap/heartbeat/terminal close;
- external authorization/provenance;
- deterministic fake-provider HTTP tests;
- Linux portability and line-ending/framing proof.

## FINAL

Review:

- actual head/proof identity;
- stable filtered gate;
- CI Windows/Linux/macOS matrix;
- migration/model state;
- architecture/traceability/guard results;
- no deferred critical/high finding;
- no UI scope leakage.
