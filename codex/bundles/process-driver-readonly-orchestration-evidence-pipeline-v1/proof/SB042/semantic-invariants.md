# SB042 Semantic Invariants

## Invariant SB042-RUNTIME-HOST-MATRIX-DENIES-ALL-SURFACES
- Invariant ID: `SB042-RUNTIME-HOST-MATRIX-DENIES-ALL-SURFACES`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The current bundle runtime-host decision explicitly keeps every runtime-host surface `Not approved` and every future approval prerequisite `Not satisfied`.
- Disallowed shallow implementation: A short prose-only denial, an incomplete matrix, or text that treats `ExecutionCapableFuture` as an approved permission.
- Failing-first test: No deliberate P14 production compile/test failure was produced; the guard fails if a required row or prerequisite is removed or if an approval claim is added.
- Passing test: bundle://proof/SB042/transcripts/focused-p14-runtime-host-denial-unit-tests.txt and bundle://proof/SB042/transcripts/full-unit-p14.txt
- Changed source files: repo://codex/bundles/process-driver-readonly-orchestration-evidence-pipeline-v1/architecture/04-runtime-host-decision.md and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs
- Production assertions: Runtime-host, registry, selector, DI registration, manager command, scheduler hook, workflow hook, execution-capable drivers, and mutation surfaces remain not approved.
- Red-team negative case: The focused unit test rejects approval claims including runtime host approval, DI approval, scheduler/workflow approval, and `ExecutionCapableFuture is permission`.
- Downstream dependency check: SB043-SB054 can cite a source-backed current decision rather than relying on report-only status text.

## Invariant SB042-CURRENT-PIPELINE-REJECTS-RUNTIME-HOST-HOOKS
- Invariant ID: `SB042-CURRENT-PIPELINE-REJECTS-RUNTIME-HOST-HOOKS`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The scoped read-only driver/gateway/process pipeline contains no runtime host, registry, selector, dependency injection registration, manager command, scheduler hook, workflow hook, service-host, or container-resolution surface.
- Disallowed shallow implementation: Scanning unrelated modules only, excluding the current read-only pipeline, or allowing hook tokens because the docs say they are denied.
- Failing-first test: No deliberate P14 production compile/test failure was produced; the helper would fail if a forbidden runtime-host token appears in any scoped driver/gateway/read-only process target.
- Passing test: bundle://proof/SB042/transcripts/focused-p14-runtime-host-denial-unit-tests.txt and bundle://proof/SB042/transcripts/p14-source-scans.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs
- Production assertions: The source scan permits only the existing `ExecutionCapableFuture` enum marker and rejects runtime hook implementation surfaces.
- Red-team negative case: Source scans also reject direct file/network/storage/workspace APIs, Process Core reverse dependency, stubs, and UI/media drift.
- Downstream dependency check: Documentation and release gates can build on an enforced no-runtime-host boundary, not an aspirational one.
