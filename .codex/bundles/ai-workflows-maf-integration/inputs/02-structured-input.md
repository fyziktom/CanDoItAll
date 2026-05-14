# Structured Input

## Goals

- Introduce AI workflows as first-class executable definitions in CanDoItAll.
- Use Microsoft Agent Framework workflow primitives where they are appropriate and wrap them behind CanDoItAll contracts.
- Keep workflows at the same decision level as AI agents for process role assignment.
- Keep CanDoItAll processes above agents and workflows; a process may choose an agent or workflow to fill a role.
- Add workflow definition storage, settings, testing, UI, API, runtime observation, artifact handling, human-in-loop handling, and process integration.
- Add a reusable library of prepared LLM Call Components for workflow construction.
- Provide a workflow canvas editor similar in usability to the process canvas while preserving a distinct workflow domain model.

## Non-goals

- Do not replace process runtime, process definitions, or process canvas with MAF workflows.
- Do not implement feature code in this planning bundle.
- Do not collapse workflow models into process models just because some UI concepts are similar.
- Do not expose raw MAF types as persistence contracts or public API response contracts unless a boundary review explicitly approves it.
- Do not add fallback execution paths that hide workflow runtime failures.

## Constraints

- Use local MAF source at `C:\repositories\agent-framework` as the primary reference.
- Maintain strong typing for workflow identifiers, component kinds, executor kinds, run states, event kinds, artifact kinds, and external request kinds.
- Avoid magic strings for identifiers, keys, node kinds, commands, route names where constants or typed wrappers fit.
- Preserve existing AgentFramework module ownership and add a separate Workflows page inside that module.
- Reuse existing component library and Radzen usage patterns if present in the touched UI.
- Capture architecture reviews after every phase and run a detailed architecture review immediately after phase 1 before downstream implementation.

## Open Questions

- Whether workflow runtime orchestration belongs inside `CanDoItAll.AgentFramework.Maf`, a new `CanDoItAll.AgentFramework.Workflows` library, or split across Core/Maf/Persistence must be decided in subbundle 01 after a source-backed review.
- Whether MAF declarative YAML workflows should be first-class authoring input now or only imported later must be decided after the wrapper foundation proves model shape.
- Whether workflow runs share the existing agent execution history infrastructure or get a separate workflow history surface must be decided in subbundle 02 with migration and query impact documented.
