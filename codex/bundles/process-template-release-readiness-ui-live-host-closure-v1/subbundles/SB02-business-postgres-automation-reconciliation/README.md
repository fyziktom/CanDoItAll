# SB02: Business PostgreSQL automation reconciliation

## Status
- Status: `Completed`

## Objective
Resolve the mismatch between report claims and source code around PostgreSQL-backed business-analysis automation.

## Covered Inputs
- REQ-002: Fix or confirm PostgreSQL-backed business-analysis automation E2E through production dispatch.

## Prerequisites
- SB01 must be completed with the explicit baseline SHA and proof-classification guard.
- PostgreSQL integration test profile must be available or honestly classified as blocked/skipped in proof.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Deliverables
- PostgreSQL-backed process-mock automation proof for `business-plan-development`, or a corrected explicit classification if the environment blocks it.
- Manual-transition PostgreSQL tests labeled as persistence/state contract tests, not automation E2E.
- Source confirmation that business role mapping does not use software shortcut semantics.

## Dependency Impact
- SB03 and SB07 rely on the business-analysis proof classification being honest.
- SB08 cannot claim merge readiness if this proof is missing or mislabeled.

## Validation Depth
- Integration test proof through launch plan creation, approval, dispatch, outbox drain, finalizer, execution-run readback, and artifact readback.
- Negative proof that automation proof does not use `SuppressAutomationDispatch = true`.
- Source scan for software/.NET/Blazor leakage in business role mapping.

## Implementation Steps
1. Inspect `Business_plan_process_SB05_INV_001...` and determine whether it uses an explicit PostgreSQL profile.
2. If not, add a PostgreSQL-backed process-mock automation test that creates a PostgreSQL test database, enables process-mock agents, imports/publishes `business-plan-development`, creates/approves/executes launch plan, drains outbox, asserts completed run/outbox/execution runs/artifacts through `AppDbContext`, and reads managed artifact files.
3. Keep old manual-transition PostgreSQL tests, but label them as state/persistence contract tests, not automation E2E proof.
4. Verify no software/.NET/Blazor leakage in business template or role mapping.

## Do Not Do
- Do not count manual state transitions as automation E2E.
- Do not use `SuppressAutomationDispatch = true` for representative automation proof.

## Acceptance Checklist
- Explicit PostgreSQL automation test exists and passes or is blocked with concrete environment proof.
- Test uses process-mock launch/approval/dispatch path.
- No manual `SuppressAutomationDispatch = true` in automation proof.
- Business role mappings do not use software shortcut semantics.

## Proof Required
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Failing-first or adversarial transcript proving manual/suppressed dispatch is rejected as automation proof.
- Passing transcript for the PostgreSQL automation or explicit blocked classification.

## Browser Validation Logging
- No browser proof required for SB02; execution report should record `N/A` outside browser analytics.

## Progression Gate
- SB03 may start only after business automation is either proven through PostgreSQL-backed dispatch or explicitly marked as blocked without being counted as E2E proof.

## Suggested Agent Prompt
Implement only the business PostgreSQL automation reconciliation for SB02, record artifact-backed proof, update report rows, then run the closure gate before SB03.
