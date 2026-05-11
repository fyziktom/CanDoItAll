# Routing Domain Contracts And Compatibility

## Status

- `Ready`

## Objective

- Add a stable, serializable routing contract to workflow edges so the workflow graph can express direct edges, simple predicates, switch cases/defaults, and fan-out selectors without relying on arbitrary code or a higher-level orchestration layer.
- Preserve existing saved workflow definitions that only contain `WorkflowEdge.Kind` and `ConditionExpression` while making the new `Routing` contract authoritative for execution.
- Create the replacement seam that lets ARTL become the route language later without changing the MAF compiler or workflow canvas ownership model again.

## Success Criteria

- `WorkflowEdge` can represent a direct route, binary predicate route, switch case, switch default, and fan-out selector route.
- Existing serialized workflow definitions remain readable; missing routing metadata defaults to direct/legacy-safe behavior.
- Legacy `ConditionExpression` is retained but is not silently treated as executable C# or MAF predicate logic.
- Validator reports route-shape errors before runtime compilation.
- Unit tests cover route serialization, defaults, legacy compatibility, invalid route definitions, and ARTL placeholder rejection.

## Covered Inputs

- User requirement: use Microsoft Agent Framework's prepared routing primitives for the current phase rather than implementing a custom higher-level IF/SWITCH engine.
- User requirement: keep the future ARTL DSL replacement path open.
- Current-state finding: `WorkflowEdgeKind` already contains `Direct`, `Conditional`, `FanOut`, and `FanIn`, but there is no executable typed routing contract.
- Current-state finding: `ConditionExpression` exists today and must not be broken for saved definitions.
- Architecture requirement: no arbitrary C# or user script evaluation in workflow definitions.

## Prerequisites

- Confirm the repo still uses the workflow domain files listed in `Exact Source References`.
- Confirm no implementation after bundle preparation already added a competing route model.
- Read `architecture/01-target-solution.md` and `templates/routing-contract-proposal.md` before editing.

## Exact Source References

- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowIdJsonConverters.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`

## Deliverables

- New route model types under `CanDoItAll.AgentFramework.Models`, preferably in `WorkflowModels.cs` or a nearby workflow-routing file if project style allows.
- Updated `WorkflowEdge` contract with a defaulted `Routing` property while preserving constructor compatibility and `ConditionExpression` behavior.
- Safe built-in route language constants: `built-in-json-v1`, `legacy-condition-expression`, and `artl-v1`.
- Validation rules for route kind, JSON path, expected value, unsupported language, duplicate switch default, fan-out index collisions, and predicate-required fields.
- Unit tests proving old and new workflow definitions serialize and validate correctly.

## Dependency Impact

- Subbundle 02 cannot safely compile MAF predicates until this subbundle defines the route contract and validation semantics.
- Subbundle 03 depends on the route model to present a route builder rather than free-form text.
- Subbundle 04 depends on the exact serialized contract for persistence/API round-trip.
- Subbundle 05 depends on these contracts for the ARTL handoff and final proof matrix.

## Validation Depth

- `Critical foundation`: this subbundle must reach model, serializer, validator, and unit-test depth before downstream compiler/UI work proceeds.

## Implementation Steps

1. Add `WorkflowRouteKind`, `WorkflowRouteOperator`, `WorkflowRouteValueKind`, `WorkflowRoutingLanguages`, and `WorkflowEdgeRouting` using the shape in `templates/routing-contract-proposal.md` as the starting point.
2. Add `WorkflowEdgeRouting.Always` and factory helpers for predicate, switch case, switch default, and fan-out selector if they keep call sites readable.
3. Update `WorkflowEdge` so `Routing` defaults to `Always` for old JSON and existing constructor call sites.
4. Keep `ConditionExpression` as a compatibility field; do not remove it and do not make it executable by default.
5. Extend `WorkflowDefinitionValidator` to validate route kinds and grouped outgoing edges.
6. Add validation errors that include `WorkflowEdgeId` and actionable text for malformed route definitions.
7. Add unit tests for old JSON without `Routing`, new JSON with route metadata, invalid expected JSON, unsupported `artl-v1`, duplicate switch default, and fan-out index conflicts.
8. Run targeted workflow foundation unit tests and record proof in `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not implement ARTL parser, AST, or advanced expression language in this subbundle.
- Do not change MAF compilation behavior here except where test fixtures need the new model.
- Do not redesign workflow persistence schema unless adding the `Routing` property proves impossible without a migration.

## Do Not Do

- Do not evaluate arbitrary C#, JavaScript, Dynamic LINQ, reflection expressions, or user-provided code.
- Do not remove or repurpose `ConditionExpression` in a way that breaks existing saved definitions.
- Do not hide unsupported route-language errors by downgrading them to direct edges.
- Do not add route-specific UI controls in this subbundle.

## Acceptance Checklist

- `WorkflowEdge.Routing` defaults safely for existing definitions.
- New route metadata serializes and deserializes without losing enum or value-kind fields.
- Validator rejects unsupported route languages, malformed required fields, duplicate switch defaults, and conflicting fan-out order.
- Legacy `ConditionExpression` remains visible to API/UI layers as compatibility data.
- Unit tests prove new and old graph contracts.

## Proof Required

- `dotnet test /mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WorkflowFoundationTests|FullyQualifiedName~WorkflowCatalogTests" --verbosity minimal -m:1`
- Add or update test names in `reviews/01-execution-report.md`.
- Include a short serialized JSON sample in the execution report or test assertion that shows `Routing` round-trips.

## Browser Validation Logging

- `N/A`: no browser-visible surface is changed in this subbundle.
- Downstream browser proof is owned by subbundle 03 after runtime and UI wiring exist.

## Progression Gate

- Subbundle 02 may start only after route metadata is validated, legacy definitions load, and targeted unit tests pass or a blocker is recorded with exact failing tests.

## Suggested Agent Prompt

```text
Implement subbundle 01 only.
Add the workflow routing domain contract, defaulting, validation, and compatibility tests. Keep ConditionExpression as legacy compatibility data, do not make it executable, and do not implement ARTL. Record targeted test proof in reviews/01-execution-report.md before stopping.
```
