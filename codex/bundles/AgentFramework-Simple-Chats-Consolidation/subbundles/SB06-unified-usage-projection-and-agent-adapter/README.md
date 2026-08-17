# SB06 — Unified usage projection and Agent adapter

## Status

- Prepared
- Stage: analytics-checkpoint
- Proof tier: Governed

## Objective

Implement independent Agent and Simple Chat usage-source adapters, compose exact scoped projections, and move cross-workload Agent usage assembly out of the broad execution partial while preserving Agent projection behavior.

## Owned Requirements

- ASCC-004
- ASCC-014
- ASCC-015
- ASCC-016
- ASCC-020
- ASCC-021
- ASCC-022
- ASCC-023
- ASCC-025
- ASCC-026
- ASCC-027
- ASCC-028
- ASCC-029
- ASCC-030
- ASCC-038
- ASCC-046

## Prerequisites

- SB05
- CP1 Pass

## Current Source Anchors

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Usage.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workspace/AgentOverviewModels.cs
- target://src/MAF/Common/CanDoItAll.AgentFramework.Usage/
- target://src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/

## Explicit Non-Goals

- Do not change Razor/dashboard rendering.
- Do not dual-write between stores.
- Do not replace Agent operational storage.
- Do not flatten chat consumers into Agent rows.
- Do not silently suppress a failed source.

## Implementation Steps

1. Implement Agent usage-source adapter over existing observation/projection evidence with typed Agent/unattributed legacy classification.
2. Implement Simple Chat usage-source adapter over invocation records joined to stable operation/conversation/definition identities.
3. Register both source adapters exactly once in App.Composition.
4. Implement scoped query for Agents, SimpleChats, Both and source health/freshness.
5. Deduplicate by source-specific stable contribution identity; prohibit transcript/terminal rows.
6. Preserve separate usage-known/pricing-known and new/legacy price provenance.
7. Define provider/model totals and typed Agent/Simple Chat consumer rows.
8. Extract usage enrichment/normalization/pricing assembly from AgentFrameworkWorkspaceExecutionService.Usage into top-level collaborator(s); preserve existing Agent projection/API compatibility.
9. Add aggregate invariants for retries, failures, cancellations, duplicate rows, ambiguous Agent legacy evidence, partial source failure, and idempotent rebuild.
10. Run CP2 architecture/behavior gate.

## Acceptance Criteria

- [ ] Agents and SimpleChats scopes are disjoint.
- [ ] Both equals their deduplicated union.
- [ ] Failed/retried billed attempts count once; operations count once.
- [ ] Agent existing usage/projection tests remain green.
- [ ] Source errors/unknown/unpriced are explicit.
- [ ] Agent execution partial shrinks and no new partial appears.
- [ ] CP2 Pass.

## Validation Depth

- Proof tier: Governed.
- Critical foundation: yes; scoped dashboard correctness and final cost claims depend on it.

Governed critical behavior extraction with failing-first aggregate tests, cross-store integration, old-owner shrink, deduplication invariants, DI and architecture proof.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Integration.slnx

Required:

- ProviderUsageAggregationTests
- DashboardQueryServicesTests
- ProviderUsageNormalizationTests
- ProviderPricingTests
- AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests
- FileSandboxWorkspaceUsageProjectionIntegrationTests
- LlmChatPersistenceIntegrationTests
- SimpleChatUsageProjectionSourceTests

Exact cases:

- BothEqualsDeduplicatedSourceSum
- RetriedAndFailedBilledAttemptsCountOncePerAttempt
- DuplicateOperationOrdinalDoesNotIncreaseTotals
- LegacyKnownTokensWithoutPricingRemainUnpriced
- PartialSourceFailureIsVisible
- ExistingAgentProjectionRemainsCompatible

Expected discovery: all exact cases and non-zero per selected class.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden. Reopen on source mapping, identity/dedup, completeness, pricing, aggregation, Agent observation/projection, DI, or partial extraction changes.

## UI Composition Contract

Return UI-neutral snapshot supporting metrics, provider/model charts, Agent consumers, Simple Chat definition/conversation consumers, dialogs, and source-health states.

## C# Architecture Impact

Introduces producer adapters and removes cross-workload usage responsibility from a broad Agent execution partial.

## Boundary Ownership

Each operational owner maps its evidence; Usage combines it; Agent page later consumes it.

## Dependency Direction

Agent Core and Simple Chat Persistence -> Usage. Usage never -> either implementation.

## Pattern Decision

Ports/adapters, composite read model, immutable evidence, typed strategy mapping.

## Testability Contract

Each adapter tests independently; aggregate tests use in-memory source fakes; no test requires both real stores except named integration proof.

## Partial Class Policy

No new partial. Record line/member reduction in AgentFrameworkWorkspaceExecutionService.Usage and direct tests of new top-level owner.

## Architecture Proof Required

Before/after dependency and cycle graph, direct source owner, negative double-count/source-dependency tests, old partial shrink, DI cardinality, architecture gate.

## Progression Gate

- CP2 Pass plus SB07 completion unlocks SB08.

## Reopen Triggers

- mismatch between source totals and aggregate;
- source ambiguity guessed;
- known/unpriced collapsed;
- UI/persistence dependency in Usage;
- old partial regrowth;
- Agent regression.

## Covered Inputs

- Raw request: put Simple Chat cost together with Agent cost and switch Agents/Simple Chats/Both.
- Requirements ASCC-004, ASCC-014–016, ASCC-020–030, ASCC-038, ASCC-046.

## Exact Source References

- C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Usage.cs
- C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Persistence\Storage\FileSandboxWorkspaceExecutionSliceStore.cs
- C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\Providers\ProviderUsageModels.cs
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats.Persistence\Entities\LlmChatPersistenceRows.cs

## Deliverables

- Independent Agent/Simple Chat usage source adapters, exact composite query, typed consumer rows, and extracted Agent usage collaborators.

## Dependency Impact

- SB08/SB09/SB11 rely on exact deduplication/completeness; any mismatch invalidates browser cost proof.

## Acceptance Checklist

- All Acceptance Criteria above pass for disjoint scopes, Both union, retries/failures, legacy unpriced, source errors, and Agent compatibility.

## Proof Required

- proof/SB06/manifest.md, failing/passing aggregate/source tests, cross-store integration, old-partial shrink, DI/cycle/reference guards, architecture gate.
