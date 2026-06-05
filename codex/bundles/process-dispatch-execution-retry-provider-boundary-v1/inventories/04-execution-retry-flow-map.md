# Execution And Retry Flow Map

## ExecuteUntilSettledAsync Branches

- Recover existing execution run when `candidate.RecoveryExecutionRunId` is present on attempt 1.
- Adopt active concurrent automation execution before launching a fresh run.
- Build invocation policy and metadata before launching `executionClient.ExecuteRunAsync`.
- On `ProcessAutomationExecutionFailedException`, load failed run detail and inspect it as a terminal attempt.
- On concurrent chat-session busy exception, adopt the competing automation run instead of launching a duplicate.
- Return an `InProgress` dispatch outcome when the observed automation run is non-terminal.
- Validate governed structured outcome and log validation errors without changing the terminal inspection flow.
- Accumulate successful tool names and carried implementation proof across attempts.
- Build post-attempt facts: missing required tools, unresolved critical failures, completion status, completion reason, and selected branch outcome.
- Return immediately when the step completes.
- Try provider repair before generic retry when the attempt failed because of provider infrastructure.
- Resolve retry decision, retry reasons, no-progress signal, no-progress compression, recovery decision, optional rework packet, and typed recovery directive.

## Retry Reason Families

- Missing required tools.
- Unresolved critical tool failures.
- Recoverable implementation punt.
- Missing concrete proof.
- Missing current-attempt implementation proof.
- Missing runnable application proof.
- Invalid browser proof.
- Invalid quality validation proof.
- Missing required artifact.
- Downgraded project-structure requirement.
- Missing upstream artifact inspection.
- Stale or ungrounded product path reference.
- Shared managed artifact collision risk.
- Recoverable provider failure.
- Recoverable finalizer validation failure.
- Recoverable execution interruption.
- Recoverable repeated tool invocation.

## Provider Recovery Side Effects

- Lists agents and providers through `executionClient`.
- Probes fallback provider health with a bounded linked cancellation token.
- Loads assigned party ids from the process database.
- Loads assigned technical-agent summaries through `technicalAgentBridge`.
- Mutates affected technical agents through `executionClient.SaveAgentAsync`.
- Writes provider recovery journals and typed recovery directives through existing dispatcher journal paths.

## Closure

- SB02 creates no production behavior change.
- Later subbundles must keep the same branch order unless a focused test proves a deliberate behavior-preserving equivalent.
