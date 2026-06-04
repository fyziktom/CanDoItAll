# Target Solution

## Desired End State For This Bundle

The dispatcher still owns process runtime orchestration, but artifact evidence concerns become smaller and testable:

```text
ProcessRunAutomationDispatchService
  -> ProcessArtifactExpectationMatcher
  -> ProcessArtifactProjectionLineageBuilder
  -> ProcessArtifactProjectionPlanner
  -> ProcessArtifactEvidenceValidationRules
  -> existing storage/DB mutation path, migrated gradually
```

The bundle should reduce coupling and method size without pretending that artifact runtime is now core-ready.

## Allowed Internal Services

- Internal services/classes inside `CanDoItAll.Modules.Processes`.
- Neutral records in `CanDoItAll.Processes.Contracts` only if they contain no EF, UI, AgentFramework, storage, or module dependencies.
- Focused test fixtures under Unit/Integration tests.

## Disallowed End State

- A new Process Core project.
- Domain driver packs.
- Broad dispatcher rewrite.
- New external tool surfaces.
- UI work.
