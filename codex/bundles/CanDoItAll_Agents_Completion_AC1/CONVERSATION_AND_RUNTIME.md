# Conversation, runtime and floating adjuncts

The transcript and composer already have real isolated surfaces. S14/S15 establish that the large AgentChatPanel/ChatWorkspacePanel hosts mainly bind native state, tool argument policy, activity, attachments and effects. This run completes the remaining product-visible adjuncts without relocating or duplicating those authorities.

## Actual residual views

`AgentRuntimeDetailsDialog` renders selected run, provider failure context, request compatibility evidence, timeline and metrics (S16). `AgentExecutionLogDialog` renders ordered persisted entries, highlight, safe copy text and duration (S17). Extract these real controls into the light family and keep public compatibility wrappers only where callers need them. Move or reuse the appropriate **safe** pure presentation mappings, not Core's policy/service graph. Truncation, UTC timestamps and credential/path redaction remain enforced. Copy must use the same sanitized text shown on screen; raw payload is not an alternative fallback.

Floating content's context/affinity strip is an actual residual view around native `IAgentChatContextRegistry`/`IAgentConversationContextService` (S18). Make its badges, pending-context notice and Follow/Detach command a typed presentation. The native host still knows conversation/session/handle keys, exact contextual source and active lease. Test identity captured at click, not just the newest Chat parameter after an await.

Inspect and extract active remaining switch/history/thread selection/close confirmations discovered by A1. An already-isolated descendant stays in place. The legacy ScenarioHarness is not automatically a product screen. Trace dynamic registrations and deliberate developer routes before deciding.

## Guardrails

Keep durable execution and projected activity separate. Activity streams are typed/sequenced, show gaps and terminal/evicted/unknown states, and do not hydrate canonical approvals or transcripts (S23). A status renderer must not create new readers during rerender; old readers stop and dispose after unwind. A canceled contextual subscription does not cancel the admitted durable run.

Dialogs capture agent, session, run, highlighted entry, generation and parent token. Delayed results/close callbacks cannot act on a successor even if the same public agent ID is selected again. Ordinary close, detached/following mode and cancel-run preserve their distinct existing meanings. Do not replace all windows or close unrelated module presentations.

Attachments must use actual InputFile byte limits and the existing upload/staging owner. Preserve exact file/path grants, authorization, storage selection, and cancellation after accepted upload. A stale upload may need to release its own handle; it cannot append to another conversation. No renderer may launch an arbitrary path or reconstruct a missing grant from display text.

Approvals keep original run/proposal/tool arguments and stage. There is no new auto-approve convenience. New-thread, cancel-pending and recover-original controls all use native admission and retain known/unknown effects; do not normalize an unknown operation into a fresh Send.

## Proof

Use same light surfaces in source/published sandbox for waiting/streaming/completed/failure/unknown/partial states, long lines, multiple simultaneous views, modal highlighting and sanitized copy. Native tests cover standard/floating/contextual callers, held approvals, cancel and close, model/agent switch, exact old/new sessions and profile/navigation changes. Observe faulted owned tasks rather than hiding fire-and-forget exceptions.

Final browser proof includes a current Agent chat with an actual granted and denied tool invocation, exact saved transcript/run/logs and file bytes; a floating chat opened from two different source contexts; Detach/Follow affecting the next turn only as current policy specifies; and runtime/details/Usage/History overlays closed independently while another conversation remains usable.
