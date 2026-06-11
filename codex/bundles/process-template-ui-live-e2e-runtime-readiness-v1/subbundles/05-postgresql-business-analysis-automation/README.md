# SB05: PostgreSQL business-analysis automation

## Status
- Status: Completed

## Objective
Move business-analysis automation proof from in-memory/process-service confidence to PostgreSQL-backed automation confidence.

## Covered Inputs
- Raw request: identify what is still missing or broken after refactoring for representative process execution.
- REQ-005: prove business-analysis automation on PostgreSQL without software/.NET domain leakage.

## Prerequisites
- SB04 closure gate passed and representative automation is production-path.
- Local PostgreSQL integration-test prerequisites are available or the subbundle records a concrete environment blocker.

## Exact Source References
- repo://Templates/Processes/processes/business-plan-development/definition.json
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs

## Deliverables
- Add PostgreSQL-backed business-plan automation E2E using process-mock agents and outbox drain.
- Keep the existing manual-transition PostgreSQL proof only as a state/persistence contract test.
- Verify no software/.NET/Blazor leakage in business template steps, artifacts, and execution readback.
- Verify business artifacts: strategy, product evidence, business plan, financial model, marketing plan, integrated review.

## Dependency Impact
- SB06 can attach runtime-host readback to representative runs only after both software and business scenarios have persisted automation evidence.
- SB08 cannot claim generic process restoration if this subbundle falls back to in-memory proof.

## Validation Depth
- Run PostgreSQL-backed integration tests that create/drop their own database or schema.
- Verify persisted outbox, execution runs, artifacts, and readback through PostgreSQL.
- Include semantic adequacy proof, manifest, negative software-leakage evidence, passing transcript, source assertions, and anti-stub audit under `proof/SB05/`.

## Implementation Steps
- Audit existing PostgreSQL business-plan tests and separate manual-transition persistence proof from automation proof.
- Add process-mock automation dispatch and outbox drain on PostgreSQL.
- Assert persisted business artifacts and reject software/.NET/Blazor leakage in steps, artifacts, and readback.
- Capture transcripts and scans.

## Do Not Do
- Do not use software roles or software shortcuts in the business-analysis proof.
- Do not claim in-memory automation as PostgreSQL automation.
- Do not call live OpenAI in this subbundle.

## Acceptance Checklist
- PostgreSQL database is created/dropped by the test.
- Process run completes through process-mock automation.
- Outbox/execution runs/artifacts are persisted and read back from PostgreSQL.
- Non-software assertions pass.

## Proof Required
- PostgreSQL integration transcript.
- Source scan for software-domain terms in business run assertions.
- Completion manifest: `bundle://proof/SB05/manifest.md`
- Semantic invariants: `bundle://proof/SB05/semantic-invariants.md`
- PostgreSQL transcript: `bundle://proof/SB05/transcripts/postgresql-integration.txt`
- Non-software leakage scan: `bundle://proof/SB05/transcripts/business-nonsoftware-leakage-scan.txt`
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Browser Validation Logging
- N/A: this subbundle has no browser-visible behavior.

## Progression Gate
- SB06 may proceed only after non-software automation is persisted and read back through PostgreSQL or an explicit environment blocker is recorded.
- Reopen SB05 if later proof discovers business artifacts were seeded manually or persisted only in memory.

## Suggested Agent Prompt
- Implement SB05 by adding PostgreSQL-backed business-plan automation proof with process-mock dispatch, persisted outbox/execution/artifact readback, and non-software leakage checks. Store proof under `proof/SB05/`.
