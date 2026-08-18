# Production source assertions

- Run label: `SB04-SOURCE-ASSERT-001`
- Working directory: `repo://`
- Command: `rg -n "ActiveOperationId|HasActiveTurn|activeTurnId|activeTurn\\.TurnId"` over the six changed production paths.
- Exit code: `0`

```text
src/Modules/CanDoItAll.Modules.LlmChats/Ports/LlmChatExecutionPorts.cs:12: LlmChatOperationId? ActiveOperationId
src/Modules/CanDoItAll.Modules.LlmChats/Ports/LlmChatExecutionPorts.cs:16: public bool HasActiveTurn => ActiveOperationId.HasValue;
src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs:389: new LlmChatOperationId(activeTurn.TurnId)
src/Modules/CanDoItAll.Modules.LlmChats.Persistence/ReadModels/EfLlmChatConversationReadStore.cs:161-162: ActiveTurnId maps to new LlmChatOperationId(activeTurnId)
src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationContracts.cs:77: application projection delegates to Transcript.ActiveOperationId
src/App/CanDoItAll.Web/Api/LlmChatApiContracts.cs:134: public Guid? ActiveOperationId { get; init; }
src/App/CanDoItAll.Web/Api/LlmChatApiMapper.cs:123: ActiveOperationId = details.ActiveOperationId?.Value
```

The behavior exists in production owners outside fixtures. Invariant IDs: `SB04-INV-01`, `SB04-INV-02`, `SB04-INV-03`, `SB04-INV-05`.
