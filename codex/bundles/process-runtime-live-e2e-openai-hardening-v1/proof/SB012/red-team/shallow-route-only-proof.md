# Shallow Route-Only Proof

This intentionally insufficient proof claims SB012 is complete because the projected output folder quick action opens `/projects/{projectId}/processes?runId={runId}` and the process workspace shell renders.

The proof does not verify persisted project-structure context in the run trigger, does not prove the route contains `processId`, does not assert the projected output folder node exists under `process-run:{runId}`, and does not prove the opened workspace selected the target run.
