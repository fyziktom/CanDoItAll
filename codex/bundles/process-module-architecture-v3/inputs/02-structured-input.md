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
- Preserve v2 as historical evidence and produce v3 as architecture plus future subbundle roadmap.
- Prepare real future subbundles SB01-SB28 without executing them.
- Fix project dependency/order ambiguity before future implementation.
- Add explicit runtime persistence/event/outbox architecture.
- Add explicit branch/switch/loop contract.
- Add explicit manager control loop.
- Add UI/UX projection contract inventory.
- Add execution adapter boundaries for workflows, agents, agent groups, handoffs, scheduler starts, and project/workbench integrations.
- Add runtime history migration/read-only compatibility plan.
- Improve v3 with a user-story map derived from current Process implementation, UI/UX, tests, templates, and live UI evidence.
- Split broad UI/rebuild work into smaller subbundles with validations after each complex part.
- Require Playwright MCP and screenshot proof for browser-facing subbundles.

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
- v3 replaces the deferred subbundle marker with detailed future implementation packages.
- The story-map update treats current UI/UX behavior as a coverage baseline, not as an endorsement of current runtime/dispatcher internals.
