# Implementation Plan

## Phase 1. Documentation baseline
1. Create this `improvements-1` package.
2. Lock clarified requirements and validation gates.

## Phase 2. Global manager visibility
1. Add a machine-wide backend catalog service.
2. Register/unregister backends in both workspace-local and machine-wide storage.
3. Add aggregate manager snapshot models and API support.
4. Update the manager UI to render all discovered backends.

## Phase 3. Manager controls
1. Add manager action contracts and endpoints.
2. Add local execution and remote proxy execution.
3. Add session controls:
   - stop
   - force stop
   - rebuild / restart
4. Add backend-level controls:
   - start default app
   - build workspace target

## Phase 4. Log reduction
1. Add a log view mode with an agent-optimized default.
2. Implement a reducer for app logs and operation logs.
3. Add response metadata summarizing suppressed noise.
4. Add targeted unit tests for warning and noise reduction.

## Phase 5. Verification
1. Run focused unit tests.
2. Build the MCP server.
3. Re-run live validation for `CanDoItAll`.
4. Re-run live validation for `pveinvoicing`.
5. Open the manager UI and verify both backends appear with controls.
6. Measure raw versus reduced logs and write the results.
