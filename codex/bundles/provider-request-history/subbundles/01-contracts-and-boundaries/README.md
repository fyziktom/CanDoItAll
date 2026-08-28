# SB01 — Contracts And Boundary Lock

## Status

- Execution: `Not started`. This is an implementation contract, not completed feature evidence.
- Preparation: defined; entry requires the prerequisites below and renewed scope authorization.

## Objective

- Freeze the smallest typed contract and project boundary set, source/attempt ownership matrix and characterization tests before any history behavior is added.

## Covered Inputs

- N002, N006–N012; R002, R006–R014.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Prerequisites

- Prepared bundle readiness passed; user authorizes implementation separately.
- Recheck current source/working tree against the recorded anchor; preserve unrelated changes.
- Read the three architecture guard documents and existing provider foundation tests.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderArchitectureFoundationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderManagementBoundaryTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderBoundaryArchitectureGuardTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderUsageAggregationTests.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[Provider foundation guard](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderArchitectureFoundationTests.cs).
[Provider management boundary guard](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/ProviderManagementBoundaryTests.cs).
[Provider boundary guard](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/ProviderBoundaryArchitectureGuardTests.cs).
[Usage aggregation](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/ProviderUsageAggregationTests.cs).
[Existing Models project](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- Create the three small history projects with only contracts/minimal registration scaffolding needed by dependent phases; no no-op shipped capture/search implementation.
- Lock EntryId, logical RequestId, nullable legacy AttemptId, immutable SortAtUtc/TimeBasis, stable source identity/version separation, stable partition and transient fence.
- Lock typed price/usage/caller/owner/detail/coverage states and ports; preserve existing public contracts compatibly with explicit unknown legacy defaults.
- Record exact selected new project edges and actual type usages; extend architecture guards in their natural existing homes.
- Characterize current duplicate-observation/retry/relay ownership and all identified actual capture paths.

## C# Architecture Impact

Introduces neutral contracts without moving existing canonical stores. Do not add an interface-only mirror of existing models or a second provider framework.

## Boundary Ownership

History.Abstractions owns new neutral values/ports. Existing producer modules own adapters; Web owns identity mapping; Composition owns construction. SharedProviders.Abstractions retains zero project references.

## Dependency Direction

Abstractions has no project dependencies. Application references only Abstractions; Persistence references the approved inner/application/Infrastructure boundaries. All other additions must fit the explicit dependency ceiling.

## Pattern Decision

ADR01/04/05: metadata projection, explicit identity/state, ports at real boundaries. Reject source version in uniqueness and provider/model/correlation as attempt identity.

## Testability Contract

Pure identity/version/ownership tests plus existing dependency guards. Proposed cases: Source_version_update_preserves_entry_identity; Same_correlation_does_not_merge_attempts; History_contracts_do_not_reference_outer_types.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Verify no new module-to-Providers reference, ProviderManagement-to-Workspace/Web/UI edge, Workspace-to-AgentFramework edge or Infrastructure-to-History.Persistence edge.

## Dependency Impact

- Blocks SB02 and SB03; any identity/ownership/signature change reopens every downstream store/capture/query fixture.
- The capture matrix must identify MAF SDK and callback-stream paths before SB04 can proceed.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: Yes; public identity, project graph and canonical ownership..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` / `FullyQualifiedName~ProviderArchitectureFoundationTests|FullyQualifiedName~ProviderManagementBoundaryTests|FullyQualifiedName~ProviderBoundaryArchitectureGuardTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests|FullyQualifiedName~ProviderHistoryIdentityTests|FullyQualifiedName~ProviderUsageAggregationTests`.
- Selection reason: Current boundary guards, source-project graph cycles and inner dependency characterization, plus new identity behavior and the existing usage de-duplication baseline.
- Expected discovery: Provider_management_project_has_no_outer_feature_dependency, BothEqualsDeduplicatedSourceSum, Source_project_reference_graph_has_no_cycles and Inner_provider_projects_do_not_reference_outer_feature_layers, plus the three proposed contract cases above. Record exact actual cases/counts at execution;
  zero discovery or a missing named expected case fails the gate. Discovery has not run now.
- Invalidation keys: HistoryContractV1; CanonicalSourceIdentity; ProviderGraph; CaptureCoverageMatrix.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --list-tests --filter 'FullyQualifiedName~ProviderArchitectureFoundationTests|FullyQualifiedName~ProviderManagementBoundaryTests|FullyQualifiedName~ProviderBoundaryArchitectureGuardTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests|FullyQualifiedName~ProviderHistoryIdentityTests|FullyQualifiedName~ProviderUsageAggregationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --no-build --filter 'FullyQualifiedName~ProviderArchitectureFoundationTests|FullyQualifiedName~ProviderManagementBoundaryTests|FullyQualifiedName~ProviderBoundaryArchitectureGuardTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests|FullyQualifiedName~ProviderHistoryIdentityTests|FullyQualifiedName~ProviderUsageAggregationTests'
```

## Implementation Steps

1. Revalidate current source seams/test names and list existing focused tests before edits.
2. Add neutral typed contracts and selected minimal project edges; update source ownership inventory.
3. Add pure behavioral/architecture cases and preserve old tests without weakening assertions.
4. Review constructors/factories, public DTOs and typed unknown legacy states; freeze the contract decision.

## Acceptance Checklist

- [ ] Stable identity/version/time semantics are unambiguous for new attempts and legacy aggregates.
- [ ] No production capture/query returns fake success or empty placeholder results.
- [ ] Selected direct references have real type consumers and pass boundary guards.
- [ ] Canonical content ownership and all actual invocation paths are recorded before implementation.

## Proof Required

- Store focused command/discovery results, source references and changed behavior evidence in the execution report; do not fabricate a full runtime manifest from static inspection.
- Attach the before/after affected project graph, public-signature inventory, exact test discovery and source characterization; no browser or paid call is needed.
- Follow [validation strategy](../../plan/02-validation-strategy.md); distinguish existing
  test anchors from proposed new cases, and source proof from executed behavior.

## Browser Validation Logging

N/A for direct UI changes in this phase. Production host/SQL/lifecycle proof is required where listed; the two-tab desktop acceptance remains SB07/SB08.

## Scope Exceptions

- This phase alone does not close the complete product request. Deferred IDM/EGCP person
  mapping, global federation, exact wire replay, mobile redesign and unrelated refactors
  remain outside the bundle.
- No paid inference, user-database mutation or deployment without explicit authorization.

## Do Not Do

- Do not implement UI/storage/backfill in this foundation phase.
- Do not introduce ambient string/object metadata or an allow-all policy default.
- Do not resolve an architecture test failure by deleting or broadening its guard.

## Progression Gate

- SB02/SB03 may start only after typed identity/source ownership, dependency guards and characterization evidence pass independent review.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- Any new provider path, canonical source identity ambiguity, forbidden edge or incompatible DTO invalidates this foundation and dependent phases.
