# Failing-first active-operation contract

- Run label: `SB04-INV-01-FAIL-001`
- Working directory: `repo://`
- Command: `dotnet test tests/Solutions/CanDoItAll.Tests.Unit.slnx --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatActiveOperationProjectionTests"`
- Exit code: `1`

```text
LlmChatProviderRuntimeTests.cs(404,51): error CS1061: LlmChatConversationEngineState does not contain a definition for ActiveOperationId
LlmChatProviderRuntimeTests.cs(407,31): error CS1061: LlmChatConversationEngineState does not contain a definition for ActiveOperationId
LlmChatProviderRuntimeTests.cs(413,37): error CS1061: LlmChatConversationEngineState does not contain a definition for ActiveOperationId
LlmChatProviderRuntimeTests.cs(425,33): error CS1061: LlmChatConversationEngineState does not contain a definition for ActiveOperationId
LlmChatProviderRuntimeTests.cs(437,31): error CS1061: LlmChatConversationEngineState does not contain a definition for ActiveOperationId
```

The new behavior test could not compile before the production contract existed. This transcript covers `SB04-INV-01`, `SB04-INV-02`, `SB04-INV-03`, and `SB04-INV-05`.
