# Assumptions And Risks

## Working Assumptions

- The branch starts from the post-`process-dispatch-artifact-validation-residual-boundary-v1` implementation state described in `inputs/01-branch-review-summary.md`.
- Execution/retry/provider work remains module-local under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- Runtime/service-only refactoring keeps browser validation N/A unless UI files unexpectedly change.
- Existing focused process dispatch tests are the preferred proof path; broad unrelated historical failures must be recorded rather than hidden.

## Critical Path Risks

- **Retry behavior drift**: A small change in no-progress compression, retry reasons, or carried proof can cause the dispatcher to stop too early or retry forever.
- **Provider repair side effects hidden in pure helpers**: Provider fallback may mutate technical agents through `SaveAgentAsync`; this must remain explicit in a coordinator, not a pure rule class.
- **Recovered execution adoption drift**: Recovered or concurrent execution runs must preserve chat session, response text, and attempt-number behavior.
- **Recovery journal drift**: Retry/rework/provider recovery journal records must retain original mode, packet, next-attempt, and correlation semantics.
- **Premature Process Core pressure**: Extracting these concepts into public Core contracts before they are stable would freeze unstable implementation details.

## Validation Risks

- Broad architecture test classes may still include unrelated historical bundle fixture failures; this bundle must use focused tests and explicitly record any known unrelated failures.
- Source scans must distinguish a valid no-match result from an error. `rg` exit code 1 for no matches is expected in no-core/no-driver scans.
- No browser/viewport proof should be created unless UI files unexpectedly change. Runtime/service refactor should keep browser validation N/A.

## Reopen Triggers

Reopen the most recent production movement subbundle if any of these are observed:

- `ExecuteUntilSettledAsync` loses a recovery path or skips provider fallback.
- No-progress compression changes event type, correlation id, or replay context semantics.
- Provider repair mutates agent configuration without explicit coordinator proof.
- A helper introduces `CanDoItAll.Processes.Core`, `IProcessDriverPack`, `ProcessDriverRegistry`, or driver package references.
- Any moved helper contains TODO, NotImplementedException, `return default` stub behavior, or changes branch order without explicit test proof.
