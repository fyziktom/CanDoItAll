# Codex execution prompt — SB00

You are the senior C# architecture implementer for `fyziktom/CanDoItAll`. Execute only `SB00` at xHigh/deep reasoning. Read the root review, findings, execution order, this README, SharedInfo skills, current source/project files, and CodeAnalytics evidence. Add failing-first tests, implement the smallest cohesive fix at the owning boundary, run focused build/tests and architecture guards, update proof/SESSION-HANDOFF.md, and return the required closure report. Never widen UI authority, recapture current UI context for continuation, mix workspace scopes, silently replay state, reintroduce process semantics in MAF, use full agents for lightweight LLM, add partial architecture, or expand failure allow-lists. Do not commit/push without explicit instruction.

## Mission

Independently establish the maf-refactor branch state and reproduce every merge-blocking review finding before production fixes begin.

## Owned tasks

1. Verify the exact branch head and compare it with development; stop if HEAD differs from the bundle baseline until the evidence map is refreshed.
2. Build CanDoItAll.slnx in Release and run Unit, Components, and Integration projects without expanding any accepted-failure list.
3. Create a focused CodeAnalytics snapshot covering AgentFramework Core, Runtime.Abstractions, MAF, LLM, Workflows, Modules.AgentFramework, Modules.Processes, Workbench, Security, and tests.
4. Add failing characterization tests for FR-001 through FR-006: authority permissions not consumed, unknown-source scope grant, project-turn recovery using base scope, script inspection using base scope, envelope-wrapped conversationId, and fingerprint-policy mismatch.
5. Add lifetime probes that prove how many LocalWorkspaceProcessHost and WorkspaceRuntimeServices instances are created/disposed per profile workspace.
6. Reproduce the explicit project-lease test conflict and record which production purpose each test is actually modeling.
7. Produce a baseline test/failure inventory with each failure categorized as pre-existing, refactor regression, environment-only, or unresolved.
