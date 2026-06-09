# SB036 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by moving files alone or by lane-specific unit tests alone. The proof must show the process-level read-only orchestrator remains bounded, uses explicit lane payload builders/adapters, preserves aggregate no-mutation semantics, keeps audit/evidence hashes valid, and redacts sensitive diagnostic/audit text.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- read-only orchestration becomes a generic runtime host or driver registry;
- read-only orchestration registers drivers in DI or hosted services;
- a manager/scheduler/workflow hook invokes drivers;
- batch verification writes process state, conformance observations, artifacts, workspace files, or storage records;
- aggregate observations stop proving all responses are mutation-free;
- any response lacks audit facts or evidence references;
- evidence references or audit output hashes stop using SHA-256;
- redaction hashes are missing or sensitive transcript text leaks through diagnostics/audit summaries;
- lane-specific builders/adapters are replaced with stringly-typed dispatch.

## Semantic Positive Proof

`bundle://proof/SB036/transcripts/focused-readonly-orchestration-hardening-tests.txt` proves the focused P12 matrix passes against current integration-test binaries.

## Anti-Stub Proof

`bundle://proof/SB036/transcripts/anti-stub-readonly-orchestration-negative-proof.txt` proves the cross-lane hardening test and mutation-denial path reject shallow/no-op proof.

## Raw-Note Closure

- RN-006 remains partially solved: SB036 hardens process-level read-only orchestration with bounded files and cross-lane evidence/audit/no-mutation coverage. Broader Core genericity and runtime-host roadmap gates remain planned by SB040-SB045.
- RN-007 remains partially solved: SB036 explicitly preserves the no-runtime-host/no-registry/no-selector/no-DI-driver/no-manager-command/no-scheduler-hook/no-workflow-hook boundary.

## Production Behavior Artifact Matrix

Production behavior was not expanded. Existing read-only batch orchestration remains lane-specific and non-mutating; only model placement changed. The new test-only signal verifies cross-lane audit/redaction/hash/no-mutation semantics.
