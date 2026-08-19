# Requirements

- **SCUI-001** — Freeze execution against the actual simple-chats start commit and record drift from the reviewed head.  
  Owner(s): `SB01`
- **SCUI-002** — Reconcile the predecessor UI-refactor status, checksums, ignored proof paths, and closure claims before dependent work.  
  Owner(s): `SB01`
- **SCUI-003** — Record the user-provided manual regression evidence without overstating untested scenarios.  
  Owner(s): `SB01`
- **SCUI-004** — Do not activate any Simple Chat browser surface before the mandatory CP1 hardening checkpoint passes.  
  Owner(s): `SB01, SB05`
- **SCUI-005** — Preserve the currently proven Agent settings, main chat, floating chat, Process chat, and Project Structure context behavior.  
  Owner(s): `SB01, SB05, SB12`
- **SCUI-006** — Make reusable presentation collections defensive immutable snapshots rather than caller-owned IReadOnlyList references.  
  Owner(s): `SB02`
- **SCUI-007** — Normalize and bound ConversationPresentationKey values while preserving existing agent key round-trips.  
  Owner(s): `SB02`
- **SCUI-008** — Replace hard-coded active-item Open/Stop semantics with source-neutral declared action descriptors.  
  Owner(s): `SB02, SB11`
- **SCUI-009** — Map existing Agent active-chat Open and Stop actions through the generic action contract with unchanged behavior and accessibility.  
  Owner(s): `SB02, SB05, SB11`
- **SCUI-010** — Allow the transcript to render a bounded collection of transient messages rather than one user-specific pending message.  
  Owner(s): `SB03`
- **SCUI-011** — Derive message alignment, avatar, color, copy behavior, and role label from message role, not from transient state.  
  Owner(s): `SB03`
- **SCUI-012** — Support a transient streaming Assistant message that can grow incrementally without becoming canonical transcript state.  
  Owner(s): `SB03, SB09`
- **SCUI-013** — Represent Pending, Streaming, Failed, and Cancelled display states with explicit status text and accessibility semantics.  
  Owner(s): `SB03`
- **SCUI-014** — Never persist or append partial streamed text as a canonical assistant transcript entry from the UI.  
  Owner(s): `SB03, SB09`
- **SCUI-015** — Continue to disable raw HTML in rendered conversation Markdown.  
  Owner(s): `SB03`
- **SCUI-016** — Neutralize unsafe Markdown link and image URI schemes and prove safe relative, fragment, http, https, and mailto behavior.  
  Owner(s): `SB03`
- **SCUI-017** — Preserve existing Agent hidden-context parsing, approvals, execution, voice, attachments, token metadata, copy, focus, and auto-scroll behavior.  
  Owner(s): `SB02, SB03, SB05, SB12`
- **SCUI-018** — Expose the exact active operation identity in conversation engine/application state when an active turn exists.  
  Owner(s): `SB04, SB09`
- **SCUI-019** — Propagate ActiveOperationId through internal details and the additive HTTP conversation response without exposing system prompt content.  
  Owner(s): `SB04`
- **SCUI-020** — Keep ActiveOperationId profile-fenced and prove it cannot reference another conversation or database-profile generation.  
  Owner(s): `SB04`
- **SCUI-021** — Use the existing application-level LlmChatOperationEventStreamSession boundary as the durable event source.  
  Owner(s): `SB04, SB05, SB06, SB09`
- **SCUI-022** — Do not implement the server-side Blazor UI by making loopback HTTP/SSE calls to its own Web host.  
  Owner(s): `SB05, SB06, SB09`
- **SCUI-023** — On replay retention gaps or invalid local cursors, discard transient partial output and refresh authoritative operation/transcript state.  
  Owner(s): `SB04, SB09`
- **SCUI-024** — Disposing, hiding, navigating away from, or disconnecting a UI follower must not cancel the durable LLM operation.  
  Owner(s): `SB04, SB05, SB06, SB09, SB11`
- **SCUI-025** — Cancellation must occur only through the explicit operation-cancel command and remain distinct from window close or archive.  
  Owner(s): `SB04, SB06, SB09, SB11`
- **SCUI-026** — Create a dedicated CanDoItAll.Modules.LlmChats.Ui Razor project for product UI and presentation mapping.  
  Owner(s): `SB06`
- **SCUI-027** — Keep the Simple Chat UI project free of EF Core, Persistence, Web API DTO, AgentFramework Core, tools, skills, voice, and agent execution references.  
  Owner(s): `SB06, SB12`
- **SCUI-028** — Register the UI module, Razor assembly, and required services only through focused composition extensions and assembly markers.  
  Owner(s): `SB06, SB10`
- **SCUI-029** — Gate read, manage, and execute UI actions with authorization semantics aligned to the existing LLM Chat API policies.  
  Owner(s): `SB06, SB10, SB12`
- **SCUI-030** — Expose route and shell navigation only after definitions, conversations, and operation lifecycle behavior are functionally proven.  
  Owner(s): `SB06, SB10`
- **SCUI-031** — Use one explicit large-desktop scroll owner and preserve the application first-viewport composition at 1600x1000 or maximized desktop.  
  Owner(s): `SB07, SB08, SB10, SB12`
- **SCUI-032** — List and filter Simple Chat definitions across Draft, Active, Suspended, and Archived states.  
  Owner(s): `SB07, SB10`
- **SCUI-033** — Create and edit definition name, avatar, summary, system prompt, and revision reason through reusable editor components.  
  Owner(s): `SB07`
- **SCUI-034** — Edit tags and preserve definition identity/revision metadata without adding deployment-owned fields.  
  Owner(s): `SB07`
- **SCUI-035** — Load provider/model choices through ILlmChatProviderResolver and keep provider SDK types out of the UI boundary.  
  Owner(s): `SB06, SB07`
- **SCUI-036** — Edit temperature, supported thinking effort, and timeout with provider/model capability-aware validation.  
  Owner(s): `SB07`
- **SCUI-037** — Expose Text, JSON, and JSON Schema response-format settings in an explicit advanced section.  
  Owner(s): `SB07`
- **SCUI-038** — Honor concurrency tokens, immutable revisions, status transitions, and reload-on-conflict behavior.  
  Owner(s): `SB07`
- **SCUI-039** — Create a conversation only from an Active definition and pin the authoritative definition revision.  
  Owner(s): `SB08, SB10`
- **SCUI-040** — List, search, and keyset-page conversations without materializing unbounded transcripts.  
  Owner(s): `SB08, SB10`
- **SCUI-041** — Select a conversation and page its canonical transcript without displaying persisted System messages.  
  Owner(s): `SB08, SB10`
- **SCUI-042** — Rename and archive conversations with optimistic concurrency and stable user-visible conflict handling.  
  Owner(s): `SB08, SB10`
- **SCUI-043** — Keep system prompts available only in manage-scoped definition editor state, never in read-scoped transcript presentation.  
  Owner(s): `SB07, SB08, SB12`
- **SCUI-044** — Send each logical user turn with a stable operation id and the expected transcript revision.  
  Owner(s): `SB08, SB09, SB10`
- **SCUI-045** — Render the admitted user turn as a transient pending projection until authoritative transcript refresh.  
  Owner(s): `SB08, SB09`
- **SCUI-046** — Render coalesced llm.response.delta events as one transient Assistant message.  
  Owner(s): `SB09, SB10`
- **SCUI-047** — On terminal success, failure, cancellation, or recovery-required state, refresh authoritative operation and transcript state and remove transient projections.  
  Owner(s): `SB09, SB10`
- **SCUI-048** — Recover active operations after browser refresh, circuit reconnection, profile switch, and component remount without redispatch.  
  Owner(s): `SB09, SB10, SB12`
- **SCUI-049** — Create an application-wide conversation-shell boundary for unified floating catalog composition without moving product orchestration into presentation components.  
  Owner(s): `SB11`
- **SCUI-050** — Provide an Agent floating contributor that preserves context access filtering, affinity, history, lifecycle, and focused AgentChatPanel behavior.  
  Owner(s): `SB11`
- **SCUI-051** — Provide a Simple Chat floating contributor that never receives ambient project context automatically.  
  Owner(s): `SB11`
- **SCUI-052** — Offer All, Agents, and Chats filters over one available-participant catalog.  
  Owner(s): `SB11`
- **SCUI-053** — Keep participant kind and lifecycle axis separate: Available and Active are not substitutes for Agents and Chats.  
  Owner(s): `SB11`
- **SCUI-054** — Render focused windows through source-owned descriptors/components while the neutral shell owns overlay placement and catalog composition.  
  Owner(s): `SB11`
- **SCUI-055** — Keep hide/close-window, stop Agent handle, cancel LLM operation, and archive conversation as distinct declared actions.  
  Owner(s): `SB11`
- **SCUI-056** — Preserve all existing floating Agent catalog/window test ids, keyboard behavior, retention, close decisions, and context badges.  
  Owner(s): `SB11, SB12`
- **SCUI-057** — Support Simple Chat floating create/open/history/stream/cancel/reopen behavior using the same durable conversation and operation state as the main page.  
  Owner(s): `SB11, SB12`
- **SCUI-058** — Display only sanitized failures and never leak provider response bodies, secrets, system prompts, or internal request fingerprints.  
  Owner(s): `SB03, SB04, SB06, SB07, SB08, SB09, SB10, SB12`
- **SCUI-059** — Do not implement Add context, Project Structure, selected-node, subtree, file, or attachment context in this bundle.  
  Owner(s): `SB08, SB11, SB12`
- **SCUI-060** — Do not add tools, skills, Memory, voice, image/file attachments, approvals, or agent execution semantics to Simple Chats.  
  Owner(s): `SB08, SB12`
- **SCUI-061** — Do not add public chatbot channels, moderation, external participants, rate limits, or deployment fields to reusable definitions.  
  Owner(s): `SB07, SB12`
- **SCUI-062** — Select tests from each actual source diff with CodeAnalytics impacted_tests_get and reject zero or unexpected discovery.  
  Owner(s): `SB01, SB02, SB03, SB04, SB05, SB06, SB07, SB08, SB09, SB10, SB11, SB12`
- **SCUI-063** — Run at most one unfiltered Stable gate, only at the frozen final checkpoint after all affected checks pass.  
  Owner(s): `SB12`
- **SCUI-064** — Finish in awaiting-user-simple-chat-ui-verification state with a precise Agent and Simple Chat manual regression checklist.  
  Owner(s): `SB12`