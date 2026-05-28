# 02-workflow-domain-model-and-template-loader-hardening

## Status

- `Prepared`

## Objective

Harden the repository workflow definition model and template loader so invalid graphs, routes, executor references, and runtime policies fail before execution.

## Success Criteria

- A reusable workflow graph validator exists or an existing one is hardened.
- Template pack loading validates the full graph, not just YAML and enum syntax.
- Validation diagnostics include template key/source file/node/edge identifiers.
- Existing valid templates still load.
- Tests cover invalid graph and route cases.

## Covered Inputs

- R03, R04, R06, R10, R13, R15

## Prerequisites

- SB01 inventory complete.
- MAF version baseline decision recorded.

## Exact Source References

- `Templates/Workflows/manifest.yaml`
- `Templates/Workflows/workflows/*.yaml`
- `src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- Workflow model types found by SB01.
- Existing workflow template tests found by SB01.

## Deliverables

- Validator or hardened validation service for repository workflow graphs.
- Loader refactor if needed: parser/normalizer/validator/model mapper boundaries.
- Route validation for `BuiltInJsonV1`.
- Tests for valid templates and representative invalid templates.

## Validation Rules To Cover

- Duplicate node IDs.
- Duplicate edge IDs.
- Missing or duplicate start node.
- Edge source/target not found.
- Invalid or missing ports where ports are explicit.
- Node kind incompatible with settings.
- Executor ID missing or unknown when node kind requires it.
- Executor settings JSON invalid or incompatible with descriptor schema when descriptors are available.
- Runtime/executor policy outside allowed limits.
- Route operator/value kind mismatch.
- Invalid expected JSON value.
- Case sensitivity behavior.
- Fan-out target index invalid or ambiguous.
- End node reachability.
- Non-terminal dead ends.
- Cycles either rejected or explicitly allowed with bounded semantics.

## Implementation Steps

1. Reuse existing validation types if present; otherwise introduce a small validation result model.
2. Split YAML parsing errors from semantic graph validation errors.
3. Add validation immediately after template graph conversion and before seeding/saving.
4. Add validation in the workflow save path for UI-created definitions.
5. Add targeted unit tests with invalid mini-graphs.
6. Update execution report and proof.

## Scope Exceptions

- Do not implement native MAF compilation here except for validation affordances needed by SB03.
- Do not alter plugin runtime behavior here; only use plugin descriptors if they already exist from inventory.

## Do Not Do

- Do not silently fix invalid templates during loading.
- Do not downgrade validation errors to warnings for new/edited definitions unless a compatibility gate requires it.
- Do not hard-code a list of known templates in C#.

## Acceptance Checklist

- All existing template files load and validate.
- Invalid graph tests fail with actionable diagnostics.
- Managed seed safety behavior remains intact.

## Proof Required

- Targeted unit test transcript.
- Source assertions showing no hard-coded workflow examples were added.
- Updated execution report row.

## Progression Gate

SB03 may start only after repository workflow definitions have a stable validation boundary.

## Suggested Agent Prompt

```text
Implement SB02 only. Harden workflow graph/template validation and keep the template pack file-backed. Capture failing-first and passing tests.
```
