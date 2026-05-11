# Validation Persistence API And Scenario Seeds

## Status

- `Completed`

## Objective

- Ensure route metadata created by the domain model and workflow canvas survives validation, catalog storage, API round-trip, and realistic sample/scenario creation.
- Add practical seed workflows or test fixtures for IF/ELSE, SWITCH/default, and fan-out patterns so future regression testing can verify routing behavior quickly.
- Confirm that no migration is needed when workflow definitions are stored as JSON; add one only if the current persistence shape requires it.

## Success Criteria

- Workflow definitions with `Routing` metadata can be saved, loaded, listed, validated, and preview-run through the existing workflow APIs.
- Existing definitions without `Routing` metadata still load and validate according to legacy compatibility rules.
- API/integration tests prove route metadata is not dropped by DTO mapping, persistence stores, or catalog services.
- Scenario fixtures include one predicate IF/ELSE, one switch/default, and one fan-out/multi-selection workflow.

## Covered Inputs

- User requirement: route support must be execution-grade, not just local UI state.
- Current-state finding: workflow catalog and API surfaces already exist and must carry the new route contract.
- Architecture requirement: route metadata belongs inside the workflow graph definition rather than a separate route endpoint.
- Validation requirement: incomplete or unsupported route definitions must fail before runtime.

## Prerequisites

- Subbundle 01 completed route contract and validation.
- Subbundle 02 completed compiler/runtime proof.
- Subbundle 03 route-builder mapping available for UI-generated workflow definitions, or at least route DTO shape is stable.

## Exact Source References

- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Deliverables

- Persistence/API round-trip support for `WorkflowEdge.Routing` without losing legacy `ConditionExpression`.
- Validation coverage in API responses for malformed route definitions.
- Scenario/test fixtures for predicate, switch/default, and fan-out workflows.
- Migration or explicit no-migration note based on the current storage shape.
- Integration tests proving saved route workflows load and execute/validate consistently.

## Dependency Impact

- Subbundle 05 final closure depends on API/persistence proof before claiming execution-grade readiness.
- Browser proof in subbundle 03 is incomplete if route metadata is lost after save/load; this subbundle is the durable-data gate.
- Future ARTL implementation depends on route-language persistence being stable.

## Validation Depth

- `Persistence and API compatibility`: API/integration tests plus targeted unit tests are required; browser proof may reuse subbundle 03 evidence but save/load must be demonstrated somewhere.

## Implementation Steps

1. Inspect workflow persistence to determine whether workflow definitions are stored as JSON blobs or mapped columns.
2. Update catalog/API DTO mapping only if current serialization does not automatically include `Routing`.
3. Add validator tests that exercise route errors through API validation responses, not only model-level validation.
4. Add or update fixtures for IF/ELSE, SWITCH/default, and fan-out route workflows.
5. Add integration tests that save a routed workflow, reload it, and assert route fields survive exactly.
6. Add integration or runtime test that preview-runs a saved routed workflow through the normal API path if existing test infrastructure supports it.
7. Add migration only if route metadata requires a schema change; otherwise record the no-migration decision in the execution report.
8. Run targeted catalog/API tests and record proof.

## Scope Exceptions

- Do not add a separate route-management API unless existing API design requires it.
- Do not add production DurableTask/DTS persistence semantics here.
- Do not implement ARTL parser or validation beyond recognizing unsupported `artl-v1` until the later ARTL bundle.

## Do Not Do

- Do not drop unknown route-language values silently.
- Do not persist UI-only route summaries instead of the canonical routing fields.
- Do not migrate old definitions by rewriting their `ConditionExpression` into executable predicates without explicit parser support and tests.
- Do not add relational columns for every route field when JSON graph storage already covers the need.

## Acceptance Checklist

- API save/load round-trip preserves route mode, label, JSON path, operator, expected value kind/value, case sensitivity, language, and fan-out index.
- Validation API surfaces route errors with edge IDs.
- Existing saved workflows without `Routing` remain valid unless they already had unrelated errors.
- Scenario fixtures are available for IF/ELSE, switch/default, and fan-out regression tests.
- Persistence tests and integration tests pass.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WorkflowCatalogTests|FullyQualifiedName~WorkflowFoundationTests" --verbosity minimal -m:1`
- `dotnet test C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~WorkflowApiIntegrationTests --verbosity minimal -m:1`
- Execution-report note: migration required or no migration required, with the exact storage reason.
- Include one saved/reloaded route assertion summary in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Optional unless subbundle 03 browser proof did not include save/load.
- If run here, use route `/agents/workflows`, save a routed workflow, reload or reopen it, and assert the route summary remains visible.
- Evidence file target: `reviews/evidence/subbundle-04/workflow-routing-save-load.png`.

## Progression Gate

- Subbundle 05 may close only after route metadata round-trips through catalog/API persistence or a concrete persistence blocker is recorded with failing test output.

## Suggested Agent Prompt

```text
Implement subbundle 04 only.
Prove WorkflowEdge.Routing survives validation, catalog/API persistence, and realistic scenario fixtures. Add migrations only if storage requires them. Do not implement ARTL and do not rewrite legacy ConditionExpression as executable logic.
```
