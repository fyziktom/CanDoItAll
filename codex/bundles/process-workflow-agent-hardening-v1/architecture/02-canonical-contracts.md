# Canonical Contracts

## Contract Ownership Rule

Every repeated concept must be classified as one of:

1. **Internal canonical contract**: must be owned by code constants/descriptors and reused.
2. **External protocol boundary**: may remain a string but must be wrapped by a descriptor and documented.
3. **Template content**: may remain in JSON/markdown but must be validated against canonical descriptors.
4. **UI display-only string**: can remain localized/human-readable but must not drive runtime behavior.
5. **Test fixture data**: may use literals but must cite the canonical source and avoid becoming production behavior.

## Initial Contract Inventory

| Concept | Candidate owner | Examples |
| --- | --- | --- |
| Workspace command tool ids | `ToolContractCatalog` | `workspace_dotnet_run`, `workspace_dotnet_new` |
| Browser tool ids | `ToolContractCatalog` | `browser_take_screenshot`, `browser_navigate` |
| Workflow executor ids | `WorkflowExecutorContractCatalog` | `office365.messages-by-category`, `office365.mark-message-processed` |
| Operation target scopes | `ProcessContractCatalog` | `ExternalProductTargetReadOnly`, `ExternalProductTargetMutable` |
| Step allowed operations | `ProcessContractCatalog` | `RunValidation`, `CaptureRuntimeProof`, `MutateProductTarget` |
| Artifact satisfaction status | `EvidenceContractCatalog` | `Satisfied`, `Missing`, `Invalid`, `Stale` |
| Runtime host profile | `EvidenceContractCatalog` | host URL, DB profile, build root, cleanup receipt |
| Provider usage phase | `ProviderUsageContractCatalog` | `normal-run`, `finalizer-short-circuit`, `structured-output-repair` |
| Capability proof status | Agent capability descriptor | available, unavailable, unverified, broken |
| Enum HTTP shape | API display adapter | numeric wire value + string label |

## Validation Scanner Requirement

SB01 must create or extend a scanner that reports:

- new internal string ids not in canonical descriptors
- repeated JSON paths not in canonical descriptor inventory
- template operation ids not recognized by canonical catalog
- workflow executor ids not recognized by executor catalog
- browser proof references not recognized by tool contract catalog
- skills/templates referencing removed MCP assumptions
- UI display code branching on raw numeric enum values without adapter

The scanner can start as an integration test or command-line test helper. It must be easy to run in CI.

## Contract Drift Report

SB01 must produce `proof/SB01/contract-drift-report.md` with:

- all currently accepted exceptions
- owner for each exception
- reason why it is external/template/test-only
- planned follow-up if the exception is temporary
