# Target Solution

## Manager Chat

- `ProcessWorkspace` owns only UI state for the selected manager agent, selected chat session, selected run id, draft prompt, pending prompt, and runtime snapshot.
- AgentFramework remains the canonical source of truth for chat sessions, messages, execution runs, approvals, metrics, and execution logs.
- Process runtime remains the canonical source of truth for runs, step runs, subprocess links, assignments, manager ids, and journal entries.
- The manager tab resolves a technical agent id by preferring the selected run manager id, then the definition manager override, then a bound manager option that matches the visible manager name.
- If no bound technical agent exists, the tab shows a clear unavailable state instead of silently selecting another agent.
- Sending a prompt uses `IAgentFrameworkWorkspaceService.ExecuteRunAsync` with `SourceKind = process-manager-chat`, `SourceId = process definition id`, and `ProcessRunId` when a run is selected.

## Run Context

- The run selector uses the existing `runs` list and does not load every run's detail tree.
- Selecting a run changes prompt context and badges only. It does not create a second transcript store.
- Context added to prompts includes process definition id/name, project id/name when present, selected run id/name/status/manager, and guidance to use process tools for reads or mutations.

## Feature/Function Subprocess

- Add a new default process template named `.NET feature/function implementation subprocess`.
- The subprocess focuses on one bounded feature/function: clarify slice, update tests first where feasible, implement, run targeted validation, and produce handoff notes.
- The existing `.NET implementation slice with atomic validation` delegates its implementation step to the new subprocess.
- The main software delivery process continues to launch the implementation slice, so the new subprocess is used inside the higher-level development process without adding dispatcher-specific logic.

## Revalidation

- After manager chat implementation, verify no new process chat persistence or duplicate manager canonical state was introduced.
- After template changes, verify default import order resolves nested subprocess references.
- After validation, review whether any agent failure belongs in process-step instructions, agent skills, or truly generic dispatcher logic.

## Final Revalidation Result

- Manager chat did not add a second transcript store. AgentFramework remains responsible for chat sessions, execution runs, approvals, tool logs, and metrics.
- Process runtime remains responsible for process definitions, process runs, subprocess links, assignments, step state, journal entries, and process artifacts.
- Subprocess assignment inheritance copies parent assignment decisions into child run assignments. It does not read live parent state as an implicit mutable fallback during every dispatch.
- Subprocess artifact projection records parent-visible artifact metadata with provenance back to the child run. It does not duplicate artifact content as a new canonical source.
- AgentFramework A2A/workflow features remain useful future execution primitives, but the current process tree should not be split into AgentFramework workflow state until CanDoItAll has a deliberate migration boundary.
- The live Pocket Pantry validation exposed a process-instruction gap for UI proof. The correct fix is in subprocess validation steps, not a dispatcher special case for Blazor ports.
