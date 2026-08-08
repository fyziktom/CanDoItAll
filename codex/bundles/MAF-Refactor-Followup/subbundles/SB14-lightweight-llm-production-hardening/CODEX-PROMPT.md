# Codex execution prompt — SB14

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB14` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Make ILlmInvocationPort safe for workflows today and a stable foundation for ordinary LLM chat later, without agent/session/tool construction.

## Owned tasks

1. Make request collections and attachment bytes immutable/defensively copied and enforce attachment count, per-item size, aggregate size, content type, and message length limits.
2. Clarify model selection: either allow empty model for provider default or require explicit model consistently; remove dead fallback behavior.
3. Validate ordered messages and define system-message placement policy without silently changing semantic order. Require at least one user input for ordinary invocation unless a named use case permits otherwise.
4. Add operation/correlation ID, absolute deadline or bounded timeout, and cancellation semantics.
5. Introduce typed sanitized LlmInvocationException/failure categories while retaining protected inner diagnostics. Raw provider exception messages must not cross public/workflow boundaries.
6. Add one bounded retry for a fully empty, non-actionable stateless response. Never retry after any tool/hosted action because this port has no tools by contract.
7. Map cached/reasoning/total usage consistently across OpenAI, Azure, Ollama, and future providers.
8. Move MafWorkflowLlmComponentInvoker to a neutral workflow runtime/provider project and move ILlmInvocationPort registration into Llm.ProviderRuntime/hosting. The MAF workflow backend may depend on the neutral invoker contract, not own it.
9. Add provider-driver and workflow integration parity tests, including empty response, malformed JSON, timeout, cancellation, and sanitized failures.
