# Assumptions And Risks

## Working Assumptions

- The GPTPro analysis files are treated as authoritative incident source material unless implementation discovers a contradictory source fact.
- The blocked 5032 calculator run is one concrete symptom of a systemic completion, recovery, subprocess, and template contract problem.
- Existing diagnostic codes should be preserved where possible; new aggregate/container codes may be added only when they do not erase the original diagnostic metadata.
- The first implementation target is the existing project layout. New projects are not justified unless an extracted contract must cross current project boundaries.
- Template hardening must cover source-controlled templates, not only generated or copied runtime artifacts.

## Critical Path Risks

- Retry loops can become silent churn if the recovery classifier does not fingerprint diagnostics and enforce a strict budget.
- Moving deterministic work into runtime-owned plans can blur application/runtime/module boundaries if contracts are placed in the wrong project.
- Aggregating diagnostics without deterministic priority can make rework packets noisy and harder to test.
- Child bridge changes can accidentally accept artifacts before managed artifact gates pass.
- Template migration can drift if only the incident template is updated.
- Existing tests may encode the current broken placeholder behavior and must be changed intentionally, not deleted.

## Validation Risks

- A build-only validation can pass while the escalation class remains unresolved.
- Unit tests that mock product readback without receipt history can miss the missing-helper root cause.
- File-existence tests can falsely pass artifact bridge behavior that should require ledger/slot acceptance.
- Prompt text snapshots can pass while typed template validation is absent.
- UI/projection tests can pass if they only verify parent blocked status and ignore child root-cause propagation.
- Manual 5032 validation may be unavailable; the bundle must define an equivalent local reproduction if the live instance cannot be rerun.

## Reopen Triggers

- Any tool-critical launch variable still reaches an agent with `{CurrentProcessRunId}`, `${CurrentProcessRunId}`, or `{{CurrentProcessRunId}}` unresolved.
- A safe/idempotent completion-gate failure still routes directly to manager escalation before retry budget exhaustion.
- Parent subprocess packets still show only generic child blocked text without child diagnostic code, missing receipt, and readback details.
- An accepted parent artifact can still be inferred from physical file existence alone.
- Any affected template still encodes required receipts, artifact acceptance, branch/no-go outcomes, or subprocess contracts only in markdown prose.
- Template validation passes after removing a required typed tool receipt or subprocess contract.
- A new runtime record, state, or event is introduced without production producer/consumer/lifecycle proof.
