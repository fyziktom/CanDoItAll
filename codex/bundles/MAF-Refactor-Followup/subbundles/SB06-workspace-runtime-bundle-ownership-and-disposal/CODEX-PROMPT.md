# Codex execution prompt — SB06

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB06` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Ensure one workspace aggregate owns one process host and one disposable service graph for its full profile-workspace lifetime.

## Owned tasks

1. Introduce an owned workspace aggregate or extend AgentFrameworkWorkspaceService ownership so WorkspaceRuntimeServices is disposed exactly once.
2. Remove the extra LocalWorkspaceProcessHost from CanDoItAllAgentWorkspaceFactory; use the bundle process host for command execution, boundary description, lease cleanup, and recovery.
3. Define ownership for handoff participants: they may share a run bundle but only the parent build owns disposal.
4. Verify scoped DI and manually constructed profile workspaces use equivalent graphs and lifetimes.
5. On profile switch, cancel active work, persist terminal state as applicable, stop owned processes, dispose bundle, and only then expose the new workspace.
6. Add instance-count and disposal tests, including failed construction and partial handoff build.
