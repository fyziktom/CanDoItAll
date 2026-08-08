# Codex execution prompt — SB08

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB08` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Make runtime state restore judge and inspect the correct payload while preserving fail-closed approval continuation.

## Owned tasks

1. Separate eligibility evaluation from payload inspection: parse envelope, run compatibility policy, unwrap through IAgentRuntimeStateAdapter, then inspect MAF payload fields such as conversationId.
2. Never inspect an envelope wrapper as if it were native MAF session JSON.
3. Persist and compare the effective history mode used for the runtime build, not only the agent configured preference.
4. Define restore behavior for transient context + provider-managed conversation, framework-managed local history, service-managed Responses history, and approval continuation.
5. Replace any-well-formed-JSON legacy recognition with a strict MAF legacy payload recognizer and bounded migration fixture set.
6. Keep approval continuation fail-closed when compatible native state is unavailable; ordinary sends may use explicit canonical transcript replay only when policy allows it.
7. Add v0 raw, v1 envelope, malformed, foreign adapter, provider/model mismatch, and wrapped conversationId tests.
