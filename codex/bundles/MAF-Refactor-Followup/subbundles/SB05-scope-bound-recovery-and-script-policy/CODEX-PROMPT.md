# Codex execution prompt — SB05

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB05` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Eliminate all remaining reads and policy inspections that use MafAgentRuntime construction scope instead of the effective run scope.

## Owned tasks

1. Change MafStreamingTurnExecutor recovery evidence construction to use the run-owned WorkspaceRuntimeServices or a read-only recovery service from that bundle.
2. Remove new WorkspaceFileService(workspaceRoot, workspaceScope) from recovery readers.
3. Create MafScriptPolicyInspectionService per runtime build from the effective WorkspaceExecutionScope, or inject a scope-bound inspection service from the bundle.
4. Verify managed-root mapping, external-target alias resolution, and child-script inspection use the same authority and scope as the invoked command tool.
5. Audit image/document/spreadsheet/MCP helpers for any remaining captured base scope.
6. Add project-turn-on-organization-runtime tests for normal tool read, script inspection, provider-failure recovery, and finalizer recovery.
