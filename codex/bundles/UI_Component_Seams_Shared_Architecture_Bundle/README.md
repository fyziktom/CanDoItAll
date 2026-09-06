# CanDoItAll UI Component Seams — Shared Architecture Bundle

**Reference ID:** CDA-UI-SEAMS-BASE-v2
**Version:** 2
**Kind:** Non-executable architecture reference
**Revision date:** 2026-09-05
**Preparation status:** Revised; manual semantic validation recorded in reviews
**Execution status:** Non-executable reference; the current owner request authorizes Providers-02 implementation and, after its closure, preparation only of the catalog extraction/sandbox measurement child

## Purpose and authority

Improve component boundaries across the application while preserving working behavior.
Agents is the first pilot, not a universal implementation template. This revision adopts
the accepted architectural review and the Agents hardening feedback. The Agents child owns
its authorized implementation; this reference does not itself schedule physical moves,
new routes, sandbox creation or sibling changes.

Read [the revision input and finding map](inputs/01-accepted-review-and-revision-request.md).
The earlier owner directive and bookmarkability meeting sources remain historical inputs.
The meeting pack proposes navigation decisions; its route shapes, history policy, and MAUI
scope are not final owner decisions. Current requirements take precedence over those proposals.

## Decisions retained and strengthened

1. Components and FileTools remain live sibling source dependencies.
2. Prove logical ownership in place before moving a component cluster.
3. Give semantic state one owner per workspace instance; do not centralize every effect
   or every editor draft in a route page or circuit-scoped service.
4. Keep AppComponents independent of concrete feature modules. Recognize existing cohesive
   UI families such as Conversations.Components and Conversations.Shell.
5. Choose abstractions by responsibility, substitution, and dependency direction. There
   is no interface quota, mandatory controller template, or prohibition on a justified host.
6. Inspect the entire rendered and triggered dependency graph, including contract types,
   nested dialogs, component services, CSS, JS, and browser/native capabilities.
7. Preserve state transitions, mutation semantics, authorization, concurrency, load timing,
   and failures through a behavior matrix, not an unchanged number of tests.
8. After a proven seam, physical extraction/sandbox work and bookmarkability binding are
   separate dependent workstreams. Production URL implementation is not a sandbox prerequisite.
9. Measure the development loop before and after a physical extraction. In-place refactoring
   alone is not evidence that the watched/build graph became smaller.
10. Migrate a second, different UI archetype before declaring a pilot pattern universal.

## Reading map

- [Context and target](architecture/00-program-context-and-target.md)
- [Ownership and placement](architecture/01-component-ownership-and-placement.md)
- [State, intent, lifetime, and routing](architecture/02-state-intent-and-routing-readiness.md)
- [I/O and controller decisions](architecture/03-service-io-and-controller-seams.md)
- [Sandbox and project readiness](architecture/04-sandboxability-and-future-project-boundaries.md)
- [Behavior and architecture proof](architecture/05-test-and-architecture-guard-hygiene.md)
- [Rejected patterns and decision rules](architecture/06-anti-patterns-and-decision-rules.md)
- [Program sequence](plan/00-program-sequence.md)
- [Child-bundle contract](plan/01-child-bundle-contract.md)
- [Assessment template](templates/component-boundary-assessment-template.md)
- [Reference block](templates/child-bundle-reference-block.md)
- [Source register](references/00-source-register.md)
- [Review and validation](reviews/00-shared-base-review-checklist.md)
- [Agents hardening feedback](reviews/02-agents-hardening-feedback.md)

## Readiness vocabulary

Every child reports separately: semantic-state readiness; deterministic rendering;
scenario interaction coverage; lightweight compile-graph readiness; browser-sandbox proof;
and implemented bookmarkability. Each result is Proven, Partial, Deferred, or Blocked with
evidence or an exact missing dependency. Intended outcomes are not achieved results.

## Scope and lifecycle

This reference contains no executable subbundles or product test command catalog. Each child
owns exact source changes, test selection, proof tiers, gates, and rollback. No new feature
module, universal component base, UI redesign, package snapshot mode, or Manager optimization
is prescribed here.

The previous development-test-repair hold was historical; the Agents child already exists.
Refresh actual source and test state at execution entry rather than waiting on that stale hold.

This remains a temporary branch artifact. Move proven durable rules to maintained product
documentation or SharedInfo at an owned documentation checkpoint; remove temporary bundles
only after active consumers have migrated. This revision performs neither action.

## Shape compatibility

This non-executable reference deliberately uses an alternate shape. Its semantic roles are:
inputs; architecture requirements; inventories/references for current state; plan/templates
for dependencies and consumers; reviews for status and validation. Structural implementation
scaffolding would add no meaning. Validate those roles manually and verify links, JSON,
input preservation, and the complete checksum manifest.

- [Lifecycle follow-up and provider sequence](reviews/03-lifecycle-and-provider-followup.md)

- [Provider mutation and shared-authority feedback](reviews/04-provider-mutation-and-authority-feedback.md)

- [Unconfirmed verification feedback](reviews/05-unconfirmed-verification-feedback.md)

[Catalog extraction and asset evidence feedback](reviews/06-catalog-extraction-asset-feedback.md) records generalized lessons proven by the first real rendering extraction and browser sandbox. Performance claims remain gated by its measurement phase.

[Verification postconditions and acknowledged delivery](reviews/07-verification-postconditions-and-delivery.md) records the proven provider follow-up lessons.

Follow-up: [explicit assets and direct observation](reviews/08-explicit-assets-and-direct-observation.md).

[Explicit acknowledgement and retained evidence](reviews/09-explicit-acknowledgement-and-retained-evidence.md).

[Second stateful rendering archetype](reviews/10-second-archetype-state-read-seam.md).
