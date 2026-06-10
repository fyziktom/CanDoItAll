# Real test outcome summary

## Passing proof observed
- Latest focused integration transcript reports `46 passed, 0 failed` for verification host / live smoke related tests.
- Prior live process-run OpenAI smoke was real and valuable: it created a process run, dispatched via `IProcessRunAutomationDispatchService`, queried AgentFramework execution by process run/step id, and verified provider usage observations.
- Latest unit/build proofs remain green in the previous reports.

## Remaining validation gap
The newest code-first work appears to rely mainly on focused integration tests around existing adapters/host surfaces. The next validation must prove broader production behavior:

- EF audit readback across service scopes and profile/bootstrap boundaries.
- Manager API/UI readback of status/audit/denials.
- Scheduler/workflow read-only job execution through normal process services.
- Dry-run host contracts with effectful request attempts denied, not merely model-created.
- Live process-run OpenAI regression with explicit model/timeout/token budget, not implicit defaults.

## Rule for next implementation
A green test matrix is not sufficient unless it includes source-level code changes in the target runtime layers and a ratio gate showing that production/test code dominates bundle/proof edits.
