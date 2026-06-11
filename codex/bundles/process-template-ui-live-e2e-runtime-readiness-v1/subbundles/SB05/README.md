# SB05: PostgreSQL business-analysis automation

## Status
Prepared.

## Objective
Move business-analysis automation proof from in-memory/process-service confidence to PostgreSQL-backed automation confidence.

## Exact Source References
- repo://Templates/Processes/processes/business-plan-development/definition.json
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs

## Deliverables
- Add PostgreSQL-backed business-plan automation E2E using process-mock agents and outbox drain.
- Keep the existing manual-transition PostgreSQL proof only as a state/persistence contract test.
- Verify no software/.NET/Blazor leakage in business template steps, artifacts, and execution readback.
- Verify business artifacts: strategy, product evidence, business plan, financial model, marketing plan, integrated review.

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

## Browser Validation Logging
N/A.

## Progression Gate
SB06 may proceed after non-software automation is persisted and read back through PostgreSQL.
