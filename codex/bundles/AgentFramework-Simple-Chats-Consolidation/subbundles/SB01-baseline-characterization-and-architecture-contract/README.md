# SB01 — Baseline characterization and architecture contract

## Status

- Completed — CP0 Pass
- Stage: baseline
- Proof tier: Governed

## Objective

Freeze the actual execution baseline, convert current behavior into named characterization proof, and accept or reject the exact target project/usage/migration contract before product relocation begins.

## Owned Requirements

- ASCC-001
- ASCC-002
- ASCC-003
- ASCC-004
- ASCC-005
- ASCC-006
- ASCC-007
- ASCC-014

## Prerequisites

- None.

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.LlmChats/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs
- repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/
- repo://tests/Solutions/

## Explicit Non-Goals

- Do not move or rename product types/projects.
- Do not change schema, DI, routes, UI, or usage calculations.
- Do not run Stable or full Playwright.
- Do not design new Simple Chat features.

## Implementation Steps

1. Record execution start SHA, worktree status, branch, SharedInfo SHA, and drift from prepared head.
2. Refresh CodeAnalytics over the affected projects; record project/type cycles and compare the known baseline.
3. Produce a machine-checkable caller inventory for old projects/namespaces across App, Web APIs, migrations, tests, solution, DI, assembly scanning, navigation, and transfer.
4. Produce a file-to-target-owner matrix for Core, Application, Runtime, Persistence, Components, Agent module, Web, and Composition.
5. Record current DI registrations and hosted-service/shell contributor cardinality.
6. Record EF table/configuration/migration/model-snapshot/transfer identities and generate a no-destructive-schema baseline.
7. Add/confirm characterization tests for API/SSE, profile fence, lease/recovery, invocation audit, main workspace, floating Simple Chat, and floating Agent behavior.
8. Freeze typed usage identity, usage/pricing completeness, retry/deduplication, historical classification, partial-source error, query, and /chats compatibility semantics.
9. Retry Components MCP for current library/component availability; record failure if transport remains unavailable.
10. Run the C# architecture governor/review gate and stop on any unresolved target cycle or ambiguous data migration.

## Acceptance Criteria

- [x] CP0 evidence identifies every current caller/writer/registration and target owner.
- [x] Characterization selectors discover non-zero tests and pass.
- [x] Target project graph is cycle-free on paper and through CodeAnalytics.
- [x] Legacy cost policy never guesses or reprices.
- [x] No production/schema/UI implementation is included.
- [x] Architecture gate is Pass.

## Validation Depth

- Proof tier: Governed.
- Critical foundation: yes; invalidates every downstream architecture/data/UI surface.

Governed: manifest, execution report, source/caller/schema inventories, CodeAnalytics transcript, semantic invariants, commands/results, candidate hash, and architecture gate.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Components.slnx

Required existing selectors:

- LlmChatApplicationBoundaryTests
- ProviderRuntimeContractOwnershipTests
- LlmInvocationPortCompositionTests
- LlmChatProviderResolutionTests
- LlmChatRuntimeFenceTests
- LlmChatActiveOperationProjectionTests
- LlmChatDefinitionRevisionExecutionTests
- LlmChatWholeUseCaseProfileScopeTests
- LlmChatUiAuthorizationFacadeTests
- LlmChatDefinitionUiGatewayTests
- LlmChatOperationProjectionReducerTests
- LlmChatUiEventSessionGatewayTests
- LlmChatUiRegistrationAndArchitectureTests
- AgentsHomePageTests
- AgentDetailsDialogAvatarGenerationTests
- LlmChatDefinitionUiTests
- LlmChatConversationWorkspaceTests
- LlmChatConversationShellContributorTests
- ConversationShellRegistrationTests

Expected discovery: every selector discovers at least one test; record exact counts before running.

## Invalidation And Broad-Gate Decision

Stable and full Playwright are forbidden. Reopen CP0 on any change to project map, usage identity/completeness, schema plan, route plan, or known cycle baseline.

## UI Composition Contract

Inventory only. Record current /agents, /chats, nested tabs, navigation, shell contributors, and 1600x1000 baseline screenshots if a deterministic host is already available; do not change them.

## C# Architecture Impact

Architecture and characterization only. Any production code change beyond a narrowly necessary test seam requires explicit review and must not implement a later boundary.

## Boundary Ownership

The bundle owns the target contract; existing projects retain runtime ownership until their named extraction subbundle.

## Dependency Direction

Validate architecture/02-csharp-dependency-direction.md and stop if the target cannot be achieved without a forbidden edge.

## Pattern Decision

Confirm PSR-001 through PSR-009. Record any replacement as a dated decision with affected requirements/subbundles before implementation.

## Testability Contract

Characterization must observe behavior at public/application boundaries and be capable of failing after a shallow or incorrect move. Browser-only proof is insufficient.

## Partial Class Policy

Add no partial class. Inventory current partials and establish the no-new-partial source guard.

## Architecture Proof Required

Before graph/callers/DI/schema, target graph, CodeAnalytics cycles, architecture gate, and explicit CP0 unlock.

## Progression Gate

- CP0 Pass unlocks SB02. Any ambiguity in historical cost or profile/lease ownership blocks progression.

## Reopen Triggers

- baseline drift;
- new caller/registration/schema surface;
- characterization failure or zero discovery;
- changed target project count/name;
- new/enlarged cycle;
- Components MCP evidence contradicting the UI contract.

## Covered Inputs

- Raw request: isolate Simple Chat MAF libraries, consolidate Agent/provider/cost/UI placement, preserve behavior, and prepare before implementation.
- Requirements ASCC-001–007 and ASCC-014; findings F-001–F-020 are inventoried, including the follow-up UI/avatar gaps that SB07 owns.

## Exact Source References

- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats.Persistence
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats.Ui
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor

## Deliverables

- Frozen SHA/caller/DI/schema/test baseline, accepted target ownership matrix, and CP0 gate record.

## Dependency Impact

- SB02-SB11 rely on this baseline; weak caller/schema/behavior evidence invalidates every extraction and closure claim.

## Acceptance Checklist

- All Acceptance Criteria above are checked with actual evidence; CP0 remains closed on any unresolved ambiguity.

## Proof Required

- proof/SB01/manifest.md, semantic-invariants.md, execution-report.md, CodeAnalytics/caller/schema/test transcripts, and C# architecture gate.
