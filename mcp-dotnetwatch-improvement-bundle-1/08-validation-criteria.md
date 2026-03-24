# 08. Validation Criteria

## Evidence rule

Nothing passes on prose alone.
Every gate below must be backed by one or more of:

- automated test results
- captured tool payloads
- persisted transaction/slot manifests
- bootstrap logs
- manager screenshots where UI state is part of the requirement

## Strict pass criteria

### 1. Bridge reliability

Pass conditions:

- a clean stdio wrapper launch answers `workspace_info`
- if the backend registration becomes stale or disappears, the next read-only tool call repairs or rebinds the backend automatically
- bridge failures surface typed codes instead of generic invocation errors
- no hidden HTTP client timeout cuts off a tool before the tool's own timeout contract

Failure conditions:

- Codex still receives a generic invocation failure for a known bridge failure class
- repair attempts are silent and unobservable

### 2. Existing source-watch fluency is preserved

Pass conditions:

- small Razor or CSS changes complete to a confirmed revision state within 10 seconds median over 3 runs on a warm watch session
- restart-required C# changes complete to a confirmed revision state within 90 seconds on a warm watch session
- `app_status` exposes enough revision state that the agent does not need raw logs to know whether the current generation is authoritative

Failure conditions:

- bundle 1 makes small watch-based changes slower or less trustworthy than the current tested behavior

### 3. Published candidate preparation is isolated

Pass conditions:

- `app_update_atomic` publishes into an inactive slot, not the current active slot
- preparing the candidate does not require stopping the current active runtime
- running a published candidate does not lock the path needed for the next candidate prepare
- candidate runtime ports are allocated without colliding with active managed sessions

Failure conditions:

- the implementation still depends on one hot publish folder
- active runtime must be stopped before candidate prepare begins
- candidate endpoint allocation is implicit, ad hoc, or collision-prone

### 4. Atomic commit semantics

Pass conditions:

- the logical active runtime does not change until candidate health succeeds
- after commit, `app_status` for the logical app points to the new revision and session
- failed candidate prepare leaves the old active runtime authoritative
- failed commit does not leave the logical app in an indeterminate state

Failure conditions:

- Codex can observe a half-committed runtime
- commit mutates active runtime before candidate health is established

### 5. Rollback safety

Pass conditions:

- a previous committed revision is recoverable through `app_rollback`
- rollback switches the logical active runtime back to the previous revision
- rollback evidence includes prior and restored revision identifiers

Failure conditions:

- rollback is not implemented
- rollback depends on manual file surgery or manual process stopping

### 6. Resource coordination correctness

Pass conditions:

- conflicting operations fail fast with a named scope holder
- non-conflicting bridge or slot work is not serialized behind unrelated operations
- no deadlock scenario appears in automated or failure-injection validation

Failure conditions:

- the code still effectively behaves like one workspace-global lock
- resource conflicts produce vague or context-free failures

### 7. Backward compatibility

Pass conditions:

- existing `WatchRun` and `RunOnce` callers still work
- existing settings file remains valid with defaults for new sections
- existing watch-based integration tests continue to pass

Failure conditions:

- bundle 1 breaks current watch flows to gain published-slot support

### 8. Self-host validation isolation

Pass conditions:

- the live backend can successfully build or test `CanDoItAll.Mcp.DotNetWatch` itself through isolated artifacts
- no default loaded output directory has to be overwritten to validate the live server

Failure conditions:

- validating the MCP server still requires stopping the live backend
- the implementation silently relies on manual shell-side artifacts-path workarounds

### 9. Manager and observability quality

Pass conditions:

- manager status shows logical app id, lane, active revision, active slot, and pending transaction where relevant
- `workspace_info` exposes bridge status
- structured events are queryable incrementally
- raw log access remains available

Failure conditions:

- transaction state exists internally but is not visible to operators or Codex

## Required test matrix

### Unit tests

- bridge repair classification
- request idempotency and retry rules
- launch-spec compatibility mapping
- slot manifest serialization
- transaction state transitions
- resource-scope ordering and conflict handling
- rollback restoration logic

### Integration tests

- wrapper launch plus `workspace_info`
- backend repair after stale registration
- source-watch small edit flow
- source-watch restart-required flow
- published candidate prepare while active runtime remains live
- commit to candidate revision
- rollback to previous revision
- manager visibility of active slot and transaction state
- self-host build/test isolation for `CanDoItAll.Mcp.DotNetWatch`

### Failure-injection tests

- backend disappears mid-call
- auth token mismatch
- candidate publish fails
- candidate starts but never becomes healthy
- commit fails after candidate health
- rollback requested when no previous revision exists

## Minimum evidence artifacts

At final validation, capture at minimum:

- wrapper/bootstrap log excerpt
- one successful bridge repair transcript
- one successful source-watch revision confirmation transcript
- one successful atomic update transcript with transaction id and slot id
- one failed candidate transcript that preserves the prior active runtime
- one successful rollback transcript

## Final approval rule

Bundle 1 is approved only if:

- all strict pass conditions are met
- no failure condition above is observed
- the final QA reviewer in `12-final-qa-signoff.md` can cite concrete evidence for each critical area
