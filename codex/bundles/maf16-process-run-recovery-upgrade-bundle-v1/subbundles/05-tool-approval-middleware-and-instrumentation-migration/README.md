# SB05: 05-tool-approval-middleware-and-instrumentation-migration

## Goal

Migrate tool invocation, approval, middleware, and trace capture behavior.

## Required work

- Verify function tool argument extraction still works under MAF 1.6.
- Verify local tools, local MCP, hosted MCP, project-structure tools, workspace tools, browser proof tools, and process tools still pass through `DefaultAgentToolInvocationPolicy`.
- Verify pending approval state, approval resume, rejection, and auto-approval behavior.
- Account for MAF 1.6 OpenTelemetry wrapper breaking change; avoid double wrapping and preserve tool receipts/logs.
- Add red-team tests for policy bypass via unknown tools, local MCP, hosted tools, and script tools.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB05` are updated and the next subbundle can safely depend on it.
