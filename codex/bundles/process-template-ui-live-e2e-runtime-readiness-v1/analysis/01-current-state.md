# Current State Analysis

## What improved in the last bundle

The previous bundle was substantially better than earlier proof-heavy bundles. It added a real automation harness in `ProcessTemplateAutomationTestSupport` that:

- creates a project,
- imports and publishes a template,
- creates a launch plan,
- selects process-mock technical agents for template roles,
- submits and approves the launch plan,
- executes the launch plan,
- drains the process outbox through `ProcessOutboxService.ProcessPendingAsync`,
- verifies completed outbox records,
- reads AgentFramework execution runs by process run id,
- verifies artifacts and finalizer summaries.

New template E2E tests use this harness for Blazor app delivery and software-delivery / multi-team representative flow.

Business-analysis also gained an automation E2E test with process-mock agents, plus existing PostgreSQL business-plan projection/persistence tests.

## Remaining gaps

The system is much closer to “processes work again”, but it is not yet fully restored from the user/operator perspective.

1. UI/browser launch proof is still missing. The previous bundle explicitly reports no UI route/component changes and no browser proof.
2. Live OpenAI was not run in the last bundle. The previous live process-run proof exists, but the latest representative-template proof used process-mock agents only.
3. Business-analysis automation proof is not yet PostgreSQL-backed in the same way as the manual business-plan PostgreSQL proof.
4. Multi-team development is still represented by `software-delivery`; that may be acceptable, but UI labels/aliases and operator wording must make this unambiguous.
5. Repair/rework path is not strongly proven for the representative template E2E; most recent template E2E validates the happy path and skipped repair steps.
6. Runtime-host readback exists, but the next step should attach it to real template run details and UI/operator readback.
7. Scheduler/workflow-origin read-only verification job lifecycle is modeled, but needs stronger process-run/provenance/readback proof.
8. ProcessMockAgentRuntime was split and improved, but the new partial files should be watched for size and responsibility creep.

## Architectural judgment

The next bundle should not be another abstraction pass. It should be a product/runtime confidence pass:

- user-facing launch paths,
- real template automation paths,
- live-provider opt-in path,
- PostgreSQL-backed representative scenarios,
- operator/run-detail readback,
- repair/rework path,
- final release matrix.
