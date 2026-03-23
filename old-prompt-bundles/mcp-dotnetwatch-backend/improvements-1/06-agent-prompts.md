# Step Prompts

## Prompt 1. Global catalog
Implement a machine-wide backend catalog for `CanDoItAll.Mcp.DotNetWatch`.
Requirements:
- store backend registrations outside the workspace
- allow multiple workspace backends at once
- prune stale entries
- keep the existing workspace-local registration because stdio reuse still depends on it
Validation:
- two live backends from different workspaces are both discoverable from one manager instance

## Prompt 2. Aggregated manager
Upgrade the backend manager API and UI from local-only to aggregate mode.
Requirements:
- include the current backend plus all live discovered backends
- show sessions and operations per backend
- show aggregate counts
- keep the page simple and operator-oriented
Validation:
- opening the CanDoItAll manager page also shows the pveinvoicing backend

## Prompt 3. Manager actions
Add basic manager controls.
Requirements:
- stop
- force stop
- rebuild / restart for watch sessions
- build trigger for a backend workspace
- local execution for the current backend and proxy execution for remote backends
Validation:
- at least one remote backend action works from the aggregated manager page

## Prompt 4. Watch automation
Harden and verify rude-edit restart behavior for `dotnet watch`.
Requirements:
- no interactive rude-edit prompt can block agent workflows
- document the exact reason using official Microsoft docs
- keep explicit safeguards in code
Validation:
- code path and docs clearly show why confirmation is not needed

## Prompt 5. Log reduction
Implement agent-optimized log reduction without losing important diagnostics.
Requirements:
- preserve errors, failures, exceptions, watch lifecycle, and final outcomes
- suppress or summarize warning floods and repetitive framework noise
- keep raw persisted ndjson logs unchanged
Validation:
- reduced output for a real noisy sample is substantially smaller and still useful

## Prompt 6. Final validation
Run live validation against both repositories.
Requirements:
- `CanDoItAll`
- `C:\repositories\pveinvoicing\PVEInvoicing`
- MCP stdio re-instancing must not stop the running app
- small visual change and verification must still work
- manager UI must show both backends
- capture quantitative log-savings numbers
