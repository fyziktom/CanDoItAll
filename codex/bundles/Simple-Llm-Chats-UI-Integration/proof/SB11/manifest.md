# Proof Manifest — SB11

- Status: `Completed`.
- Proof tier: `Governed`.
- Owned requirements: `SCUI-008`, `SCUI-009`, `SCUI-024`, `SCUI-025`, `SCUI-049`, `SCUI-050`, `SCUI-051`, `SCUI-052`, `SCUI-053`, `SCUI-054`, `SCUI-055`, `SCUI-056`, `SCUI-057`, `SCUI-059`, `SCUI-062`.
- Start commit: `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`.
- Candidate identity: working-tree candidate based on `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`; commit skipped because repository signing remained unavailable and the user explicitly authorized continuing without bundle commits.
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Semantic contract: `bundle://proof/SB11/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB11/architecture-gate.md`.
- Execution report: `bundle://proof/SB11/execution-report.md`.

## Source and proof identity

- New neutral shell: `src/UI/CanDoItAll.Conversations.Shell/**`.
- Agent adapter: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentFloatingConversationContent.*`, `Services/AgentConversationShellContributor.cs`, and `Services/AgentChatLauncherCompatibilityFacade.cs`.
- Simple Chat adapter: `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationShellContributor.cs`, `LlmChatFloatingConversationContent.*`, `LlmChatFloatingHistoryDialog.razor`, and `LlmChatFloatingArchiveDialog.razor`.
- Streaming concurrency boundary: `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/FreshScopeLlmChatOperationEvidenceSink.cs` and the conversation-engine composition in `LlmChatsPersistenceServiceCollectionExtensions.cs`.
- Host composition: `src/App/CanDoItAll.Web/Components/Layout/MainLayout.razor`, `Program.cs`, project references, and solution membership.
- Focused tests: `ConversationShellHostTests`, `LlmChatConversationShellContributorTests`, `FloatingAgentChatHostLifecycleTests`, `AgentActiveChatPresentationMapperTests`, and `LlmChatDurableStreamEventTests`.
- Architecture snapshot: `snap-20260817135622-788ba255`.
- Static impact selection: the actual-diff request remained non-responsive and was terminated. Conservative named, non-zero selectors spanning Components and Unit were run instead.
- Browser viewport: 1600x1000.
- Simple Chat screenshot: `bundle://proof/SB11/screenshots/simple-chat-floating-cancelled.png`.
- Agent screenshot: `bundle://proof/SB11/screenshots/agent-chat-floating-completed.png`.

## Validation matrix

- Shell/Simple Chat component selection: 4 passed, 0 failed, 0 skipped.
- Agent presentation component selection: 6 passed, 0 failed, 0 skipped.
- Streaming and operation unit selection: 38 passed, 0 failed, 0 skipped.
- Agent architecture and backend-composition unit selection: 4 passed, 0 failed, 0 skipped.
- Web build after the final concurrency and layering fixes: pass, 0 warnings, 0 errors.
- Real browser, floating Simple Chat: exact response, long streaming, hide while running, reopen with durable transcript, and explicit cancellation pass.
- Real browser, floating Agent chat: exact response, preserved transcript across keep-active/reopen, and explicit stop pass. The `/chats` surface has no Agent context position, so the preserved explicit Detach control was used before dispatch.
- Static whitespace check: `git diff --check` passes.
- Components MCP: required lookup attempted but unavailable because the configured transport was closed; implementation used already-established repository wrappers and composition patterns.

## Progression

CP3 passes. SB12 is unlocked. Full Stable and full Playwright execution remain deferred to their one authorized run in SB12.

The manifest omits its own self-referential digest. Its integrity is checked by the bundle validator.
