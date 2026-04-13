# Corrective playbook — foundation stabilization

Use this when gate A or an equivalent early proof step fails.

## Typical triggers

- Canonical dependency meaning is still split.
- Validation still mutates state.
- Compatibility behavior leaked outside the allowed boundary.
- Baseline characterization was too weak to trust later refactors.

## Mandatory correction scope

- `ProcessDefinitionModels.cs`
- `ProcessDefinitionEditorModels.cs`
- `ProcessCanvasBranching.cs`
- `ProcessesService.Support.cs`
- any baseline tests added in subbundle 01
- `reviews/01-execution-report.md`
- `reviews/02-architecture-gate-memo-log.md`

## Validation rerun minimum

- prepared-stage validator
- focused integration tests for definition behavior
- focused component tests for workspace/canvas dependency behavior
- rerun gate A

## Unblock condition

Gate A passes with explicit evidence that the dependency model is canonical and validation is pure.
