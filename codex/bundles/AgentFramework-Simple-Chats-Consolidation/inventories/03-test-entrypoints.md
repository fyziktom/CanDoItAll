# Test entrypoints

## Workspaces

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Components.slnx
- tests/Solutions/CanDoItAll.Tests.Integration.slnx
- tests/Solutions/CanDoItAll.Tests.Playwright.slnx
- tests/Solutions/CanDoItAll.Tests.Stable.slnx

## Existing focused selectors

Unit:

- LlmChatApplicationBoundaryTests
- LlmChatCanonicalModelTests
- LlmChatDefinitionServiceTests
- LlmChatConversationApplicationServiceTests
- LlmChatOperationTests
- LlmChatDurableStreamEventTests
- ProviderRuntimeContractOwnershipTests
- LlmInvocationPortCompositionTests
- LlmChatProviderResolutionTests
- LlmChatRuntimeFenceTests
- LlmChatActiveOperationProjectionTests
- LlmChatDefinitionRevisionExecutionTests
- LlmChatWholeUseCaseProfileScopeTests
- LlmChatBackendCompositionTests
- LlmChatUiAuthorizationFacadeTests
- LlmChatDefinitionUiGatewayTests
- LlmChatOperationProjectionReducerTests
- LlmChatUiEventSessionGatewayTests
- LlmChatUiRegistrationAndArchitectureTests
- ProviderUsageNormalizationTests
- ProviderPricingTests
- DashboardQueryServicesTests
- ConversationShellRegistrationTests

Components:

- AgentsHomePageTests
- AgentDetailsDialogAvatarGenerationTests
- LlmChatConversationWorkspaceTests
- LlmChatDefinitionUiTests
- LlmChatConversationShellContributorTests
- LlmChatUiCompositionTests
- ConversationShellHostTests

Integration:

- LlmChatPersistenceIntegrationTests
- LlmChatTransactionalConcurrencyIntegrationTests
- LlmChatApiHardeningIntegrationTests
- LlmChatsApiIntegrationTests
- LlmChatsApiPostgreSqlIntegrationTests
- LlmChatsTurnApiIntegrationTests
- DatabaseMigrationIntegrationTests
- AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests
- FileSandboxWorkspaceUsageProjectionIntegrationTests

Playwright:

- AiAgentFlowTests is existing Agent regression context.
- No dedicated repeatable Simple Chat conversation flow exists.
- Add AgentFrameworkSimpleChatsConsolidationPlaywrightTests with exact named cases defined in SB08, SB09, and SB11.

Every selector must discover at least one test. New exact test names remain mandatory even when impact analysis cannot discover uncommitted/new cases.
