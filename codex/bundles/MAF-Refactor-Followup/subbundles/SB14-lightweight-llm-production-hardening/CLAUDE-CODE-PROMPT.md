# Claude Code execution prompt — SB14

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Make ILlmInvocationPort safe for workflows today and a stable foundation for ordinary LLM chat later, without agent/session/tool construction.
</mission>

<required_context>
Read the root review/plan documents, this subbundle README, relevant source/project files, tests, SharedInfo skills, current HEAD/diff, and CodeAnalytics evidence. Do not trust the bundle over changed source.
</required_context>

<constraints>
- Keep source-code comments and identifiers in English.
- Do not add partial-class architecture, nested architecture owners, broad Helpers/Managers, or a Common dumping ground.
- Do not let UI observation, route, prompt text, payload JSON, or current navigation grant authority.
- Do not recapture current UI context or authority during approval continuation.
- Do not duplicate provider, tool, process, or persistence side effects for comparison.
- Do not restore product/process semantics or product module references to MAF.
- Do not make lightweight LLM calls use the full agent runtime.
- Do not add new accepted test failures or exclusions.
- Do not commit, push, or open a PR unless explicitly requested.
- Work only on SB14; do not opportunistically implement later subbundles.
</constraints>

<workflow>
1. Create/update proof manifest and session handoff.
2. Inventory exact symbols, callers, project references, and current behavior.
3. Add characterization/failing tests first.
4. Implement the smallest cohesive owner-boundary change.
5. Build and run focused tests after each cutover step.
6. Exercise at least one negative/fault path.
7. Run architecture guards and inspect the diff for old-path survival.
8. Update proof/handoff continuously.
9. Return the closure output required by the README.
</workflow>

<owned_tasks>
1. Make request collections and attachment bytes immutable/defensively copied and enforce attachment count, per-item size, aggregate size, content type, and message length limits.
2. Clarify model selection: either allow empty model for provider default or require explicit model consistently; remove dead fallback behavior.
3. Validate ordered messages and define system-message placement policy without silently changing semantic order. Require at least one user input for ordinary invocation unless a named use case permits otherwise.
4. Add operation/correlation ID, absolute deadline or bounded timeout, and cancellation semantics.
5. Introduce typed sanitized LlmInvocationException/failure categories while retaining protected inner diagnostics. Raw provider exception messages must not cross public/workflow boundaries.
6. Add one bounded retry for a fully empty, non-actionable stateless response. Never retry after any tool/hosted action because this port has no tools by contract.
7. Map cached/reasoning/total usage consistently across OpenAI, Azure, Ollama, and future providers.
8. Move MafWorkflowLlmComponentInvoker to a neutral workflow runtime/provider project and move ILlmInvocationPort registration into Llm.ProviderRuntime/hosting. The MAF workflow backend may depend on the neutral invoker contract, not own it.
9. Add provider-driver and workflow integration parity tests, including empty response, malformed JSON, timeout, cancellation, and sanitized failures.
</owned_tasks>
