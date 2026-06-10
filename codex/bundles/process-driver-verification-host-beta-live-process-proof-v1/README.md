# process-driver-verification-host-beta-live-process-proof-v1

## Status
Prepared for Codex implementation.

## Purpose
This bundle follows the completed `process-runtime-live-openai-verification-host-alpha-v1` work. The branch now has a restored deterministic process runtime, a guarded live OpenAI specialist-agent smoke, and a first **verification-only** process driver runtime host alpha. The next objective is to make that host dependable enough for manager/process diagnostics and to prove one **live process-run** path, without jumping prematurely to execution-capable domain drivers.

## Strategic Goal
Move toward a stable generic Process Core with domain drivers by hardening the read-only verification host into a beta-quality runtime component while preserving the existing process execution path through `ProcessesService`, durable outbox, dispatch, MAF workflow/direct-agent execution, finalizer, artifacts, and project-structure integration.

## What This Bundle Must Prove
- The previous work is checked from real source, not just its report.
- The live OpenAI proof is classified correctly: current proof is a live specialist-agent smoke, not a live process-run proof.
- A bounded **live process-run** smoke is added as opt-in, with explicit budget/timeout/model, secret redaction, and deterministic fallback.
- `IProcessVerificationRuntimeHost` is hardened toward beta: async/cancellable API, non-throwing expected denial result, host options, lane policy, durable audit boundary, and read queries.
- Manager-readonly verification command and process diagnostic projection remain read-only and are made observable enough for UI/API integration later.
- Scheduler/workflow integration remains process-service centered; any verification scheduling is read-only and does not become a driver execution hook.
- Process Core remains generic and dependency-clean.

## Explicit Non-Goals
- No execution-capable process drivers.
- No shell/package restore through drivers.
- No Office/Graph/CRM/network calls through drivers.
- No workspace/storage/process mutation through drivers.
- No claim, transition, finalizer, retry, provider repair, or scheduler mutation through drivers.
- No fallback selector, plugin discovery, reflection-based driver loading, or `object` payload dispatch.
- No Process Core dependency on drivers, modules, infrastructure, UI, EF, workspace, storage, or AgentFramework.
- No small/medium/mobile browser proof. Large desktop only where UI proof is required.

## Bundle Shape
- 22 phases.
- 66 implementation subbundles.
- Critical gate every third subbundle.
- XLSX checklist under `evidence/checklists`.
- Runtime/UI proof is large-screen only when UI is touched.

## Required Validation
- `dotnet build CanDoItAll.slnx --configuration Debug`
- full unit tests
- focused process runtime integration matrix
- focused verification host integration matrix
- guarded live OpenAI specialist-agent smoke classification
- guarded live process-run OpenAI smoke, opt-in only
- source scans for bundle-path coupling, Core reverse dependency, driver mutation/runtime-host drift, stubs, UI/media drift, and secret leakage
- prepared and completed bundle validators
- red-team proof rejecting report-only, deterministic-only, live-skip-as-pass, and generic-host-by-accident closures
