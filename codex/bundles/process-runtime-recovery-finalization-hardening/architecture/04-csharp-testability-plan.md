# C# Testability Plan

## Test Strategy

- Characterization tests first for current scheduling, retry, manager escalation, adapter completion, and artifact readiness behavior.
- Unit tests for new Runtime services without Module or AgentFramework dependencies.
- Integration tests for launch-to-dispatch-to-finalization-to-manager flows using production service paths.
- Driver/adapter tests for AgentFramework integration and managed artifact behavior.
- Source assertions for architecture boundaries and partial-class policy.

## Required Test Surfaces

| Surface | Tests required |
|---|---|
| Artifact lineage | Direct prior step, non-direct prior step, branch path, parent/child boundary, missing concrete artifact ref, stale/unreadable artifact ref. |
| Step contract retrieval | Current assignment fetch, required inputs, expected outputs, required receipts, branch choices, sensitivity filtering, unauthorized/stale assignment denial. |
| Finalization gate | Missing required input read, missing output artifact, ungrounded artifact reference, missing required receipt, manager handoff required, accepted completion. |
| Recovery router | Upstream repair, current-step idempotent retry, denied access manager route, missing tool manager route, transient provider retry, unknown failure manager route. |
| Context packaging | Bounded manifest package, large changed-file set, retrieval handle use, sensitivity handling, explicit driver full-content policy. |
| Driver isolation | Generic runtime tests compile without Module integration; AgentFramework behavior is tested behind driver contracts. |

## Fake-Proof Resistance

- Critical tests must use production launch or dispatch paths unless the subbundle explicitly states a narrower unit-test purpose.
- Positive downstream readiness cannot be proven by manually seeding `AvailableArtifactSlots`.
- Tests must assert concrete artifact refs or lineage rows, not only step status.
- Tests must include at least one negative path per changed router/finalizer rule.
- Proof manifests must include changed-file hashes and source assertions for moved responsibilities.

## Minimum Commands During Implementation

- Targeted unit tests for each affected process runtime/application/module test class.
- `dotnet test` for the smallest relevant test projects after each critical subbundle.
- CodeAnalytics dependency refresh after architecture-affecting subbundles.
- Full relevant regression pass in SB08.
