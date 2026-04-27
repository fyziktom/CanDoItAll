# 06 - Session, History, Context, and Compaction Stabilization

## Objective

Make session and context behavior explicit. Process state must remain the source of truth; MAF sessions should provide conversation continuity, not hidden workflow state.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- Process prompt/context builders.


## Required implementation tasks


1. Define a session policy model:
   - framework-managed history
   - service-managed history
   - local history
   - restore behavior
   - transcript replay behavior
   - compaction eligibility
2. Review `CreatePromptInputMessages(...)` and ensure the current prompt is always included when required.
3. Ensure restored serialized sessions do not rely on replaying full historical transcripts unless explicitly configured.
4. Add a bounded process context provider or prompt snapshot provider that injects:
   - current process state summary
   - current step contract
   - required artifacts
   - relevant prior evidence refs
   - recent tool receipts
   - approval state
5. Keep session/history out of process state decisions.
6. Review compaction behavior:
   - apply only where MAF supports it
   - avoid using `OPENAI_API_KEY` as an implicit global compaction dependency when a provider-specific config is needed
   - skip compaction for governed process runs if current behavior requires that
7. Add diagnostics for session mode, replay mode, and context providers attached.


## Required tests


Unit tests:
- Session restore path includes current prompt or intentionally omits only when documented.
- Process state summary is present in bounded context snapshot.
- Session history alone cannot determine process completion.
- Compaction is skipped when not eligible.
- Compaction provider selection is configurable or fails clearly.

Integration tests:
- Process step after approval/retry receives current step context without stale hidden-state dependency.
- Existing process mock flow still passes.


## Risks and constraints


- Changing history behavior can alter model outputs. Add focused regression tests and keep changes incremental.

