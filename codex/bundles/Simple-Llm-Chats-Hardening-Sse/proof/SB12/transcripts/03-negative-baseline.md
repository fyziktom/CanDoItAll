# Negative baseline evidence

The reviewed feature commit `16b6aa4b60dc88a6134dd6c9c9e634c064ac5847` contains both shallow paths
that the final architecture guard now rejects:

```text
LlmChatOperationApplicationService.cs:103: var turn = await conversationEngine.SendAsync(
EfLlmConversationStore.cs:8: public sealed class EfLlmConversationStore(IDbContextFactory<AppDbContext> dbContextFactory)
EfLlmConversationStore.cs:16,44,62,149,170: dbContextFactory.CreateDbContextAsync(...)
```

Commands:

```powershell
git grep -n 'conversationEngine.SendAsync' 16b6aa4b60dc88a6134dd6c9c9e634c064ac5847 -- src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs
git grep -n -E 'IDbContextFactory|CreateDbContextAsync' 16b6aa4b60dc88a6134dd6c9c9e634c064ac5847 -- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Conversations/EfLlmConversationStore.cs
```

Both searches return the markers above. The equivalent current-source searches return no match. The
guard additionally requires the replacement owners: dispatcher signal/host registration, `202`
admission, and the shared scoped `AppDbContext` store. This is semantic negative evidence rather than a
test-count proxy.
