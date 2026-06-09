# SB033 Red-Team Negative Proof

## Rejected Shallow Pass
A shallow pass would:
- cite an existing project-structure screenshot without creating a process run,
- prove only process definition projection, not run output projection,
- skip managed output artifact recording,
- skip the `process-run-output:` node assertion,
- skip the quick action route assertion, or
- open `/processes` without preserving `projectId`, `processId`, and `runId`.

## Why It Is Rejected
Gate K requires end-to-end project-structure output and run navigation. The accepted proof must create a project node, start a real process from it, record a managed output artifact, prove projected output-node identity, and navigate back to the exact run workspace.

## Required Positive Counter-Evidence
The accepted Playwright proof asserts:
- `Stage == "run-started"`,
- projected output node parent `process-run:{runId}`,
- projected output node id prefix `process-run-output:`,
- run popup URL `/projects/{projectId}/processes?processId={definitionId}&runId={runId}`,
- selected run summary contains the originating work item title.

Proof: `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`.
