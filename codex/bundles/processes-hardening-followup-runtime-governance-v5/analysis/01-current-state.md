# Current State

The process runtime has moved through several hardening phases.

## What Is Now Better

- Direct agent, workflow-backed role, subprocess parent, and manager recovery paths are closer to a common finalizer model.
- Process step boundary metadata is emitted into AgentFramework execution metadata.
- Tool policy understands process product-mutation authorization.
- Missing upstream artifact materialization now has a reactivation path.
- Artifact validation now has storage-backed checks for JSON, Markdown, YAML, and image evidence.
- Linting exists and has both advisory and strict modes.

## What Still Needs Work

The main remaining issues are about making governance **first-class** instead of inferred:

- Step operation contracts are still derived from text unless an explicit phrase is placed in notes/contract text.
- The tool policy mostly enforces product mutation, not all allowed operations.
- Grounding still scrapes paths from broad text sources.
- Artifact lineage is present but should be made uniquely indexable and queryable.
- Workflow/subprocess artifact mapping is still heuristic.
- Strict lint mode exists but defaults can leave it advisory.
- Runtime invariant failures need durable health reporting and escalation.
