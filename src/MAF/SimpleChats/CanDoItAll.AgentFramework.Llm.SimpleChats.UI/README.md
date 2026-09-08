# Simple Chats Definition Catalog UI

This Razor class library owns immutable definition catalog presentation, typed filter and editor intents, the controlled catalog Surface, pure participant-card mapping and catalog CSS. It references BaseLib and Conversations.Components. It has no gateway, authorization, persistence or runtime dependency.

SimpleChats.Components owns the effect host, catalog session, application-item projection and existing definition editor. Filters and cursor reads remain server-side. Search debounce is 250 ms; replacing filters immediately cancels old reads and appends. Each operation disposes its own cancellation source when it finishes. Public catalog errors are fixed messages selected by `DefinitionCatalogFailure`; raw exceptions and editor prompt/schema data do not enter catalog presentation.

The host observes route targets for its full lifetime. Same-ID echoes are inert, clearing a route-owned editor closes it, and an unchanged null route preserves local Create. Editor callbacks are accepted only from the currently owned editor.

From the repository root:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.UI/CanDoItAll.AgentFramework.Llm.SimpleChats.UI.csproj -c Release /m:1
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --filter "FullyQualifiedName~DefinitionCatalog|FullyQualifiedName~LlmChatDefinitionUiTests" /m:1
```

The existing UI sandbox exposes `/agents?specimen=simple-chat-definitions` in Parity and Fast modes. Its eight typed scenarios update presentation and an intent log only; they instantiate no editor or production services.
