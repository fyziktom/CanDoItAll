# SB02 — Typed provider usage foundation

## Status

- Prepared
- Stage: foundation
- Proof tier: Governed

## Objective

Create the neutral AgentFramework.Usage boundary, typed atomic workload/selection contracts, normalized source/result models, completeness rules, and aggregate query without depending on either operational store.

## Owned Requirements

- ASCC-008
- ASCC-014
- ASCC-015
- ASCC-020
- ASCC-021
- ASCC-023
- ASCC-025
- ASCC-028
- ASCC-029

## Prerequisites

- SB01
- CP0 Pass

## Current Source Anchors

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workspace/AgentOverviewModels.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ProviderUsageNormalization.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Usage.cs

## Explicit Non-Goals

- Do not implement Agent or Simple Chat persistence adapters.
- Do not change the dashboard/UI.
- Do not migrate Simple Chat rows.
- Do not centralize operational writes.
- Do not delete existing Agent overview contracts.

## Implementation Steps

1. Add CanDoItAll.AgentFramework.Usage under src/MAF/Common and solution entries.
2. Define atomic ProviderUsageWorkloadKind values Unknown, Agent, SimpleChat; Both is forbidden as stored kind.
3. Define validated flags ProviderUsageWorkloadSelection for Agents, SimpleChats, Both; reject None/unknown bits.
4. Define normalized provider/model/consumer/totals/completeness/freshness/source-error contracts and IProviderUsageProjectionSource.
5. Implement the source-neutral aggregation query with deterministic deduplication contribution keys.
6. Keep AgentOverviewSnapshot intact; the new dashboard usage snapshot is separate.
7. Move genuinely cross-workload normalization/pricing orchestration from Agent Core only when it does not create a Models/Usage cycle; otherwise wrap existing canonical pricing policy without duplication.
8. Define unambiguous legacy Agent classification policy and explicit unattributed contribution behavior.
9. Add unit and architecture tests before any producer adapter.
10. Record direct source proof that Usage references neither store implementation nor UI.

## Acceptance Criteria

- [ ] Agents, SimpleChats, Both, invalid None, and unknown bits are directly tested.
- [ ] Both is exact deduplicated union and never persisted.
- [ ] Usage-known and pricing-known are separate.
- [ ] Partial source failure is explicit.
- [ ] Usage has no Persistence/Module/Razor/Web dependency.
- [ ] No duplicate pricing model or new cycle is introduced.

## Validation Depth

- Proof tier: Governed.
- Critical foundation: yes; Agent/chat adapters and dashboard depend on these contracts.

Governed foundation proof with failing-first tests, public contract inventory, project graph, negative architecture assertions, semantic invariants, and review gate.

## Focused Test Selection

Workspace: tests/Solutions/CanDoItAll.Tests.Unit.slnx

Required:

- ProviderUsageWorkloadSelectionTests
- ProviderUsageAggregationTests
- ProviderUsageNormalizationTests
- ProviderPricingTests

Exact new cases:

- AgentsOnlyReturnsOnlyAgentEvidence
- SimpleChatsOnlyReturnsOnlyInvocationEvidence
- BothEqualsDeduplicatedSourceSum
- NoneAndUnknownSelectionAreRejected
- PartialSourceFailureIsVisible
- ChatSessionIdDoesNotClassifySimpleChat

Expected discovery: all six exact new cases plus every selected existing class; non-zero per selector.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden. Reopen on workload, selection, completeness, identity, pricing, source-result, or aggregate contract change.

## UI Composition Contract

No Razor change. Contracts must be display-neutral and capable of driving metrics, charts, consumer rows, and dialogs without referencing component types.

## C# Architecture Impact

Adds a shared inward Usage project and begins extracting usage responsibility from a broad Agent execution partial.

## Boundary Ownership

Usage owns contracts/aggregation; producer-specific mapping stays with Agent Core and Simple Chat Persistence.

## Dependency Direction

Usage may reference Models; neither source implementation is referenced by Usage. Prevent a Models <-> Usage cycle.

## Pattern Decision

Ports/adapters plus composite read model and typed flags (PSR-002, PSR-004, PSR-005).

## Testability Contract

Aggregate service runs entirely with in-memory source fakes and detects duplicate, invalid selection, unknown pricing, unattributed legacy, and partial-source behavior.

## Partial Class Policy

No new partial. If shared behavior moves from AgentFrameworkWorkspaceExecutionService.Usage, the top-level owner must be proven and the partial must shrink.

## Architecture Proof Required

Before/after project graph, direct source owner proof, forbidden-reference tests, cycle analysis, public API surface, old-owner shrink, review gate.

## Progression Gate

- All Usage contracts/tests and architecture proof pass before SB03.

## Reopen Triggers

- producer adds a dimension the neutral contract cannot represent;
- any ambiguous zero/unknown behavior;
- new dependency on Core/Persistence/UI;
- pricing model duplication;
- failed direct negative test.

## Covered Inputs

- Raw request: switch cost view between Agents, Simple Chats, and Both without treating chats as agents.
- Requirements ASCC-008, ASCC-014–015, ASCC-020–021, ASCC-023, ASCC-025, ASCC-028–029.

## Exact Source References

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ProviderUsageNormalization.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Usage.cs

## Deliverables

- Store-neutral AgentFramework.Usage project, typed producer/selection contracts, aggregation/completeness logic, and direct tests.

## Dependency Impact

- SB03/SB05 producers and SB06/SB09 consumers compile and reason against this contract; semantic drift reopens them.

## Acceptance Checklist

- All Acceptance Criteria above pass, including invalid flags, deduplication, partial failure, and no persistence/UI dependency.

## Proof Required

- proof/SB02/manifest.md, semantic invariants, failing/passing unit transcripts, ProjectReference/cycle/source guards, old-partial shrink evidence, architecture gate.
