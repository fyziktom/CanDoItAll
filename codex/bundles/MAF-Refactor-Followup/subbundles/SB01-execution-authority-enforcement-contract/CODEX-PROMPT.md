# Codex execution prompt — SB01

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB01` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Turn AgentExecutionAuthorityRecord from metadata/audit evidence into the single immutable permission snapshot used by capability planning and tool invocation.

## Owned tasks

1. Introduce a provider-neutral AgentExecutionGovernanceSnapshot or equivalent immutable execution contract containing authority identity, profile/generation, workspace scope, read/mutation grants, allowed operations, capabilities, aliases, policy version, and fingerprint.
2. Persist only its safe projection, but retain the full trusted snapshot through the in-process execution command and continuation lease.
3. At execution start, validate snapshot agent, profile, generation, scope, authority ID/fingerprint, and transient-context digest before creating the runtime.
4. Populate AgentRuntimeContextIntent and AgentRuntimeToolProviderContext from the governance snapshot, not from UI access entries or default-true behavior.
5. Filter mutation tools when MutationAllowed is false and read tools when ReadAllowed is false; invocation-time policy must independently enforce the same snapshot.
6. Thread allowed operations, capability scopes, external-target aliases, and managed-artifact refs from one snapshot; define monotonic intersection with agent configuration and process restrictions.
7. Add a negative production-path test showing that an agent configured with mutation tools cannot mutate when canonical authority is read-only.
