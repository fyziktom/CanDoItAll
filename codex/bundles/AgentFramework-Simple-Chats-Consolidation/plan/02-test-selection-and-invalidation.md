# Test selection and invalidation

## Focused selection rule

For every subbundle:

1. obtain impacted tests from the actual diff with behavior intent;
2. add the exact named selectors in the subbundle even if impact analysis omits new tests;
3. list tests and require non-zero discovery;
4. run every required selector;
5. record command, exact candidate SHA, discovery count, pass/fail/skip, duration, and artifact path;
6. stop on unexpected zero discovery.

## Workspace policy

- Unit: domain, Application, Runtime, Usage policies and architecture guards.
- Components: Razor rendering, typed tabs/query/scope, dialogs and shell composition.
- Integration: EF/PostgreSQL, migrations, transfer, profile fences, leases, API/SSE, file+EF source adapters.
- Playwright: named consolidation tests only.
- Stable: one unfiltered run, SB11 only.

## Expected named additions

- ProviderUsageWorkloadSelectionTests
- ProviderUsageAggregationTests
- SimpleChatUsageEvidenceTests
- SimpleChatUsageProjectionSourceTests
- SimpleChatArchitectureBoundaryTests
- AgentFrameworkSimpleChatsRouteTests
- AgentFrameworkSimpleChatsConsolidationPlaywrightTests

At least these exact behaviors must exist:

- AgentsOnlyReturnsOnlyAgentEvidence
- SimpleChatsOnlyReturnsOnlyInvocationEvidence
- BothEqualsDeduplicatedSourceSum
- NoneAndUnknownSelectionAreRejected
- RetriedAndFailedBilledAttemptsCountOncePerAttempt
- DuplicateOperationOrdinalDoesNotIncreaseTotals
- LegacyKnownTokensWithoutPricingRemainUnpriced
- ChatSessionIdDoesNotClassifySimpleChat
- SimpleChatsTabImmediatelyFollowsAgents
- ChatsRouteRedirectsAndPreservesRecognizedState
- FloatingAgentAndSimpleChatRemainIndependent
- AllUsageScopesDriveChartsAndDialogs

## Broad-gate policy

- Unfiltered Stable is forbidden in SB01-SB10.
- SB11 freezes one candidate SHA and runs tests/Solutions/CanDoItAll.Tests.Stable.slnx exactly once.
- A failure is recorded honestly. Repair uses focused selectors only; the one-shot Stable run is not repeated without explicit user authorization.
- Full unfiltered Playwright is not authorized.
- Named Playwright selectors plus Playwright MCP browser scenarios are required at SB08/SB09/SB11.

## Invalidation keys

Reopen the owning subbundle and every downstream proof when any of these changes:

- public Core/Application contract or strong ID;
- provider resolution/profile fence/lease behavior;
- EF row/configuration/table/migration/transfer field;
- usage status, pricing status, identity, selection, aggregation, or deduplication rule;
- AgentOverview or neutral dashboard projection contract;
- route/tab/query key or redirect mapping;
- Components parameter, gateway, shell contribution, authorization mapping;
- DI/composition/assembly scan/hosted service;
- HTTP route/contract/SSE/security behavior;
- test workspace/project reference;
- known baseline cycle/dependency graph.

## Conditional escalation

- Run PostgreSQL selectors when EF configuration, migration, transaction, profile, lease, or provider audit fields change.
- Run API/SSE selectors when Application contracts or endpoint mappings change.
- Run browser proof when route, tab, query, component, dialog, chart, navigation, or shell composition changes.
- Retry Components MCP when shared/reusable UI controls change.

