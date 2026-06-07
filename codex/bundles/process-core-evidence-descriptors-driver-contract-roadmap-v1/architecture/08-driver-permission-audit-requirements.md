# Driver Permission And Audit Requirements

## Scope
- This is a proposal artifact for SB022-SB024.
- It defines requirements for future driver work, but it is not a production runtime contract.
- Absence of an approved mode is a denial.

## Permission Modes
| Mode | Approved now | Allowed activity | State mutation |
| --- | --- | --- | --- |
| `VerificationOnly` | Yes, for future proposal work only | Inspect existing process facts, Core descriptors, proof transcripts, artifact metadata, and read snapshots. | Denied |
| `ManagerReadonly` | Yes, for future proposal work only | Return explanations and diagnostics to a manager request without changing process, workspace, document, or external state. | Denied |
| `ExecutionCapableFuture` | No | Reserved for a later explicit bundle with sandbox, command policy, approval, and executable negative tests. | Not approved |

## Required Audit Facts
Future production work must prove each request records:
- Caller identity and authorization source.
- Process run id, step run id, and optional execution run id.
- Driver lane, permission mode, and capability scope.
- Input evidence ids and inspected artifact ids.
- Redacted diagnostic summary.
- Denial reason or result reason.
- Output hashes for captured evidence.
- Timeout policy and sandbox policy identifiers.
- Correlation id for process-owned audit trails.

## Denial Reasons
Future drivers must return explicit denial reasons instead of silently falling back:
- Permission mode missing.
- Capability scope missing.
- Execution requested in a readonly mode.
- Process mutation requested.
- Workspace, storage, or filesystem write requested.
- External service call requested.
- Shell or command execution requested.
- Process claim, transition, finalizer, or retry hook requested.
- Audit facts incomplete.

## Ownership
- Core may describe pure evidence facts.
- The process module remains the owner of claims, transitions, finalizers, retry scheduling, storage, workspace operations, and audit persistence.
- Driver runtime ownership is deferred until permission enforcement, audit persistence, sandbox policy, and negative executable tests exist.
