# Assumptions And Risks

## Assumptions

- `MafAgentRuntime` should remain the public `IAgentRuntime` implementation during this phase.
- Most new collaborators can be `internal sealed` with interfaces only at DI/test boundaries.
- Existing tests can be migrated gradually from `MafAgentRuntime` static/runtime calls to new collaborators.
- The previous extracted classes are acceptable starting points but may need renaming or repositioning if they still encourage runtime ownership.
- Full-suite baseline failures outside MAF must be documented, not solved by this bundle.

## Critical Path Risks

- **Wrong first extraction:** extracting builders before DTO/config contracts may preserve references to private runtime types and create another tangled layer.
- **Service-locator regression:** passing `IServiceProvider` everywhere would remove nesting but keep hidden dependency discovery.
- **Facade bloat:** replacing `MafAgentRuntime` with one huge `MafRuntimeCoordinator` would reproduce the same problem under a new name.
- **Behavior drift:** MCP, workspace tools, finalizer recovery, and input attachment behavior are user-visible to agents; extraction must be parity-first.
- **Test churn:** existing tests assert through runtime helpers. Migrating tests without clear seams can produce brittle reflection or broad fixture setup.

## Validation Risks

- A build-only proof is insufficient because these changes are mostly architectural.
- Tests that only assert non-empty tool lists or context-source counts can miss capability-policy regressions.
- Direct collaborator tests must include negative cases for access denial, unsupported provider features, duplicate tool names, and missing secrets.
- Performance proof must measure runtime/capability composition startup, not only project build time.
- Architecture guard tests must fail if new private nested builders are added under `MafAgentRuntime`.

## Reopen Triggers

- Reopen SB01/SB02 if implementation discovers additional nested runtime DTOs or builder classes not listed in the inventory.
- Reopen SB03 if `RuntimeCapabilityComposition` or equivalent composition state still references `MafAgentRuntime.*` nested types after extraction.
- Reopen SB04 if any capability builder constructor still accepts `MafAgentRuntime owner`.
- Reopen SB05 if workspace, input attachment, MCP, or artifact drivers still require full runtime construction for unit testing.
- Reopen SB06 if finalizer recovery, provider failure recovery, session persistence, or tool invocation guard logic remains private to `MafAgentRuntime`.
- Reopen SB07 if tests still need reflection to reach runtime internals or if architecture guards allow new `MafAgentRuntime` nested builders.
- Reopen SB08 if startup/composition metrics are missing, stale, or not compared against a baseline.
