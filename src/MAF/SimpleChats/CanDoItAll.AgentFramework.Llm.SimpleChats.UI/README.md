# Simple Chats presentation UI

This Razor class library owns the definition catalog, definition editor, conversation transcript/composer and Start/Rename/Archive dialog presentation. Immutable values and typed intents cross the boundary. It references BaseLib and Conversations.Components and has no gateway, authorization, persistence or runtime dependency.

SimpleChats.Components owns the effect hosts, catalog and editor sessions, application-item projection and conversation controller/follower. Filters and cursor reads remain server-side. Search debounce is 250 ms; replacing filters immediately cancels old reads and appends. Each operation disposes its own cancellation source when it finishes. Public catalog errors are fixed messages selected by `DefinitionCatalogFailure`; raw exceptions and editor prompt/schema data do not enter catalog presentation.

The host observes route targets for its full lifetime. Same-ID echoes are inert, clearing a route-owned editor closes it, and an unchanged null route preserves local Create. Editor callbacks are accepted only from the currently owned editor.

From the repository root:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.UI/CanDoItAll.AgentFramework.Llm.SimpleChats.UI.csproj -c Release /m:1
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --filter "FullyQualifiedName~DefinitionCatalog|FullyQualifiedName~LlmChatDefinitionUiTests" /m:1
```

The existing UI sandbox exposes `/agents?specimen=simple-chat-definitions` in Parity and Fast modes. Its eight typed scenarios update presentation and an intent log only; they instantiate no editor or production services.

## Conversation dialogs

`LlmChatConversationDialogSurface` uses the shipped Dialog, participant picker/cards,
TextBox and DangerActionDialog. It renders the complete Start, Rename and Archive forms.
Each intent retains the original generation, dialog kind and conversation identity.
Callbacks from a closed, replaced or disposed opening cannot mutate a later form.
The native workspace separately checks the same origin and current mutation state.

The workspace retains authorization, definition revision pinning, transcript concurrency,
persistence, selected-route reconciliation and operation following. Closing a dialog
does not cancel a durable response. No native service is injected into the renderer.

The existing AgentFramework sandbox exposes `/simple-chat-dialogs?scenario=Start` with
Start, Empty, Loading, Busy, Rename, Archive and Independent scenarios. The independent
state owners exercise actual filtering, paging intents, exact archive confirmation,
focus, footer access and two simultaneous dialogs without native writes.

Build the affected UI/native/sandbox projects and owning tests before discovery.
`ConversationDialogSurfaceTests` belongs to AgentFramework.UI.Tests;
`LlmChatConversationWorkspaceTests` belongs to the native Components suite;
`LlmChatUiRegistrationAndArchitectureTests` belongs to Unit. The latter scans the current
source for forbidden services and dependencies. Native browser create/send/rename/archive
and reload are separate from those controlled owner tests. See the
[AC1 proof record](../../../../docs/architecture/agents-completion-ac1.md).
