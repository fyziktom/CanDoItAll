# Codex execution prompt — SB11

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB11` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Expose the application-owned proposal model to users and bound all in-memory continuation state without weakening fail-closed behavior.

## Owned tasks

1. Render each pending proposal with tool name, classification, safe details, target scope/resource summary, and independent approve/reject choice.
2. Submit IReadOnlyList<PendingToolApprovalDecision> from UI and add a decision-list HTTP endpoint or request version. Keep the bool API only as a compatibility mapper that is clearly documented as all-proposals.
3. Require exact coverage through AgentApprovalDecisionMismatchException and preserve the original proposal arguments hash/binding.
4. Add bounded TTL/size and durable-run reconciliation to the MAF approval cache; prefer reconstruction from persisted compatible session state over process-lifetime cache authority.
5. Add an abandoned WaitingOnTool reconciliation/expiry policy that can release turn-context lease capacity without auto-approving, replaying, or losing audit evidence.
6. Resolve the explicit lease-token test conflict. Recommended default: AutoApprovedNonInteractive must not expose explicit project lease tokens; scripted harnesses should model GovernedProcessAutomation when tokens are required.
7. Add multi-proposal mixed-decision component and API integration tests.
