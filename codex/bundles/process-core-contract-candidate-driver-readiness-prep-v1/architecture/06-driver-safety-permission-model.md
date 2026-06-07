# Driver Safety Permission Model

## Scope
This is a draft safety model for future helper-driver discussions. It is not a production permission system, production interface, registry, DI registration, manager tool, or runtime dispatch mechanism.

## Permission Modes

| Mode | Allowed reads | Allowed writes or execution | Required audit facts | Disallowed behavior |
| --- | --- | --- | --- | --- |
| Manager-readonly | Process definition, run, step, artifact metadata, project-structure metadata, proof transcripts, and lane descriptors. | None. | Caller identity, process/run/step ids, selected lane, inspected artifact ids, and denial reason when a requested operation is outside the mode. | Process state mutation, file writes, tool execution, AgentFramework execution, transition calls, claim or lease changes, and external network calls. |
| Verification-only | Manager-readonly reads plus local validation of already-produced evidence and optionally configured non-mutating commands. | Read-only command execution only when a later bundle defines an allowlist and captures command, working directory, exit code, and output hash. | Mode, command allowlist id, input artifact ids, output summary hash, and redacted diagnostics. | State mutation, artifact projection writes, package publish, database migration, secret exposure, unbounded shell access, and silent fallback to execution mode. |
| Execution-capable | A future explicitly approved lane may read the same facts and execute a bounded action. | Only after a later production design adds explicit approval, lease ownership, capability scope, command allowlist, timeout, output capture, and state-transition policy. | Approval id, lease id, capability scope, command or tool identity, input/output artifact ids, transition request, and failure reason. | Implicit execution, hidden retries, writes without artifact records, unmasked secrets, manager bypass, and broad project or network access. |

## Domain Constraints

| Domain | Candidate use | Default mode | Additional constraints |
| --- | --- | --- | --- |
| .NET software development | Build/test verification, analyzer result interpretation, package/API compatibility review. | Verification-only. | No publish, package signing, database migration, credentialed feed mutation, or workload installation without a later explicit execution-capable design. |
| Rust software development | Cargo check/test result interpretation and source-layout review. | Verification-only. | No publish, toolchain install, cross-compilation setup, or networked dependency update without explicit approval and command allowlisting. |
| Office documents | Inspect, render, validate, or compare workbook/document/presentation artifacts in the workspace. | Manager-readonly or verification-only. | No external upload, email/send action, macro execution, or file overwrite outside an approved artifact output path. |
| Business analysis | Summarize process evidence, compare expected/actual deliverables, and produce decision support. | Manager-readonly. | No business-record mutation, external system write, customer communication, or policy decision automation. |

## Enforcement Requirements For Any Future Bundle
- Permission mode must be explicit in the route or manager request; absence of a mode is a denial, not a fallback.
- Capability scopes must be strongly typed before production use. String-only scopes are acceptable only inside this documentation bundle.
- Every denial must include the requested operation, mode, lane, process/run/step ids when available, and a non-sensitive reason.
- Secrets, tokens, connection strings, and user content beyond the artifact under review must be masked from logs.
- Execution-capable work must be introduced behind failing-first tests that prove manager-readonly and verification-only modes cannot mutate process state.

## Current Decision
SB029 does not authorize any driver execution. It only supplies the safety vocabulary that SB030 must prove remains documentation-only.
