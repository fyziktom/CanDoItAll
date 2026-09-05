# Contract for implementation bundles

## Authority and reference

Include [the v2 reference block](../templates/child-bundle-reference-block.md). Preserve
raw input and distinguish proposed design from current authorization. A prepared execution
prompt does not authorize implementation during a bundle-only task.

## Required preparation

1. Outcome, exact source/consumer scope, non-goals, current revision, and local dirty state.
2. Complete component assessment, including descendants, triggered overlays, types,
   transitive assemblies, composition, native/browser operations, and assets.
3. Behavior/transition matrix with baseline evidence, mutation commit points, failure
   handling, concurrency, request cancellation, and explicit unresolved defects.
4. State and lifetime owners per instance; semantic location separate from draft/view data.
5. Pattern decisions justified by responsibility, with no interface/filename/count quotas.
6. Primary files and conditional dependency repairs. Assign cross-module/public contract
   changes an owning work unit before touching them; do not hide them in a parent facade.
7. Independently actionable phases with prerequisites, proof tier, test project and exact
   selector/topic, expected named cases/discovery, invalidation keys, broad-gate decision,
   progression and rollback.
8. Per-phase UI composition: primary/supporting content, stats, list/editor arrangement,
   textarea/dialog sizing, first viewport, intended scroll owner, and overlay inspection.
9. Production wiring proof plus direct policy/workflow and real-component scenario proof.
10. Explicit downstream extraction/sandbox opportunity and measurement baseline, independent
    of production bookmarkability implementation.

## Scope decisions

Routine local naming, pure extraction, and justified host/policy decomposition are in-scope
decisions when they preserve the owned behavior and project direction. Record them in the
PSR without asking for approval again.

New projects, physical relocation, new routes, sibling edits, other module ownership,
changed public cross-module contracts, and observable behavior changes require a concrete
dependency/consumer/validation plan and an explicit scope decision before implementation.
A child can finish its limited scope honestly with named downstream extraction blockers;
it cannot call a required but unproven scenario solved.

## Closure record

Report each of:
- responsibilities removed and responsibilities retained with reasons;
- direct and transitive graph, public type ownership, and real production wiring;
- semantic state, editor lifetime, and effect ownership;
- behavior matrix coverage, test migrations, negative evidence, and discovery;
- semantic-state readiness, deterministic rendering, scenario interactions, lightweight
  graph, browser-sandbox proof, and actual bookmarkability independently;
- exact missing dependency, owner and next checkpoint for each partial/deferred dimension;
- source rollback point and data/external-effect limitations;
- durable documentation candidates and shared-base compatibility.

## Reopen

Reopen the earliest owner if state duplicates, an editor draft is reset by section changes,
a facade hides dependencies, late effects modify a newer session, a public type prevents
extraction, loading boundaries change, a scenario needs the production runtime, an
implementation-shape test obstructs valid simplification, or later moves change the graph.
Revalidate affected consumers; do not repeat broad gates for evidence-only changes.
