# Test suite prompt

Build a proper automated test suite for the CanDoItAll MCP server.

## Create or update
- unit tests
- integration test harness
- fixture apps/processes:
  - HappyPathWebApp
  - SlowStartWebApp
  - CompileErrorApp
  - ProcessTreeFixture
  - optional runner detection fixture(s)

## P0 scenarios to automate
- stdio cleanliness
- invalid config fail-fast
- workspace info
- WatchRun app start
- RunOnce app start
- stop kills process tree
- incremental app logs
- app wait healthy
- app wait quiet since cursor
- build StopAndResume
- tests StopAndResume
- unexpected exit detection
- stale cleanup
- path outside workspace blocked

## Constraints
- Prefer deterministic fixtures.
- Do not rely on flaky sleeps.
- Keep tests independent where practical.
- Capture useful failure logs/artifacts.

## Deliver
- list of tests added
- mapping to validation matrix IDs
- any remaining manual-only scenarios
