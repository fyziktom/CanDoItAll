# Quality Gates

## Dream Candidate Quality Gate

An aggregate candidate must not become an active memory unless it passes these checks or goes through an explicit review approval:

- Every generated claim has at least one allowed source map.
- High-confidence aggregate claims have independent support or clear single-source labeling.
- Contradictory clusters create either an explicit contradiction memory or a review item.
- Superseded temporal claims are not presented as current truth.
- Restricted/redacted inputs do not leak into unrestricted aggregate text.
- The aggregate records its synthesis origin and algorithm/profile version.

## Recall Synthesis Quality Gate

A synthesized brief must satisfy:

- It is shorter than the raw context pack unless diagnostics are explicitly requested.
- It contains only claims present in selected memory/evidence maps.
- It keeps scores/trace details out of normal text.
- It exposes statement IDs or internal provenance IDs that can resolve references on demand.
- It respects access and redaction policy when resolving references.

## Fast-Done Guard

A dream run that finishes quickly is not automatically wrong, but it must report enough metrics to prove work depth. A run should be flagged as shallow if it:

- Scans source items but creates no clusters.
- Creates clusters but reads no member details.
- Generates aggregates with zero claim-source maps.
- Performs no validation checks.
- Completes with only count-based success and no quality report.
