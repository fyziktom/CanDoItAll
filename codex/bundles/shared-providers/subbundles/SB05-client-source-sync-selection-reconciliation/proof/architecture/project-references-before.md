# SB05 project references before implementation

State: `CAPTURED`

The source listing is `proof/transcripts/sb05-project-references-before.txt`.

- `SharedProviders.Abstractions` has no outgoing product reference.
- `SharedProviders.Http` references only `SharedProviders.Abstractions`.
- `Modules.Workspace` references `SharedProviders.Abstractions`, not Http.
- `Composition` references Http and Workspace and remains the concrete registration boundary.
- Web references Abstractions/Workspace/Composition but not Http directly.
- The graph baseline has 14 scoped projects, 34 direct product references, and no project cycle.

SB05 is expected to add neutral source-catalog contracts and implementations within these
existing edges. A new product `ProjectReference` requires an explicit architecture reopen.
