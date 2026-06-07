# .NET And Rust Verification Driver Lane Map

## Scope
This is docs/tests-only domain-lane modelling. It defines read-only evidence schemas and permission denials for future .NET and Rust verification helpers. It does not authorize shell execution, command orchestration, tool installation, publishing, package mutation, or runtime helper-driver wiring.

## .NET Verification Lane

| Evidence field | Source | Verification use |
| --- | --- | --- |
| Project or solution identity | Existing process artifact, proof transcript, or source assertion. | Confirm the inspected build/test proof belongs to the requested process step. |
| Target framework and package context | Existing project files and build logs. | Explain compatibility, analyzer, warning, or migration findings. |
| Build/test transcript path and output hash | Existing proof file. | Verify command result integrity without rerunning commands. |
| Warning and failure summary | Existing proof file. | Produce read-only diagnostics and suggested next proof. |
| Artifact expectation ids | Existing process artifact metadata. | Link diagnostics to required deliverables. |

Denied .NET side effects:
- package publish, package signing, feed mutation, database migration, workload installation, credentialed restore, external service calls, or workspace/storage writes;
- command execution from this lane in this bundle;
- automatic escalation from verification-only to execution-capable.

## Rust Verification Lane

| Evidence field | Source | Verification use |
| --- | --- | --- |
| Cargo manifest identity | Existing process artifact, source assertion, or proof transcript. | Confirm the inspected crate/workspace belongs to the requested step. |
| Toolchain and target summary | Existing proof file. | Explain compatibility or target-specific findings. |
| Check/test/lint transcript path and output hash | Existing proof file. | Verify result integrity without rerunning commands. |
| Failure and warning summary | Existing proof file. | Produce read-only diagnostics and suggested next proof. |
| Artifact expectation ids | Existing process artifact metadata. | Link diagnostics to required deliverables. |

Denied Rust side effects:
- crate publish, toolchain installation, cross-compilation setup, networked dependency update, credentialed registry access, or workspace/storage writes;
- command execution from this lane in this bundle;
- automatic escalation from verification-only to execution-capable.

## Permission Denials
- No shell execution driver is approved.
- Verification-only may inspect existing transcripts and hashes only.
- Manager-readonly may inspect process facts only.
- Execution-capable is a future gate and requires separate approval, capability scope, allowlist, timeout, audit facts, and state-transition ownership.

