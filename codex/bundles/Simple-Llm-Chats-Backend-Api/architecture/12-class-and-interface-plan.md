# Proposed class and interface plan

This is a responsibility plan, not permission to ignore current repository naming conventions. SB00
must reconcile exact namespaces and existing reusable types before SB01 locks filenames.

## `CanDoItAll.Modules.LlmChats`

### Cohesive domain files

```text
Definitions/
  LlmChatDefinition.cs
  LlmChatDefinitionRevision.cs
  LlmChatDefinitionStatus.cs
  LlmChatDefinitionValidation.cs
  LlmChatDefinitionSnapshot.cs
  LlmChatThinkingEffortSelection.cs      # only if a focused wrapper is needed; never a duplicate effort enum

Conversations/
  LlmChatConversation.cs
  LlmChatConversationStatus.cs
  LlmChatConversationOrigin.cs
  LlmChatConversationTitlePolicy.cs

Operations/
  LlmChatOperation.cs
  LlmChatOperationStatus.cs
  LlmChatInvocationRecord.cs
  LlmChatOperationStateMachine.cs

Common/
  LlmChatIdentifiers.cs
  LlmChatFingerprints.cs
  LlmChatFailure.cs
  LlmChatPaging.cs
```

Tiny value objects may be grouped by cohesive concern. Do not create dozens of one-line files merely
to satisfy a pattern.

### Application services

```text
Application/
  ILlmChatDefinitionApplicationService.cs
  LlmChatDefinitionApplicationService.cs
  ILlmChatConversationApplicationService.cs
  LlmChatConversationApplicationService.cs
  ILlmChatOperationApplicationService.cs
  LlmChatOperationApplicationService.cs
  LlmChatOperationReconciler.cs
```

Each service owns one aggregate/use-case family. Do not combine them into `LlmChatManager`.

### Ports

```text
Ports/
  ILlmChatDefinitionRepository.cs
  ILlmChatConversationRepository.cs
  ILlmChatOperationRepository.cs
  ILlmChatInvocationRecordRepository.cs
  ILlmChatUnitOfWork.cs
  ILlmChatProviderResolver.cs
  ILlmChatRuntimeLeaseFactory.cs
  ILlmChatRuntimeLease.cs
  ILlmChatOperationScopeAccessor.cs
  ILlmChatConversationEngine.cs
  ILlmChatOperationCancellationRegistry.cs
```

Do not add speculative context, attachment, moderation, or deployment interfaces in this bundle unless
a current production use case consumes them. Their future shapes stay documented in
`architecture/09-enterprise-chatbot-readiness.md` and `architecture/11-deferred-work.md`.

### Commands/results

Prefer immutable command/result records grouped by use case:

- definition create/update/lifecycle/query;
- conversation create/list/detail/rename/archive;
- turn execute;
- operation query/cancel/recover.

Commands contain IDs and bounded product values, never EF entities, credentials, or complete provider
profiles.

Definition commands/results carry the existing typed nullable reasoning-effort value. Provider-option
results carry safe per-model capability projections. `ILlmChatProviderResolver` owns both revision
validation and option projection so endpoint code cannot invent capability rules.

## `CanDoItAll.Modules.LlmChats.Persistence`

```text
EntityConfigurations/
  LlmChatDefinitionConfiguration.cs
  LlmChatDefinitionRevisionConfiguration.cs
  LlmChatDefinitionTagConfiguration.cs
  LlmChatConversationConfiguration.cs
  LlmChatTranscriptConfiguration.cs
  LlmChatMessageConfiguration.cs
  LlmChatOperationConfiguration.cs
  LlmChatInvocationRecordConfiguration.cs

Repositories/
  EfLlmChatDefinitionRepository.cs
  EfLlmChatConversationRepository.cs
  EfLlmChatOperationRepository.cs
  EfLlmChatInvocationRecordRepository.cs

Conversations/
  EfLlmConversationStore.cs
  LlmConversationPersistenceMapper.cs

Runtime/
  DatabaseProfileLlmChatRuntimeLeaseFactory.cs
  DatabaseProfileLlmChatRuntimeLease.cs
  CanonicalLlmChatProviderResolver.cs
  ProfileFencedLlmChatInvocationPort.cs
  LlmChatOperationScopeAccessor.cs
  LlmChatConversationEngine.cs
  InProcessLlmChatOperationCancellationRegistry.cs

Persistence/
  EfLlmChatUnitOfWork.cs
  LlmChatsPersistenceServiceCollectionExtensions.cs

DatabaseTransfer/
  LlmChatsDatabaseTransferHandler.cs
  LlmChatsTransferDocument.cs
```

Map product domain entities directly where feasible. Persistence-only transcript rows are allowed
because the generic immutable conversation document is not an EF aggregate. Their mapper must be
deterministic and directly tested.

## Generic conversation layer

Prepared additive changes:

- `LlmConversationStartRequest.ConversationId` optional init property;
- `LlmConversationTurnRequest.TurnId` optional init property;
- `LlmConversationService` ID selection helper;
- optional typed thinking-effort override on `LlmModelSettings`, with the provider-runtime adapter
  translating it into the existing model-parameter envelope;
- focused tests proving supplied and generated identities.

Do not add product application interfaces to `Llm.Abstractions`. The domain project references only
the generic contracts; the persistence project owns the dependency on the generic conversation
implementation. When the execution baseline still locates `IProviderRuntimeProfileSource` in
AgentFramework Core, move the narrow read contract to `AgentFramework.Providers`. Add a similarly
narrow model-capability resolver there rather than consuming the broad editor/health service. Existing
implementations may implement these contracts without moving provider persistence in this bundle.

## Existing provider-neutral projects

When DEC-003 confirms the prepared baseline ownership:

```text
src/MAF/Common/CanDoItAll.AgentFramework.Providers/
  ProviderRuntimeProfileSource.cs          # IProviderRuntimeProfileSource only
  ProviderModelCapabilityResolver.cs       # narrow read-only capability contract

src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/
  LlmProviderRuntimeServiceCollectionExtensions.cs

src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/
  WorkflowLlmServiceCollectionExtensions.cs  # delegates port registration to owner
```

Do not move editor, health, persistence, or agent catalog behavior merely to satisfy this bundle. The
extraction is limited to contracts with real provider-runtime consumers and the idempotent stateless
port registration.

## Web API

Suggested split after current Web conventions are confirmed:

```text
Api/
  LlmChatsApi.cs
  LlmChatDefinitionsApi.cs
  LlmChatConversationsApi.cs
  LlmChatOperationsApi.cs
  LlmChatApiContracts.cs
  LlmChatApiMapper.cs
  LlmChatApiResults.cs
```

Keep endpoint files small enough to review. Mapping and error policy must not be duplicated across
route lambdas.

The Web split also owns the sanitized provider-options route and DTOs. Effort strings are parsed only
at the transport boundary into the existing enum; domain/application code remains strongly typed.
