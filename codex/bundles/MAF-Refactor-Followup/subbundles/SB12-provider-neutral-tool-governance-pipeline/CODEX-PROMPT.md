# Codex execution prompt — SB12

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB12` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Make MAF a tool-call mapper over a provider-neutral governance pipeline rather than an owner of process facts and hardcoded policy.

## Owned tasks

1. Inject IAgentToolInvocationPolicy or a composed IAgentToolGovernancePipeline into MafRuntimeAgentFactory; delete direct new DefaultAgentToolInvocationPolicy().
2. Define ExecutionGovernanceSnapshot with generic resource scope, allowed operations, mutation/read grants, approval policy, external targets, managed refs, and policy fingerprint.
3. Move process-specific interpretation into a Modules.Processes contributor/decorator that narrows the generic snapshot.
4. Remove ProcessRunId/ProcessStepId/product-branch fields from MAF policy construction where they are not telemetry. Map process requirements to generic operation/resource constraints before entering MAF.
5. Make WorkspaceExecutionAuditContext telemetry-only or prove every authorization fact also arrives explicitly in the invocation command.
6. Ensure capability filtering and invocation policy use the same monotonic decision model and cannot be weakened by catalog order.
7. Expand architecture guards beyond a short token list so new process fields cannot return under different names.
