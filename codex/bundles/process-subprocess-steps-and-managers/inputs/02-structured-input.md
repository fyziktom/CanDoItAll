# Structured Input

## Functional Requirements

- Process definitions can contain a step whose type is a subprocess.
- A subprocess step references another process definition.
- Starting or advancing a subprocess step starts the referenced process as a child run.
- The parent process can report child run status, active work, blockers, failures, and completion.
- The runtime supports many concurrent process trees without creating one observer thread per subprocess.
- AI manager selection has a default behavior and a per-process manager override.
- HR matching honors the manager override automatically when a process run is created.
- Users can request manager-style reports and add manager instructions to unblock work.
- Process canvas users can add and change subprocess steps from the right-click workflow.
- Double-clicking a subprocess step opens the subprocess process definition in a new browser tab.
- Subprocess steps have a distinct canvas visual style.
- Default process templates include software-development subprocesses for smaller .NET implementation phases.

## Nonfunctional Requirements

- Use one canonical source of truth for parent-child runtime state.
- Prefer event/outbox-driven coordination over long-lived observer threads.
- Persist enough snapshots to keep historic runs understandable after definitions or agent names change.
- Keep the feature strongly typed.
- Keep changes in the Processes module and AgentFramework boundary where they belong.
- Validate with unit/component tests, integration tests, and UI browser proof.

## Architecture Inputs

- `C:\repositories\agent-framework` version 1.3 supports sub-workflows, executor binding, A2A surfaces, continuation tokens, and handoff workflows. These are useful references, but persisted process entities must not depend on preview SDK types.
- Existing CanDoItAll process runtime already has step runs, outbox dispatch, AgentFramework execution tagging, HR matching, process canvas surfaces, and process template packs.

## Implementation Assumption

- A subprocess step references a process definition. At run start, the child run uses the referenced definition's active published version unless an existing launch plan explicitly supplies another version.
