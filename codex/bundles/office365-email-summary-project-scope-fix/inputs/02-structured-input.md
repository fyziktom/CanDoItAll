# Structured Input

## Problem

- The workflow LLM node runs through MAF without passing the project-structure `projectId` as contributor policy scope.
- Governed Cognitive Memory context correctly rejects missing project scope.
- After project scope is provided, a newly created project can legitimately have no memory context yet, which must be treated as an explicit skipped contribution rather than a runtime outage.

## Expected Output

- LLM workflow execution passes `WorkspaceScopeKind.Project` to context contributors when workflow payload contains `projectId` or `project.id`.
- Missing project scope still fails governed automation.
- Empty Cognitive Memory context pack is traced as skipped, while actual recall exceptions still fail.
- Office365 category workflow completes and creates markdown under the workflow node.

## Validation Targets

- Unit: MAF LLM invoker passes project scope.
- Unit: MAF runtime uses context scope override for contributors.
- Unit: Cognitive Memory skips empty context pack for process automation.
- Integration: project-structure LLM workflow creates markdown asset under the workflow node.
- Live: `candoitall_development` Office365 workflow fetches the Tetris email and stores a useful markdown summary.
