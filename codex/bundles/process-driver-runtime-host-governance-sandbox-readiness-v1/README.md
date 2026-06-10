# process-driver-runtime-host-governance-sandbox-readiness-v1

## Status
Prepared for Codex implementation.

## Purpose
Move the current `maf-processes-refactor` branch from a working read-only verification-host beta toward a **governed generic process driver runtime host readiness layer** while preserving the current rule: execution-capable drivers remain blocked until a separate source-backed approval gate passes.

This bundle intentionally builds the missing operational foundations before allowing any driver to execute commands, mutate workspace/storage, call Office/Graph/CRM, or mutate process state.

## Why this bundle exists
The latest branch now has real process runtime proof and a live OpenAI process-run smoke. It also has a first verification runtime host. The next risk is architectural: the verification host can silently turn into a generic execution host unless lifecycle ownership, durable audit, health, manager readback, scheduler/workflow job execution, sandbox policy, and future execution approval gates are designed and tested now.

## High-Level Scope
- Reconcile the latest real source/test state.
- Harden durable EF audit as the production default and prove it across service scopes/profile restart.
- Make all production paths async/cancellable and keep the sync wrapper compatibility-only.
- Add host health/readiness/emergency-disable and operator readback.
- Execute scheduler/workflow read-only verification jobs without driver execution hooks.
- Add manager API/UI large-screen proof for verification diagnostics and audit readback.
- Add dry-run sandbox/allow-list contracts for future execution-capable drivers without executing anything.
- Keep Process Core generic and dependency-clean.
- Keep execution-capable domain drivers blocked and future-gated.

## Bundle Shape
- 20 phases.
- 60 subbundles.
- Critical gate every third subbundle.
- XLSX checklist under `evidence/checklists`.
- Browser proof: large desktop only when UI/readback changes.

## Required Validation
- `dotnet build CanDoItAll.slnx --configuration Debug`
- full unit tests
- focused integration tests for process runtime, verification host, EF audit, scheduler/workflow read-only jobs, manager facade, live smoke classification
- large-desktop Playwright process-run-detail manager diagnostics proof
- source scans for Core reverse dependency, driver mutation hooks, host fallback, reflection discovery, bundle-path coupling, secret leakage, UI drift
- prepared and completed bundle validators