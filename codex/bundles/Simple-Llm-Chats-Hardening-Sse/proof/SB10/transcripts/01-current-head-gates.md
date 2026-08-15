# Current-head gates

Repository: `C:\repositories\CanDoItAll`  
Branch: `simple-chats`  
Implementation commit: `ebb8deae5f2deb0a379875fecf853ea8fc423be7`  
Dependency mode: local sibling source projects  
Database: PostgreSQL Testcontainers for persisted origin; in-memory real hosts for policy/transport

## Expected-red semantic proof

Command:

```text
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.LlmChatsSecurityApiIntegrationTests.AuthorizationEnabledHost_EnforcesDistinctScopesAndAuthenticatesSseOnlyThroughBearerHeader" -nologo -v:minimal
```

Result against the pre-SB10 source: exit 1, 0 passed, 1 failed. The first assertion expected 403 for a
JWT containing only the broad `api` scope; the route returned 200. This is the intended semantic red:
the previous implementation authenticated only the parent API group and had no LLM Chat policies.

## Affected builds

Command (run after policy/origin/contract implementation and again after the source audit removed every
remaining raw-exception logger overload):

```text
dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal
```

Final result: exit 0, 0 warnings, 0 errors. Two affected build commands were used, within the maximum of
three. The first also passed; the second proves the final sanitized logging source.

## Focused real-host API behavior

Command:

```text
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatsDefinitionApiIntegrationTests|FullyQualifiedName~LlmChatsConversationApiIntegrationTests|FullyQualifiedName~LlmChatsSecurityApiIntegrationTests|FullyQualifiedName~LlmChatsTurnApiIntegrationTests|FullyQualifiedName~LlmChatsIdempotencyApiIntegrationTests|FullyQualifiedName~LlmChatsCancellationApiIntegrationTests|FullyQualifiedName~LlmChatsRecoveryApiIntegrationTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests" -nologo -v:minimal
```

Result: exit 0, 12 passed, 0 failed, 0 skipped in 17 seconds.

Covered behavior: exact read/manage/execute separation, broad-scope denial, auth-disabled compatibility,
origin spoof rejection, header-only SSE authentication, versioned operation response, OpenAPI transport
schema, system-prompt omission, redacted idempotency conflict, cancel, and recovery.

## Direct trusted-application origin proof

Command:

```text
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName=CanDoItAll.Tests.Unit.LlmChatConversationApplicationServiceTests.Create_pins_the_exact_current_active_revision_and_preserves_trusted_application_origin" -nologo -v:minimal
```

Result: exit 0, 1 passed, 0 failed, 0 skipped in 52 milliseconds. The product service preserves an
explicit trusted `Application` origin and the pinned definition revision.

## Real PostgreSQL persisted-origin proof

Command:

```text
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.LlmChatsApiPostgreSqlIntegrationTests.ConversationApi_PersistsServerOwnedApiOriginAndRejectsSpoofedOrigin" -nologo -v:minimal
```

Result: exit 0, 1 passed, 0 failed, 0 skipped in 8 seconds. The HTTP endpoint rejects supplied
`application` origin, creates without an origin field, returns `api`, and the authoritative PostgreSQL
`LlmChatConversationRow` stores `Api`.

## Budget record

Four filtered test commands were used: one required expected-red, one 12-case focused API union, one
exact product-owner test, and one exact PostgreSQL persistence proof. Two affected Web builds were used.
No unfiltered Unit/Integration project, solution-wide test, Playwright, LiveProcess, LongRunning, or
Quarantined lane ran. No deviation from the declared budget occurred.
