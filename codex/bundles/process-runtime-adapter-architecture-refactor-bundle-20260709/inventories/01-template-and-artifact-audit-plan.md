# Template And Artifact Audit Plan

## Template Locations To Inspect

- `src/Processes/CanDoItAll.Processes.Templates`
- Driver template fragments contributed through `IProcessTemplateFragmentProvider`
- Process definition/template JSON files that declare completion, branch, receipt, or tool-plan metadata
- Artifact templates used by managed process outputs
- Launch variable contributors that provide script refs, execution plans, side-effect manifests, required receipts, acceptance criteria, or product paths

## Required Inventory Columns

Each audited item must be recorded with:

- Path.
- Template/process/artifact name.
- Domain terms present.
- Required receipts present.
- Branch applicability present.
- Tool-critical placeholders present.
- Deterministic plan as typed data or prompt-only prose.
- Artifact ledger/slot usage.
- Migration decision.
- Tests covering it.

## Migration Decision Values

- `NoChange`: Template is already typed enough and no generic leak depends on it.
- `TypedContractRequired`: Template needs structured fields for receipts, tool plans, branch routes, acceptance criteria, or placeholders.
- `DriverPolicyRequired`: Domain-specific behavior must move to a driver policy.
- `ArtifactContractRequired`: Artifact template must declare ledger/slot or evidence expectations.
- `ObsoleteOrUnsafe`: Template should be removed or blocked.

## Audit Completion Criteria

- At least all process templates touched by software-delivery, multi-team development, subprocess, QA, repair, and managed artifact flows are audited.
- At least one non-Tetris/non-calculator process path is included in regression coverage.
- Generic runtime/dispatcher source assertions prove domain terms did not move into generic code during migration.

