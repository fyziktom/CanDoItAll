# SB06 Semantic Invariants

## Invariants

- `SB06-INV-001`: Required tool names resolve through a typed resolver boundary rather than ad hoc call sites.
- `SB06-INV-002`: Browser proof requirements resolve through a typed requirement result that carries runtime/proof context.
- `SB06-INV-003`: Artifact matching and completion/retry policy are exposed as typed decisions with diagnostics.
- `SB06-INV-004`: Existing special cases for setup scaffolds, console validation, explicit QA repair disposition, and unresolved critical diagnostic tool failures remain behaviorally stable.
- `SB06-INV-005`: The refactor must not introduce scenario-key branches.

## Evidence

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DecisionServices.cs`
- `bundle://proof/SB06/transcripts/passing-dispatch-decision-services.txt`
- `bundle://proof/SB07/transcripts/template-contract-and-scenario-scan.txt`

## Residual Risk

The dispatch class remains large. This pass creates typed extraction points and test coverage; further physical decomposition can be done incrementally after the SB04/SB05/SB09 gates remain green.
