# 14 — DotNetWatch and Live-Process Test Gating


## Problem

DotNetWatch/live-process tests can be long-running and flaky if mixed into the default gate without isolation.

## Tasks

1. Identify dotnetwatch/live-process tests and their process-spawn behavior.
2. Categorize them as `LiveProcess` and/or `LongRunning`, unless they are made fully deterministic and fast.
3. Add environment-variable gating if needed, for example `RUN_LIVE_PROCESS_TESTS=true`.
4. Ensure spawned processes are killed on timeout/failure.
5. Ensure tests run serially if they share ports, files, or host state.
6. Add docs for running this extended suite.

## Acceptance criteria

- Default test gate is not destabilized by long-running live-process tests.
- Extended live-process gate is documented and runnable.
- Timeouts produce useful logs and clean up child processes.

