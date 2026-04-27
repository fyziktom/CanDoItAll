# Requirements

## R00 — Snapshot integrity and truthful reporting

Codex must verify that claimed files/classes/tests exist in the final snapshot. Execution reports must be backed by file paths and test names.

## R01 — Secret emergency remediation

Remove the committed provider key from `appsettings.json`, rotate/revoke it outside the repo, add secret scanning, and ensure no generated output contains raw secrets.

## R02 — Process mutation tool classification

Classify all process state-changing tools as mutation tools or a stricter category. Read-only process tools must remain read-only.

## R03 — Process mutation approval and policy enforcement

Process mutation tools must be explicitly governed by approval wrappers, execution policy, or documented governed auto-approval rules. Hidden bypasses are not allowed.

## R04 — Typed recovery decision

Introduce a typed `AgentRecoveryDecision` with explicit modes: `FormatRepair`, `FreshStepRetry`, `ReworkContinuation`, `ProviderFallback`, and `HumanEscalation`.

## R05 — Typed rework packet

Introduce `AgentReworkPacket` and related models for QA/build/test/browser rework. Use it in repair prompts and persist it with the attempt ledger.

## R06 — Efficient context selection

Implement explicit context strategies that decide when to reuse, summarize, or discard failed-run context. MAF session must not be the process source of truth.

## R07 — QA return to rework loop

QA rejection must propagate typed findings into a rework packet and route to a repair step that preserves completed work and reruns invalidated proofs.

## R08 — Proof fingerprints and receipt reuse

Implement proof fingerprints so successful proof receipts can be reused only when relevant inputs have not changed.

## R09 — Retry ledger, backoff, and loop control

Persist attempt lineage, retry reasons, backoff, provider fallback decisions, repeated tool signatures, and escalation conditions.

## R10 — Finalizer sequence trace hardening

For governed required-finalizer runs, finalizer sequence validation must be behaviorally tested. Missing trace data must produce a deterministic policy decision, not silent ambiguity.

## R11 — Default test gate stabilization

Make a documented default `dotnet test` gate green, either by fixing the full suite or moving heavy/obsolete tests behind explicit traits with rationale.

## R12 — Playwright fixture Release/no-build fix

Playwright fixtures that launch the app with `dotnet run --no-build` must pass the active configuration and stop assuming Debug output.

## R13 — MCP stdio path/configuration fix

MCP stdio integration tests must not hardcode Windows repo roots or Debug assembly paths.

## R14 — ProjectStructure host stabilization

Fix host lifetime/service replacement issues in ProjectStructure integration tests and add coverage for the stabilized host factory.

## R15 — Component test modernization

Replace obsolete brittle component/canvas assertions with semantic assertions or move browser-specific checks to Playwright.

## R16 — Storage/project-structure isolation

Stabilize storage and project-structure integration tests through isolated temp roots, profiles, deterministic cleanup, and no shared mutable global state.

## R17 — DotNetWatch/live-process gate

Live-process/dotnetwatch tests must be deterministic and serial, or moved behind explicit `LiveProcess`/`LongRunning` gates.

## R18 — Documentation truthfulness

Update docs to reflect what exists, what passed, what is quarantined, and what remains risky. Remove inaccurate claims from earlier verification docs.
