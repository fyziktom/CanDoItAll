# Codex execution prompt — SB09

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB09` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Make state compatibility reflect the semantic inputs that can change provider continuation behavior or authorization.

## Owned tasks

1. Design RuntimeStateEnvelope schema v2 with separate AuthorityPolicyFingerprint, ModelContextFingerprint, CapabilityPolicyFingerprint, and ToolContractFingerprint.
2. Compute authority fingerprint from the admitted canonical policy, not from UI/model-context content.
3. Compute tool-contract fingerprint from stable tool identity plus input schema, classification, approval requirement, owning provider key/version, and relevant capability policy—not names alone.
4. Decide adapter package compatibility using an explicit readable-version range or adapter migration registry; do not require exact package match unless the state format demands it.
5. Compare effective history mode and provider conversation strategy.
6. Implement registered v1-to-v2 migration and prove legacy v0 remains bounded by the policy from SB08.
7. Record compatibility reasons without raw payload data.
