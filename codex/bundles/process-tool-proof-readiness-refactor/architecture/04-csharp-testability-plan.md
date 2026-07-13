# C# Testability Plan

## Unit Tests

- Contract compiler normalizes template and driver contributions into a stable effective contract.
- Capability translator handles allow, allow-only, deny, suppress, and require directives for runtime tools and MCPs.
- Receipt gate rejects completed outcomes when current-run required receipts are absent.
- Receipt gate accepts outcomes only when matching receipt type, provider/tool name, attempt/run id, and minimum count are satisfied.
- Fallback planner maps missing proof diagnostics to redispatch, reassignment, driver recovery, or NeedsAttention.

## Integration Tests

- Process assignment persistence round-trips the new contract fields.
- Metadata builder carries the effective contract into governed AgentFramework execution metadata.
- HR readiness flow reports missing Playwright/image tool access before launch.
- Finalizer/recovery path cannot accept artifact-only outcome for a step with required receipts.
- Migrated software-delivery template produces non-empty contracts for QA validation and QA recheck.

## E2E Tests

- Reproduce the `qa-recheck` failure pattern with required browser/image receipts missing and assert the run does not falsely complete.
- Run a happy path where browser screenshot, console, runtime, and image analysis receipts are captured and accepted.
- Validate that suppression removes a development skill or MCP from the agent context for a management-only process step.

## Test Fixtures

- Minimal process definition with a single QA proof step.
- Agent fixture with Playwright/image capabilities.
- Agent fixture without Playwright/image capabilities.
- Receipt fixture for stale upstream artifact proof.
- Receipt fixture for current-run tool proof.

## Performance Tests

- Assert compiled contract cache is used for repeated readiness evaluations on the same process plan hash.
- Assert readiness evaluation does not instantiate tool providers or launch MCP servers.
- Assert runtime metadata building uses existing capability catalogs rather than rebuilding them per attempt.
