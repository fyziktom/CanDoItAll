# Driver Negative Scenarios

## Scope
- This is SB023/SB024 proposal proof.
- These scenarios describe what future driver tests must deny before any production driver runtime may exist.

## Negative Scenario Matrix
| Scenario | Requested action | Required result |
| --- | --- | --- |
| Mutation denied | Change a process run, step run, claim, transition, finalizer result, retry state, artifact record, or workflow state. | Denied with explicit process-mutation reason. |
| Runtime hook denied | Attach to AgentFramework execution, provider repair, retry scheduling, recovery packet creation, or finalizer application. | Denied with explicit runtime-hook reason. |
| Registry denied | Register or resolve a process-driver pack, runtime selector, or production driver registry. | Denied because production driver runtime is not approved. |
| DI denied | Add production service registration for a process driver runtime. | Denied because dependency-injection integration is not approved. |
| Manager command denied | Expose a manager command that invokes a process driver. | Denied because manager-facing execution is not approved. |
| Shell denied | Run shell, PowerShell, `dotnet`, `cargo`, package restore, publish, or git mutation. | Denied because command execution is not approved. |
| Graph denied | Call Office, Graph, mail, calendar, task, or document APIs. | Denied because external service execution is not approved. |
| Storage denied | Write workspace, storage, generated artifact, or filesystem content. | Denied because readonly modes cannot write. |
| Audit incomplete | Return diagnostics without caller, process, mode, capability, evidence, result, and correlation facts. | Denied because audit facts are incomplete. |

## Required Proof Shape
- Production source scan must find no process-driver pack, registry, runtime selector, manager command, or runtime API.
- Documentation may describe future decisions, but must not contain production registration code.
- Architecture tests must fail if the proposal accidentally becomes a production runtime surface.
- Any execution-capable lane requires a later bundle with sandbox policy and executable negative tests before production code.
