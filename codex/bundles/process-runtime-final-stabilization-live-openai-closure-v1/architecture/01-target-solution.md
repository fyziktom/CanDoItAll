# Target Solution

This canonical file mirrors `bundle://architecture/01-target-architecture.md` for validator compatibility.

## Process Core
- Keep Process Core generic and deterministic.
- Do not add UI, EF, AgentFramework, OpenAI, scheduler, workflow, template, driver, or domain-specific process-family dependencies.

## Process Module Runtime
- The existing process module remains responsible for definitions, templates, launch plans, run lifecycle, outbox dispatch, finalizers, artifacts, UI/API integration, scheduler/workflow starts, and runtime-host diagnostic surfaces.

## Runtime Host
- Verification and dry-run host behavior remains read-only and diagnostic.
- It must not mutate process state, workspace, storage, transitions, claims, retries, finalizers, Office/Graph, CRM, or command execution.

## Deferred Scope
- Execution-capable drivers, driver registries, fallback selectors, runtime extraction, and hidden scheduler driver hooks remain out of scope.
