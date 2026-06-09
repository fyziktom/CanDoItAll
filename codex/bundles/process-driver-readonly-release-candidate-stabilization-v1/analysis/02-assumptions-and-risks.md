# Assumptions And Risks

## Assumptions
- Current branch is `maf-processes-refactor`.
- Codex will use GPT-5.5 extra high and can handle larger coherent phases.
- Work remains backend/runtime/Core/driver-only unless source drift says otherwise.
- Browser validation remains N/A unless UI/media files change unexpectedly; unexpected UI/media drift must fail the bundle and trigger re-scope.

## Critical Path Risks
1. Splitting adapters changes behavior or weakens no-mutation proof.
2. A typed batch gateway slowly becomes generic runtime dispatch.
3. Process-module project references to all driver packages become ungoverned coupling.
4. Observation projection planning accidentally becomes persistence, UI, or manager-command implementation.
5. Runtime-host roadmap prose accidentally approves implementation.
6. Full-unit proof regresses into owned skips or stale architecture fixtures again.
7. Evidence policy diverges across transcript/runtime/artifact/Office/business lanes.
8. New proof is table-only and not source/test-backed.

## Validation Risks
- Build-only proof is insufficient.
- Focused tests can pass while source scans miss new generic dispatch.
- Direct process-module references can bypass the explicit gateway.
- README samples can imply runtime use even if code is safe.

## Reopen Triggers
- Any `CanDoItAll.Processes.Core` reference to `CanDoItAll.Processes.Drivers`.
- Any `Verify(object)`, string-lane dispatch, reflection selector, registry, provider, host, service registration, hosted service, scheduler hook, workflow hook, or manager command in verification paths.
- Any file/network/storage/workspace read/write from verification packages, gateway, process adapters, payload builders, or orchestration.
- Any process mutation, claim, transition, finalizer, retry, provider repair, shell execution, Office/Graph call, or business-record mutation.
- Any full-unit failure or skipped test not explicitly owned with a current-source replacement.
- Any critical manifest missing source assertions, command transcripts, changed-file hashes, semantic positive proof, adversarial negative proof, and anti-stub audit.
