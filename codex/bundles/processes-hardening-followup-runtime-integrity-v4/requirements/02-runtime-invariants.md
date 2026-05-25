# Runtime Invariants

## INV01 — Artifact materialization must unblock dependents deterministically

When an upstream required artifact is recorded, all blocked downstream steps waiting on that artifact must be re-evaluated in the same transaction or a durable follow-up outbox event.

## INV02 — Lineage must not depend on bounded text

Execution run ids, recovery run ids, source artifact ids, workflow run ids, subprocess run ids, and rework packet ids must be stored in typed fields or structured payloads, not only in a truncated external reference key.

## INV03 — Non-mutating means non-mutating

If a process step disallows `MutateProductTarget`, no tool path, script path, helper script, or run tool may mutate product targets. Artifact-only writes remain allowed only in current-run artifact roots or trusted artifact destinations.

## INV04 — Grounding source authority must be typed

Writable target aliases must come from trusted current-run source records, not arbitrary free-text summaries.

## INV05 — Required artifacts must be content-valid

A required artifact that declares JSON, YAML, Markdown, image, report, dataset, transcript, or evidence pack must validate against actual stored bytes when a managed storage path is present.

## INV06 — Process-owned mapping must be explicit

Workflow and subprocess outputs must satisfy process artifact expectations through explicit mapping or high-confidence one-to-one adapter rules.

## INV07 — Own artifact failures cannot be hidden by branch outcomes

A step that is responsible for producing its own required artifact must block or recover when that artifact is missing/invalid, unless its explicit disposition policy says otherwise.

## INV08 — Operation contract is a typed model

Text parsing is a migration aid, not the source of truth for process step operations and target scope.

## INV09 — No-progress history survives restarts

Repeated no-progress behavior must be detected using durable fingerprints, not only in-memory tool invocation counts.

## INV10 — High-risk process definitions must fail fast before runtime

Strict lint gates must apply automatically to high-criticality/autonomous processes and to process definitions with ambiguous product mutation boundaries.
