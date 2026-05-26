# Processes Hardening Follow-up: Runtime Correctness, Contract Enforcement, and Refactoring Gates

## Status

Prepared for Codex execution.

## Branch Context

- Repository: `fyziktom/CanDoItAll`
- Expected branch: `processes-hardening`
- User may refer to it as `process-hardening`; use the actual repository branch if only `processes-hardening` exists.
- Reviewed head: `phase5` / `c66ae4d4dcfc10623fad9f9c926bedf8932917a3`
- Previous reviewed head: `phase4` / `474708e7a09d85a90d9541946e1e0e3dd964ec18`

## Goal

Validate and harden the process runtime after the phase5 implementation. The prior bundle improved persisted operation contracts, operation-aware tool policy, lineage, storage-backed validation, and typed blocked states. This bundle focuses on defects and fragility that remain after those improvements.

## Core Problem Statement

The process runtime is much stronger, but there are still correctness risks where a process can:

1. block unnecessarily,
2. complete with weak/manual artifact validation,
3. deny legitimate product mutation due to read-only alias overlap,
4. allow script-based side effects through imperfect regex inspection,
5. fail to deduplicate artifacts because projection identity is not fully materialized,
6. infer wrong block/recovery classification from broad reason text,
7. route workflow/subprocess artifacts heuristically instead of explicitly,
8. rely on workspace filesystem validation instead of the storage abstraction.

## Non-negotiable Constraints

- Processes are above Workflows. Workflows may execute process roles, but Processes own lifecycle, artifacts, transitions, recovery, and governance.
- Keep the process core generic. Do not make behavior specific to Blazor, .NET, software delivery, or any one business domain.
- PostgreSQL only. Do not add SQLite runtime paths, SQLite migrations, or provider-switching logic.
- Prefer typed persisted state over prompt text, keyword parsing, and bounded display strings.
- Add refactoring checkpoints every few subbundles; do not keep adding more partial classes and heuristics without extracting services.

## Output Required From Codex

- Implement the subbundles in order.
- After each subbundle, update its proof manifest.
- After each refactoring checkpoint, update architecture notes and rerun focused tests.
- At final closure, run full focused + build validation and the bundle validator.

## Validation Summary

- Bundle preparation status: `Ready for execution after structural repair`
- Bundle readiness gate: `Prepared-stage validator passed during execution`
- Execution status: `SB01-SB14 completed`
- Subbundle gate review: `All subbundle entry and closure gates passed`
- Final closure gate: `Completed`
- Browser validation analytics: `SB13 UI proof covered by bUnit component evidence; SB14 introduced no rendered UI change`
