# Architecture Checkpoints

## Entry Rule

Use the architecture governor, dependency graph audit, testability and architecture review
gate with the current source diff. The three mandatory architecture/performance skills
were used for preparation; implementation must maintain their invariants, not merely
repeat their names. Existing source/owner and project boundaries are normative.

| Gate | Required decision / source inspection | Required proof before progression |
|---|---|---|
| Prepared | Current-state/constructor/partial inventory, canonical ownership, actual invocation paths, two-pass performance scan, chosen patterns, dependency direction and proof plan. | Completed bundle structure/link/traceability checks and independent semantic review. No product runtime claim. |
| SB01 | Stable entry/attempt/source/time/partition/fence contracts, closed enums/ports, minimal actual project edges, no runtime implementation stub. | Existing and extended project/public-signature guards, pure identity tests and actual factory/source matrix. |
| SB02 | One pricing calculator path and one extracted finalizer; immutable tariff/provider-reported evidence; long-count/unknown/free semantics. | Buffered/terminal-stream and negative pricing fixtures, no live catalog lookup or historical repricing. |
| SB03 | EF-only persistence boundaries, actual same-context outbox, detail quota/protection, stable profile data and additive mappings/migration. | Disposable relational transaction/migration/profile/retention tests and production model registration. Cleanup remains disabled. |
| SB04 | Each real typed producer path, decorator position, trusted caller mapping, owner suppression authority and stream lifecycle. | Actual factory/backend/SDK/batch/media/relay tests; durable begin, distinct retries and no persistence-induced inference replay. |
| SB05 | Every canonical create/update/delete publisher, durable file journal including late first commit, monotonic source versions and tombstones. | Production producer→intent→index evidence, crash/replay/late-expiry/multi-owner tests, bounded backfill/lock work. |
| SB06 | Scalar server query, live keyset, separate metadata/content/manage policies, exact owner access and before-publish profile/authority recheck. | SQL/EXPLAIN, real host authorization and cursor/detail negative tests with no body/files/provider reads. |
| SB07 | Provider form authority, shared panel/controller, Workspace-owned settings and compact desktop component composition. | Zero eager reads/save side effects; stale-result and separate policy tests; normal/open-overlay screenshots with focus/scroll review. |
| SB08 | Actual final graph/DI/model configuration and class sizes; composed producer/UI/lifecycle/scale behavior. | Governed runtime artifacts, measured bounds and one actual-diff justified affected regression checkpoint. |
| SB09 | No fake separation, duplicate canonical data/charges, unused alternate path, silent fallback, invalid proof or unresolved mandatory note. | Completed validators, semantic invariant/manifest audit and final architecture verdict. |

## Architecture Change Controls

- Each new runtime class normally fits 250 lines; over250 requires a responsibility
  review and over400 requires a documented redesign/exception. Measure methods/constructor
  dependencies too. Do not split into partials just to satisfy a line threshold.
- Preserve actual existing guards: ProviderManagement cannot depend on outer Workspace/
  Web/AgentFramework UI, and feature modules cannot add direct Providers dependencies.
  Workspace must not import AgentFramework's settings component.
- Abstractions has no project references. Application is independent of concrete owners,
  persistence, HTTP/SDK/UI. Infrastructure discovers new EF configuration through outer
  composition, not a reverse reference.
- An extraction must remove the old behavior and update the actual construction/call site.
  A test-only collaborator, dummy adapter or unused alternate path fails the gate.
- A mutable source version is not a new identity; transient profile generation is not a
  persistent partition; correlation is not an attempt. Reopen SB01 if any becomes ambiguous.
- File journal production covers first canonical creation/attachment as well as later
  update/delete, even when an old pending reservation has expired.
- All data/body/security changes require explicit positive and adversarial evidence. UI
  visibility and blank result tests are insufficient to prove backend authorization/laziness.

## Review And Invalidation

The [C# architecture review record](../reviews/csharp-architecture-gate.md) records current
preparation findings. Product gate statuses remain Not started. Every later review records
actual source revision, diff/graph, test artifacts, verdict and affected downstream gates.
Use [validation strategy](02-validation-strategy.md) invalidation keys; do not rerun all
projects for a documentation-only update or waive tests because the graph looks clean.
