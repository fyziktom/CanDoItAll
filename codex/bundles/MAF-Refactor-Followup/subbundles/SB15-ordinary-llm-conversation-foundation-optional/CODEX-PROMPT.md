# Codex execution prompt — SB15

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB15` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Create the application-level transcript and conversation semantics needed for a future plain LLM chat without coupling it to agents or MAF.

## Owned tasks

1. Define ILlmConversationService above ILlmInvocationPort. The canonical source of truth is an application transcript with user/assistant/system records and usage, not provider-native conversation state.
2. Define conversation identity, provider/model snapshot, title, created/updated times, transcript revision, and optional opaque provider acceleration state envelope.
3. Implement atomic append/admit semantics preventing two concurrent turns from corrupting transcript order.
4. Keep tools, memory, agent catalog, workspace authority, approvals, finalizers, handoffs, and process semantics absent.
5. Add bounded context-window selection/summarization seams but do not implement heuristic destructive summarization without an explicit policy.
6. Provide an application service and persistence tests only; no product UI is required in this subbundle.
