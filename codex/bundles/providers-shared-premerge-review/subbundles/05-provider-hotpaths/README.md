# SB05 — Bounded provider hot-path work

## Status

- State: `Pending prerequisites`
- Proof tier: Behavioral
- Execution: not started; this file is a plan, not proof.

## Objective

Remove repeated immutable-set creation, avoid redundant full-payload copies, and avoid expensive catalog evaluation on cache hits without weakening safety.

## Covered Inputs

- R05/R10; N02/N04/N06; three hot-path findings in performance-review.md

## Prerequisites

- SB01/SB02/SB04 pass their foundations; freeze request behavior and retention lifecycle before measurement/optimization.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayUsageExtractor.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogQueryService.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogCache.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/SharedProviderPublicationAndCatalogTests.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Record baseline allocation/query data for the workloads in plan/02-validation-strategy.md before changing code.
- Cache constant validation sets statically with the exact existing comparer/contents; 29 nonempty runtime construction sites are scan leads, not permission to rewrite all LINQ.
- Use owned ReadOnlyMemory parser overloads to avoid the two full-body ToArray copies; consolidate parse/rewrite/usage only if lifetime stays explicit and tests prove payload ownership.
- Make cache-hit detection use a cheap persisted stamp projection; do expensive eligibility/model work on miss. Keep cross-process freshness, required-secret existence and dispatch-time target validation.
- Measure after with identical inputs/configuration. Treat fixed maintenance scheduling as capacity investigation; change it only for observed SLO/backlog failure, otherwise document results.

## Dependency Impact

- Unlocks final docs and frozen evidence. Changes to wire behavior reopen SB01/SB06 instead of hiding in optimization.
- Reopen on changes to: cached stamp/eligibility/routing, request allowlists/comparers, payload ownership/lifetime, protocol limits, modified SQL.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: Unit SharedProviderRelayPolicyTests and SharedProviderPublicationAndCatalogTests; Integration SharedProviderOpenAiCompatibilityIntegrationTests. Measurements supplement behavior tests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- QueryCacheUsesPersistedStampAcrossInstancesAndRechecksCurrentEligibility (existing; mandatory)
- CachedCatalog_SecretDeletionRevokesRoute (1 new)
- BufferedPayload_OwnershipAndLimitsRemainValid (1 new)
- Normalization_PreservesAllowedAndRejectedRequestMatrix (named existing matrix plus added boundaries)
- Invalidation keys: cached stamp/eligibility/routing, request allowlists/comparers, payload ownership/lifetime, protocol limits, modified SQL.
- Broad-gate decision: No broad gate here; same-source focused suites and before/after measurement. Final shared-contract gate SB09.

## Acceptance Checklist

- [ ] Allowed/denied request subset, limits, order/comparer semantics, model/price behavior and failure outcomes unchanged.
- [ ] Publication/profile edits and secret deletion in another scope/process invalidate stale routing; no cache-only authorization.
- [ ] Allocation/query work measurably drops in targeted workloads; no claimed universal speedup or correctness-changing optimization.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- Fast process-only cache that ignores external revocation is a failed implementation. A smaller allocated-byte count cannot override failing authorization tests.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

Use existing cache/projector and protocol adapter boundaries. No FrozenSet construction in per-message path; no unsafe code, broad sealing campaign, or new plugin framework.

## Boundary Ownership

- Keep the responsibility in the named current owner. Any extraction must be independently testable and remove moved logic from the old class.

## Dependency Direction

- Preserve architecture/02-csharp-dependency-direction.md; no new project/reference is assumed. If needed, stop that edit and amend the boundary/checkpoint before proceeding.

## Pattern Decision

- Follow architecture/03-csharp-pattern-selection-records.md. Prefer current adapters/decorators and small functions; avoid abstractions without a concrete boundary.

## Testability Contract

- Pure policies use direct isolated tests; persistence/network behavior uses the selected integration seam and a real production consumer. Do not construct the full runtime for a pure rule.

## Partial Class Policy

- No new runtime partial. Existing generated code and cohesive UI code-behind are allowed; no nested service used to hide responsibility.

## Architecture Proof Required

- Relevant checkpoint: plan/architecture-checkpoints.md. Review .csproj diff, policy placement, production registration, independent tests and no-new-partial proof.
- If behavior is extracted, show old-owner shrink/thin facade and a negative test rejecting delegation back to the monolith. No extraction is required solely for this metric.

## Progression Gate

- Pass only after acceptance and required proof agree; otherwise record precise failed/blocked cases.
- Unlocks final docs and frozen evidence. Changes to wire behavior reopen SB01/SB06 instead of hiding in optimization.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.
