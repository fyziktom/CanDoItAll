# Structured Input

## Functional Requirements

- Add a `Manager chat` tab after `Exchange` in the process detail workspace.
- Resolve the responsible manager agent from the selected run when available, otherwise from the process manager override, otherwise from the bound default manager option when one can be resolved.
- Use the existing AgentFramework chat session store and execution pipeline for manager conversation history.
- Let the user select a process run from a modal before sending context-scoped prompts.
- Add a bounded feature/function implementation subprocess to the default process templates.
- Wire that subprocess into the main software-development flow through the implementation slice.
- Validate the subprocess alone and through the higher-level process flow.

## Nonfunctional Requirements

- Avoid splitting source of truth for chat, runs, manager assignment, or subprocess state.
- Keep dispatcher behavior generic; process-step descriptions and agent instructions should carry domain detail.
- Preserve strong typing for manager/run resolution and subprocess references.
- Use existing Blazor components and AgentFramework chat UI where possible.
- Capture build/test/browser evidence and document real-agent blockers honestly.
