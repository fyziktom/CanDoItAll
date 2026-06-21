# Structured Input

## Hard Constraints

- Preserve the current Process UI/UX direction as a reference point.
- Refactor or replace everything underneath the UI if necessary.
- Do not assume current drivers are correct.
- Keep generic runtime/dispatcher/core free of domain-specific vocabulary.
- Assign execution strategies during process instance composition.
- Treat completed steps as retained runtime records, not disposable work items.
- Support recursive subprocess composition.
- Use JSON as template source of truth.
- Use Git through a wrapper; do not implement a custom VCS.
- Make bundles versionable through `.gitignore`.
- Prepare architecture only; do not implement the rewrite now.
- Preserve v1 as historical evidence and produce v2 as the improved architecture bundle.
- Do not claim implementation subbundles are ready in this iteration.
- Ground architecture claims in actual repository files.
- Fill traceability with requirement-level acceptance criteria.

## Architecture Surfaces Required

- Generic process core
- Runtime execution engine
- Dispatcher
- Process instance builder and factories
- Templates and modular template components
- Domain drivers and layered driver selection
- Domain strategies
- Process manager
- Subprocess lifecycle and manager communication
- Artifact lifecycle and recovery
- Error preprocessing, recovery, and escalation
- Monitoring events, observers, snapshots, live/history projections
- UI-facing read models
- Git wrapper and Git UI components
- Switch/branch steps, backward routes, loop protection

## Important Assumptions

- The Process module rewrite can introduce new projects and remove current Process projects on a rewrite branch.
- The current UI can be migrated incrementally after the new backend contracts exist.
- Existing process templates are valuable source material but need a new versioned component/override model.
- Current driver verification projects are useful reference material but too narrow for the future driver system.
- Database persistence remains useful for indexing, snapshots, run state, and historical query performance, while text configuration should live as files under Git.
- The current validation script expects a `subbundles/` directory, so v2 keeps a deferred marker there for structure only.
