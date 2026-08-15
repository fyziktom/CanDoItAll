# SB02 — API Contract Correctness And Transport Ownership

## Status

- `Ready`

## Objective

- Make the public/read/manage HTTP boundary complete, confidential, consistently validated, and locally maintainable without changing product/project boundaries.

## Success Criteria

- Empty GUIDs, invalid paging/cursors, and unknown JSON members return stable `llm-chat.invalid-request` Problem Details.
- System messages never appear in read-scoped transcript pages; manage scope can read authoritative editor state through the new editor route.
- Request fingerprint is absent from public operation JSON and sanitized invocation projection is prepared in its SB05-owned contract slot.
- Definition/conversation endpoint handlers have distinct internal owners with identical routes/names/policies.
- Unused operation-kind values and stale documentation contract names are removed/corrected.

## Covered Inputs

- BC-006, BC-010 through BC-019.

## Prerequisites

- SB01 CP0 `Pass`.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/LlmChatsApi.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatApiContracts.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatApiMapper.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatApiResults.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationApiContracts.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/ReadModels/EfLlmChatConversationReadStore.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Operations/LlmChatOperation.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs`
- `repo://docs/llm-chats-api.md`

## UI Composition Contract

- N/A — backend HTTP only; no UI consumer is added.

## Deliverables

- Stable route/query/body validation helpers used by both endpoint owners.
- `GET /api/llm-chats/{definitionId}/editor` manage-only DTO/mapper/metadata/ETag.
- Query-level exclusion of system transcript messages.
- Thin `LlmChatsApi` coordinator plus definition/conversation owner files.
- Operation response fingerprint removal, operation-kind cleanup, OpenAPI/docs correction.

## Dependency Impact

- Blocks lifecycle/SSE host proof because later work consumes these public DTO/error/security contracts.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solution: `repo://tests/Solutions/CanDoItAll.Tests.Integration.slnx`.
- Filter: exact union of new/current methods in `LlmChatsDefinitionApiIntegrationTests`, `LlmChatsConversationApiIntegrationTests`, `LlmChatsSecurityApiIntegrationTests`, operation API integration owners, and `LlmChatPersistenceIntegrationTests`.
- Selection reason: model binding, auth metadata, Problem Details, query paging, and response serialization require the real Web host.
- Expected named cases: `Empty_definition_conversation_operation_and_filter_ids_return_stable_invalid_request`, `Explicit_invalid_page_sizes_return_stable_invalid_request`, `Unknown_members_return_stable_invalid_request_problem_details`, `Read_scope_excludes_system_messages_while_provider_context_retains_prompt`, `Manage_editor_returns_authoritative_prompt_without_provider_secrets`, `Editor_requires_manage_scope`, `Operation_response_omits_internal_request_fingerprint`, `Endpoint_split_preserves_routes_names_and_policies`, existing `AuthorizationEnabledHost_EnforcesDistinctScopesAndAuthenticatesSseOnlyThroughBearerHeader`, existing `ConversationApi_OwnsApiOriginAndRejectsClientOriginSpoofing`, `OpenApi_declares_every_implemented_llm_chat_problem_status`, and `Unknown_persisted_operation_kind_fails_as_storage_corrupted` (12 cases).
- Invalidation keys: Web route/DTO/mapper/results, read model, operation enum, auth policies, API docs.
- Broad-gate decision: deferred to SB10 for public Web/shared persistence changes.

## Implementation Steps

1. Add common non-empty typed-ID and strict paging validation at the Web boundary for definition, conversation, operation, and optional definition-filter IDs; do not weaken typed constructors.
2. Ensure binder/model-state failures use the same stable Problem Details code as handler validation.
3. Filter `System` role in the database query before ordering/cursor/`Take`.
4. Add the manage-only editor route and allowlisted DTO; preserve read DTO redaction and ETag behavior.
5. Split definition/conversation endpoint owners while retaining the existing map entry point and endpoint identities.
6. Remove public request fingerprint and unused `Cancel`/`Recover` kind values; keep `SendTurn = 0`.
7. Correct OpenAPI metadata and docs, including the full implemented error-status set and the real definition details contract name.
8. Re-prove exact read/manage/execute scopes and server-owned `Api` origin while exercising the refactored owners.
9. Insert an impossible persisted operation kind and prove explicit storage-corruption handling while the public enum exposes only `SendTurn = 0`.
10. Build Core, Persistence, and Web; list and run the exact host/PostgreSQL cases; run documentation validation for changed docs.

## C# Architecture Impact

- Local Web responsibility extraction and a product read-query change; no project/reference change.

## Boundary Ownership

- Web validates/projects; Persistence filters query rows; Core retains typed IDs and operation kinds.

## Dependency Direction

- Web continues to depend on Core/Composition only, never Persistence/EF.

## Pattern Decision

- PSR-01 and PSR-10; no generic endpoint framework or shared full DTO.

## Testability Contract

- Real host with actual auth/model binding; prompt sentinel must be absent from every read response but present in deterministic provider context.

## Partial Class Policy

- Endpoint split uses distinct non-partial internal types.

## Architecture Proof Required

- Source assertion for unchanged route names/policies, no Web-to-Persistence reference, no system role in public query, and no fingerprint JSON property.

## Scope Exceptions

- Invocation collection fields are completed in SB05; SB02 only removes the fingerprint and preserves a compatible extension point.
- Conversation-create idempotency remains explicitly deferred.

## Do Not Do

- Do not return system prompt under read scope or reuse mutation DTO as a broad response.
- Do not catch typed-ID constructor exceptions globally.
- Do not add UI or a new project/interface.

## Acceptance Checklist

- [ ] Twelve named host/PostgreSQL cases discover and pass.
- [ ] Read/manage authorization and secret sentinels are proven.
- [ ] Route names/templates unchanged except the additive editor route.
- [ ] Core/Persistence/Web Release builds pass.
- [ ] Documentation check passes.

## Proof Required

- Failing-first and passing host transcripts, exact discovery, serialized Problem Details/DTO samples with secret scan, changed project builds, route/OpenAPI snapshot, and source assertions under `proof/SB02`.

## Browser Validation Logging

- N/A — HTTP host only; no rendered UI.

## Progression Gate

- SB03 starts only after public validation/privacy and endpoint ownership proof pass.

## Reopen Triggers

- Any later Web route/DTO/auth/read-query/error mapping change reopens SB02 and affected SB04/SB05/SB09/SB10 host proof.

## Suggested Agent Prompt

```text
Execute SB02 only. Repair the HTTP/privacy boundary and split local endpoint ownership with the smallest change set. Use real-host failing-first proof and stop if system prompt or raw internal identifiers remain externally observable.
```
