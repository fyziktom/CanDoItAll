# API Captures

These files were captured from `http://localhost:5032` against the development database profile. Email addresses were redacted in the saved JSON.

Process captures:

- `access-status.json`
- `process-runs-list.json`
- `process-run-6724-detail.json`
- `process-run-stale-49fd-detail.error.txt`
- `agent-execution-runs-for-process-6724.json`

Agent captures:

- `agents-include-templates.json`
- `agent-providers.json`
- `agent-capabilities.json`

Workflow captures:

- `workflow-runs-list.json`
- `workflow-run-e58-detail.json`
- `workflow-run-e58-events.json`
- `workflow-run-e58-artifacts.json`
- `workflow-run-e58-checkpoints.json`
- `workflow-definition-ec134686.json`
- `workflow-executor-catalog.json`
- `workflow-runtime-backends.json`

Notes:

- `process-run-stale-49fd-detail.error.txt` records that a process run id referenced by stale or alternate agent-output lineage was not found through the process API.
- The workflow run was present and completed, so the Office365 workflow was not rerun.

