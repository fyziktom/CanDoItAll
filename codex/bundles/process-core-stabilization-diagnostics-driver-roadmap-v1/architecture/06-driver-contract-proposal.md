# Driver Contract Proposal

## Scope
This is docs/tests-only readiness material for a future process helper-driver contract discussion. It is not a production API, registry, DI registration, manager command, runtime selector, or execution hook.

## Contract Gates

| Gate | Current status | Allowed candidate behavior | Denied behavior |
| --- | --- | --- | --- |
| Verification-only | Proposal only | Inspect already-produced process facts, Core read-model descriptors, command transcripts, artifact metadata, and proof files; return diagnostics. | Mutating process state, running unapproved commands, writing artifacts, publishing packages, accessing secrets, or silently escalating to execution. |
| Manager-readonly | Proposal only | Let a process manager inspect process/run/step/artifact facts and record denial reasons for unsupported operations. | Transition execution, claim or lease changes, workspace/storage writes, external system calls, or hidden retries. |
| Execution-capable future gate | Not approved | Only a later bundle may consider bounded execution after explicit approval, capability scope, command allowlist, timeout, audit log, and state-transition ownership are designed. | Any runtime hook in this bundle, any implicit command execution, any helper that can bypass the process module, or any side effect without artifact evidence. |

## Candidate Contract Vocabulary
The terms below are descriptive labels for future analysis. They are not type names and must not appear as production C# contracts in this bundle.

| Vocabulary | Meaning | Current owner |
| --- | --- | --- |
| Route evidence | Route-stage decisions and reasons emitted by Core pure rules and module adapters. | `repo://src/CanDoItAll.Processes.Core/Routing` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch` |
| Artifact evidence | Expected artifact facts, match diagnostics, projection descriptors, trust/sensitivity facts, and validation policy descriptors. | `repo://src/CanDoItAll.Processes.Core/Artifacts` and module validation adapters |
| Domain evidence | .NET, Rust, Office, and business-analysis observations that future helpers may inspect after proof files already exist. | `bundle://architecture/08-driver-lane-map-dotnet-rust.md` and `bundle://architecture/09-driver-lane-map-office-business-analysis.md` |
| Permission denial | A testable reason that a requested helper action is outside the selected mode. | `bundle://architecture/07-driver-permission-negative-scenarios.md` |

## Permission Rules
- Absence of an explicit mode is denied.
- Verification-only and manager-readonly modes are read-only.
- Execution-capable work requires a separate future approval gate and cannot be inferred from this proposal.
- Denials must include the requested operation, lane, selected mode, relevant process/run/step ids when available, and a non-sensitive reason.
- Logs must not expose secrets, tokens, connection strings, or unrelated user content.

## Test Expectations
- Production source remains free of process-helper-driver APIs, registries, runtime selectors, manager commands, and driver DI registration.
- Driver-readiness docs contain no production API-shape or service-registration examples.
- Lane maps name accepted evidence and denied side effects, not executable implementation.
- Gate I closes only when SB025-SB027 proof confirms this proposal stayed non-production.

