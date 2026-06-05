# process-dispatch-artifact-satisfaction-evidence-boundary-v1

Status: Prepared for Codex implementation.

## Mission

Continue the `maf-processes-refactor` dispatcher decomposition after the implementation-proof/evidence boundary bundle.

This bundle targets the remaining large artifact satisfaction and evidence-validation logic in:

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- adjacent module-local helpers already extracted by prior bundles

The goal is **not** to create `Process Core` yet. The goal is to create one more stable module-local boundary that will later make Process Core extraction and process helper drivers safer.

## Non-negotiable constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not add production process-driver APIs, packages, registries, or `IProcessDriverPack`.
- Do not remove any existing runtime behavior.
- Do not simplify artifact satisfaction by dropping branch ordering.
- Do not move file/storage/DbContext/service-scope/transition side effects into pure rule helpers.
- Do not perform small/medium/mobile proof. This is runtime/service work; browser proof should remain `N/A`.

## Why this is next

The previous bundle reduced `ImplementationProof.cs` to a much smaller wrapper and extracted contract/stack/path/receipt/runnable/.NET/carry-forward rules. However, `ArtifactValidation.cs` still owns broad evidence-satisfaction orchestration: missing required artifact summaries, auto-satisfaction, provider-native browser output, response text eligibility, external-target reference checks, shallow managed artifact checks, and quality validation evidence aggregation.

This is the next safe seam before any Process Core discussion.
