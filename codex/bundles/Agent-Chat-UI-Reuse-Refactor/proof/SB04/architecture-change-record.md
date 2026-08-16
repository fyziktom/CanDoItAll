# SB04 architecture change record

- The neutral project now owns thread rail search, ordering, count display, loading/error/empty/no-match states, selected thread rows, metadata/badges/tooltips, action slots, and bounded history rendering.
- `ConversationThreadPresentation` carries an opaque `ConversationPresentationKey`; Agent session identifiers are mapped only in Agent-owned adapters.
- `AgentThreadPresentationMapper` and `AgentThreadHistoryPresentationMapper` retain Agent copy, approval/auto-approve/run-evidence projection, date formatting, preview normalization, and Guid conversion.
- `AgentChatPanel` retains workspace loading, thread creation and selection effects, busy state, approvals, provider execution, notifications, and failure logging.
- `AgentThreadHistoryDialog` remains a public compatibility façade and retains DialogService close semantics with Guid results.
- Snapshot `snap-20260816115315-acdf4779` is healthy for the three scoped projects and introduces no project cycle or blocking diagnostic.
- The two reported module/type cycles are the pre-existing Modules.AgentFramework cycles recorded by prior subbundles and do not involve the neutral project.
