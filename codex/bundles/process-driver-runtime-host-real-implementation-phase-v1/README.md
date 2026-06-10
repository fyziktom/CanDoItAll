# process-driver-runtime-host-real-implementation-phase-v1

## Status
Prepared for Codex implementation.

## Purpose
This bundle corrects the current implementation pattern: the previous code-first attempt still generated too much `codex/bundles` material compared with real source/test changes. The next step must be a real implementation phase for the generic process driver runtime-host path, not another proof-heavy bundle closure.

## Current Branch Baseline
- Branch: `maf-processes-refactor`
- Current reviewed head: `09d155bc696d15e3bd8d25824f1c321951f4a55a`
- Previous baseline for the latest code-first attempt: `b5149b5a647ea78f367174303b9ba161de53e413`

## Strategic Goal
Move from the current read-only verification host and module-local dry-run host toward a stable, generic process driver runtime-host architecture while still blocking all execution-capable side effects until the later approval gate is genuinely satisfied.

## Required Outcome
Codex must deliver larger coherent implementation areas:

1. A stable runtime-host abstraction boundary in driver/runtime contracts.
2. A refactored process-module runtime host pipeline using those contracts.
3. Durable audit/readback and governance over both verification and dry-run requests.
4. A controlled, explicit capability registry/catalog that is not reflection discovery or self-registration.
5. Scheduler/workflow/manager integration as read-only or dry-run only.
6. A sandbox/authorization gate that produces structured denials and auditable plans.
7. Tests that exercise real code paths rather than proof files.

## Code-First Rule
The implementation must make real code changes first. Bundle/proof edits are allowed only as a minimal coordination layer.

Final closure is blocked unless:

```text
(src + tests changed lines) >= 3 × (codex/bundles changed lines)
```

Docs may be counted separately, but docs must not be used to mask weak implementation. If the ratio is not met, Codex must keep implementing source/test changes or explicitly stop with `Blocked: code-first ratio not satisfied`.

## Hard Constraints
- Do not implement execution-capable drivers yet.
- Do not execute shell commands, package restore, file writes, Office/Graph/CRM calls, workspace/storage writes, transition/finalizer/claim/retry/process mutation through drivers.
- Do not put domain-specific driver concepts into `CanDoItAll.Processes.Core`.
- Do not add reflection discovery, fallback selector, driver self-registration, or implicit DI discovery.
- Do not create another huge proof tree. Critical proof is required, but it must be concise and source-backed.
- Do not add dozens of boilerplate subbundle README files during execution. This bundle already defines the work.
- Do not mark deterministic fallback or skipped live tests as live provider proof.

## Validation Summary Required At Completion
- `git diff --numstat <start-sha>...HEAD` with grouped totals for `src`, `tests`, `docs`, and `codex/bundles`.
- `dotnet build CanDoItAll.slnx --configuration Debug`.
- Full unit test run.
- Focused integration matrix for verification host, dry-run host, audit, scheduler/workflow read-only jobs, manager readback, and live OpenAI process-run smoke where opt-in variables are present.
- Source scans for Core dependency drift, reflection discovery, fallback selector, driver self-registration, side-effect APIs, secret leakage, bundle-path coupling, and large file growth.
