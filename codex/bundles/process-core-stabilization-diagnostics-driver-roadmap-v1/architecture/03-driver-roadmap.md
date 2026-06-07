# Driver Roadmap

## Long-term target
A stable Process Core should eventually support domain helper drivers such as:
- .NET software-development verification driver
- Rust software-development verification driver
- Office/document/spreadsheet analysis driver
- Business-analysis evidence driver
- Runtime proof consistency verifier

## Current bundle decision
This bundle should not create production drivers yet. It should prepare the next decision gate.

## Proposed driver contract lanes
| Lane | Mode | Allowed |
| --- | --- | --- |
| Verification-only | read-only | inspect Core facts and module-produced evidence; return diagnostics |
| Manager-readonly | read-only | help process manager verify evidence without changing state |
| Execution-capable | future only | must require separate approval, permission enforcement, audit logs, and sandboxing |

## Explicit denials
No driver may silently:
- mutate process state,
- run shell commands,
- access secrets,
- write workspace/storage,
- bypass process approvals,
- convert verification mode into execution mode.
