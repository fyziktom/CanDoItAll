# 07. Prompts

## Prompt 0. Master implementation prompt

You are implementing `mcp-dotnetwatch-improvement-bundle-1` for `CanDoItAll.Mcp.DotNetWatch`.

Required reading order:

1. `mcp-dotnetwatch-improvement-bundle-1/01-current-state-analysis.md`
2. `mcp-dotnetwatch-improvement-bundle-1/02-target-operating-model.md`
3. `mcp-dotnetwatch-improvement-bundle-1/03-architecture-redesign.md`
4. `mcp-dotnetwatch-improvement-bundle-1/04-tool-contract-and-state-model.md`
5. `mcp-dotnetwatch-improvement-bundle-1/05-implementation-plan.md`
6. `mcp-dotnetwatch-improvement-bundle-1/08-validation-criteria.md`

Rules:

- preserve detached backend architecture
- keep existing tool names backward compatible
- do not collapse everything into one PR-sized refactor
- keep raw logs and manager behavior working unless explicitly improved
- prefer additive contracts and narrow focused changes
- if a design choice would break existing watch workflows, stop and fix the design instead of forcing the code

Definition of done:

- all strict pass criteria in `08-validation-criteria.md` are met with evidence

## Prompt 1. Bridge repair and typed failures

Implement Phase 2 from the bundle.

Deliverables:

- bridge repair coordinator or equivalent logic
- refactored `BackendConnectionManager`
- refactored `BackendToolInvoker` with safe repair-and-retry behavior
- bridge status included in `workspace_info`
- typed failure codes for bridge failures

Validation:

- prove that a stale or missing backend registration can be repaired without a generic invocation error
- prove that read-only calls retry safely
- prove that non-idempotent calls use idempotency keys or equivalent deduplication

## Prompt 2. Launch model refactor

Implement Phase 3 from the bundle.

Deliverables:

- new launch-spec model hierarchy
- compatibility mapping from current `AppRunMode`
- support for project watch, project run, published DLL, and executable launch descriptors
- endpoint allocation for candidate runtimes
- `app_status` lane and revision fields

Validation:

- existing watch and run integration tests still pass
- a published DLL can be launched under backend management
- candidate runtime endpoint leasing prevents port collisions

## Prompt 3. Resource scopes and lane-aware coordination

Implement Phase 4 from the bundle.

Deliverables:

- resource-scope planner
- scoped lock acquisition for app, build, test, bridge, slot, and shadow-build operations
- actionable conflict failures

Validation:

- conflicting operations fail fast with named scope holder information
- non-conflicting operations are no longer blocked by one coarse workspace lock

## Prompt 4. Atomic runtime lane

Implement Phase 5 from the bundle.

Deliverables:

- runtime slot registry
- candidate publish into inactive slot
- candidate launch on isolated ports
- `app_update_atomic`
- `app_rollback`
- persisted transaction and slot manifests

Validation:

- candidate preparation does not stop or mutate the active runtime
- commit only changes logical active runtime after health passes
- rollback restores the previous revision

## Prompt 5. Structured events and manager surfacing

Implement Phase 6 from the bundle.

Deliverables:

- `app_events` or equivalent structured incremental lifecycle stream
- manager/UI updates for lane, revision, slot, and transaction visibility
- unchanged raw log access

Validation:

- an update transaction can be followed from structured responses without raw-log parsing
- manager view clearly distinguishes active runtime, candidate runtime, and rollback availability

## Prompt 6. Validation and cleanup

Implement Phase 7 from the bundle.

Deliverables:

- updated unit and integration tests
- failure-injection coverage
- slot and shadow-build cleanup rules
- migration notes for existing Codex config
- self-host validation proof that the live backend can still build/test `CanDoItAll.Mcp.DotNetWatch` through isolated artifacts

Validation:

- satisfy every strict pass rule in `08-validation-criteria.md`
- provide concrete evidence artifacts, not only test names

## Prompt 7. Senior QA review prompt

You are acting as a senior C# and MCP QA inspector reviewing the completed implementation of `mcp-dotnetwatch-improvement-bundle-1`.

Required behavior:

- prioritize correctness, rollback safety, and bridge reliability over convenience
- look for behavioral regressions in existing watch flows
- verify backward compatibility of current tool names and settings
- verify that atomic updates are truly candidate-safe and rollbackable
- reject the implementation if validation evidence is missing or indirect

Required output:

1. findings ordered by severity
2. missing evidence list
3. regression risks
4. final approval or rejection against `08-validation-criteria.md`
