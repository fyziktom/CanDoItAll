# SB02 — Shared Pricing Evidence

## Status

- Execution: Completed

## Objective

- Fix the shared relay's source-level null-price path using request-time evidence while preserving honest legacy/partial/provider-reported cost.

## Covered Inputs

- N001, N007, N011; R001, R007, R011.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Prerequisites

- SB01 contract/boundary gate passed.
- Characterize both relay finalization paths and preserve shared protocol behavior.
- Coordinate shared DTO/schema files with SB03; parallel work is allowed only for disjoint files.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderAuditedRelayStream.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderPricingTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[Relay finalizer/stream](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderAuditedRelayStream.cs).
[Relay application service](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs).
[Pricing models/calculator](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs).
[Pricing tests](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/ProviderPricingTests.cs).
[Shared OpenAI compatibility fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- A small execution pricing resolver freezes exact model/tariff/source/currency before dispatch; finalizer consumes it without live catalog reads.
- Extract the existing finalizer into its own top-level file and remove its old body; preserve bounded independent cleanup and concurrency.
- Extend existing arithmetic with validated long counts and observed categories; retain ProviderReported precedence, explicit free state, partial/missing/unsupported/legacy reasons.
- Persist only available price evidence and expose the reason in existing usage projection; no historic repricing or new catalog.

## C# Architecture Impact

Keep tariff ownership/calculation in existing model/pricing seams and relay orchestration in ProviderManagement. No pricing/search/retention manager or repeated formula implementation.

## Boundary Ownership

Pricing resolver and relay finalizer own immutable dispatch tariff use; canonical audit owns final persisted result. Metadata query displays persisted evidence without recalculation.

## Dependency Direction

ProviderManagement uses existing Models pricing plus neutral history contracts as approved. No credential registry, UI, Web or current-rate service pulled into the finalizer.

## Pattern Decision

ADR04 plus existing calculator reuse. New interface only if an actual boundary needs substitution; pure rate validation can stay concrete.

## Testability Contract

Extend existing ProviderPricingTests and shared fixtures. Proposed cases: Relay_buffered_and_streaming_use_frozen_tariff; Long_usage_does_not_clamp_or_overflow; Provider_reported_zero_remains_known; Missing_categories_remain_partial; Legacy_unknown_is_not_repriced.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Only one calculator formula path and one finalizer owner remain; no outer-feature dependencies or large-file responsibility growth.

## Dependency Impact

- SB04 captures the price snapshot at actual dispatch; SB06 displays stable provenance.
- SB03 coordinates additive price/provenance fields; an incompatible change reopens both phases.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: Yes for truthful price semantics, but not a standalone history release..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` / `FullyQualifiedName~ProviderPricingTests|FullyQualifiedName~SharedProviderRelayPolicyTests`; `C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` / `FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests`.
- Selection reason: Existing arithmetic and buffered/stream relay production fixtures own the changed behavior.
- Expected discovery: Cost_calculator_prices_uncached_cached_and_output_tokens_separately, Usage_summary_applies_long_context_pricing_per_observation_not_to_aggregate_tokens, PersistedProviderRelay_ResolvesRouteSecretAndFinalizesMetadataOnlyAudit, plus proposed cases above. Record exact actual cases/counts at execution;
  zero discovery or a missing named expected case fails the gate. Discovery has not run now.
- Invalidation keys: PriceEvidenceV1; RelayFinalizer; LongUsageArithmetic; SharedWireCompatibility.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --list-tests --filter 'FullyQualifiedName~ProviderPricingTests|FullyQualifiedName~SharedProviderRelayPolicyTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --no-build --filter 'FullyQualifiedName~ProviderPricingTests|FullyQualifiedName~SharedProviderRelayPolicyTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --list-tests --filter 'FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --no-build --filter 'FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests'
```

## Implementation Steps

1. Add failing characterizations for current null price in buffered and terminal-stream paths.
2. Implement frozen pricing, long-count validation and conservative free/unknown migration semantics.
3. Extract finalizer without changing cancellation/stream lifetime; update compatible price projection.
4. Test tariff changes mid-request, missing terminal usage, provider-reported amount and mixed currencies.

## Acceptance Checklist

- [ ] Supported known relay usage becomes priced; unknown/partial remains explicit.
- [ ] No zero placeholders silently become free; authoritative provider-reported zero survives.
- [ ] Cache/reasoning/long-context rates are correct per observed attempt.
- [ ] Old evidence is not repriced from today's catalog and search has no provider/catalog side effects.

## Proof Required

- Store focused command/discovery results, source references and changed behavior evidence in the execution report; do not fabricate a full runtime manifest from static inspection.
- Capture focused arithmetic/relay results including failed/cancelled/partial streaming paths and the source diff that removes Price:null as unconditional behavior. The exact deployed5210 row remains a later runtime check.
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

- Do not sum different currencies or label a configured-rate estimate as an invoice.
- Do not fake missing usage categories, clamp long counts, or invent historical tariff snapshots.
- Do not change public OpenAI JSON/SSE shape or retry inference after persistence failure.

## Progression Gate

- SB04 may use the price contract only after both buffered and terminal-stream evidence and negative price cases pass; coordinate schema with SB03.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- Changes to price units/currencies, terminal usage extraction, explicit-free migration or provider-reported precedence invalidate pricing and its downstream display proof.
