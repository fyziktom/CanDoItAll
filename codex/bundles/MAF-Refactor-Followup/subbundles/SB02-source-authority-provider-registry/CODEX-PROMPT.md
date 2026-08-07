# Codex execution prompt — SB02

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB02` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Replace the observed-compatibility grant path with explicit source authority providers and an unambiguous fail-closed default.

## Owned tasks

1. Define IAgentExecutionAuthorityProvider with a stable source-kind key and deterministic order/uniqueness validation.
2. Move project-structure authority resolution into a dedicated provider that verifies project identity/existence, agent access, current profile, and canonical project scope.
3. Add providers for every currently published context source that requires organization/project authority; inventory Projects, CRM/HR, Prompts, Workbench, Processes UI, and other context publishers before coding.
4. Unknown source kinds must resolve to no application authority or bounded read-only sandbox; they must never inherit an observed project scope.
5. Treat UiAccessHint only as an early denial optimization. A hint may reduce access but cannot select scope, grant read, or grant mutation.
6. Fence database profile generation after every asynchronous authority lookup as well as before it.
7. Add collision tests for duplicate source providers and fail-closed tests for malformed/foreign project IDs and profile switches.
