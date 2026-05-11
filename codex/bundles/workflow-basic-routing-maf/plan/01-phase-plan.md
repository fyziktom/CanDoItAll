# Phase Plan

## Phase Sequence

1. Establish route domain contracts, legacy compatibility, and validation shape.
2. Compile those contracts into MAF routing primitives and prove runtime behavior.
3. Add workflow canvas authoring UX and visual route summaries.
4. Prove persistence/API round-trip and seed practical route scenarios.
5. Run final test/browser proof, complete raw-note closure, and record ARTL handoff.

## Subbundle Dependency Map

```mermaid
gantt
title Workflow Basic Routing MAF dependency and validation map
dateFormat  YYYY-MM-DD
section Critical foundations
01 Routing contracts and compatibility :crit, s1, 2026-05-11, 1d
02 MAF compiler routing integration :crit, s2, after s1, 1d
section UI and data
03 Workflow canvas routing authoring UX :crit, s3, after s2, 1d
04 Validation persistence API and scenario seeds :s4, after s1, 1d
section Closure
05 Routing test proof browser proof and ARTL handoff :s5, after s3, 1d
```

## Critical Subbundles

- Subbundle 01 is critical because it defines the saved graph contract consumed by all later phases.
- Subbundle 02 is critical because UI proof is meaningless unless MAF runtime routing honors the saved routes.
- Subbundle 03 is critical because the user explicitly requested workflow canvas support.
- Subbundle 04 is not allowed to close before route metadata round-trips through persistence/API.
- Subbundle 05 is the closure gate and must reopen earlier subbundles if proof contradicts their assumptions.

## Phase Gates

- Gate after preparation: run `python3 codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py .codex/bundles/workflow-basic-routing-maf --profile initiative --stage prepared` and repair failures.
- Gate before subbundle 01: confirm the current `WorkflowEdge` shape and saved-definition serializer have not changed since bundle preparation.
- Gate after subbundle 01: old definitions load; new route metadata serializes; validator detects malformed route definitions.
- Gate after subbundle 02: runtime tests prove conditional false branches are skipped, switch default is honored, and fan-out selection matches expected target indices.
- Gate after subbundle 03: browser proof shows route builder authoring, save/load, validation, and preview-run.
- Gate after subbundle 04: API and persistence integration tests prove route metadata round-trip.
- Final gate: execution report contains subbundle proof, browser analytics, raw-note closure, and ARTL handoff notes.
