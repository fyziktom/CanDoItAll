# SB02 Semantic Invariants

## SB02_INV_001 Unknown Tools Do Not Fall Back To Read

Invariant: a tool name that is neither in `ToolCapabilityRegistry` nor an explicitly modeled provider-native/MCP family must classify as `Unknown` and be denied by policy.

Implementation points:

- `ToolCapabilityRegistry.Classify` returns `Unknown` for unregistered names.
- `DefaultAgentToolInvocationPolicy.EvaluateAsync` denies `ToolInvocationClassification.Unknown`.

Proof:

- Failing-first: `bundle://proof/SB02/transcripts/failing-first-tool-capability-registry.txt`
- Passing: `bundle://proof/SB02/transcripts/passing-tool-capability-registry.txt`
- Full policy class: `bundle://proof/SB02/transcripts/passing-agent-tool-policy-tests.txt`

## SB02_INV_002 Every Known Catalog Tool Has Canonical Metadata

Invariant: every `ToolContractCatalog.KnownToolNames` entry must exist exactly once in `ToolCapabilityRegistry`.

Implementation points:

- `ToolCapabilityRegistry.Capabilities` is the canonical source of tool metadata.
- `AgentToolInvocationPolicyMetadata.Tools`, `Classify`, `IsMutationTool`, `IsValidationTool`, and `RequiresApprovalByDefault` delegate to the registry.

Proof:

- Registry completeness test: `ToolCapabilityRegistry_SB02_INV_004_registers_every_known_catalog_tool`
- Source assertions: `bundle://proof/SB02/source-assertions.txt`

## SB02_INV_003 High-Risk Tools Require Explicit Operations

Invariant: `workspace_command_run`, `local_mcp_launch`, and browser proof/interaction tools must not execute in governed process steps unless the step declares the matching process operation.

Implementation points:

- Command and local MCP launch require `ExecuteExternalAction`.
- Browser proof and interaction tools require `CaptureRuntimeProof`.
- Dynamic workspace file/script/dotnet-run/artifact tools resolve through `ToolOperationRequirementResolver`.

Proof:

- Command negative test: `EvaluateAsync_SB02_INV_003_denies_command_run_without_execute_external_action_operation`
- Static high-risk metadata test: `ToolCapabilityRegistry_SB02_INV_005_declares_static_operation_requirements_for_high_risk_tools`
- Full policy class: `bundle://proof/SB02/transcripts/passing-agent-tool-policy-tests.txt`

## SB02_INV_004 Registry Metadata Describes Side Effects And Target Scopes

Invariant: capability metadata must expose side-effect descriptors, target-scope requirements, approval defaults, capability flags, browser proof role, and idempotency descriptor without callers re-parsing tool names.

Implementation points:

- `ToolCapabilityMetadata` includes `SideEffectKind`, `OperationRequirementKind`, `TargetScopeRequirements`, capability flags, `BrowserProofRole`, and `IdempotencyDescriptor`.
- Target scopes derive from the same strongly typed `ProcessOperationContractNames` contract names used by process definitions.

Proof:

- Metadata test: `ToolCapabilityRegistry_SB02_INV_006_declares_side_effect_target_scope_and_proof_metadata`
- Source assertions: `bundle://proof/SB02/source-assertions.txt`

## SB02_INV_005 Provider-Native And MCP Families Are Explicitly Modeled

Invariant: hosted provider-native, hosted MCP, and local MCP tool families are classified only by their explicit family prefixes; arbitrary unregistered tool names remain `Unknown`.

Proof:

- `Classify_returns_expected_tool_classification` covers provider-native, hosted MCP, and local MCP classification.
- Unknown-tool tests cover unregistered workspace/browser/arbitrary names.

## Shallow-Pass Trap

A shallow fix that only changes `AgentToolInvocationPolicyMetadata.RegisteredTools` would still allow catalog drift and stale fallback behavior. `ToolCapabilityRegistry_SB02_INV_004_registers_every_known_catalog_tool` prevents this by comparing the registry against `ToolContractCatalog.KnownToolNames`.

A shallow fix that only denies unknown names would still leave command execution, local launch, and browser interactions without explicit governed operation metadata. `ToolCapabilityRegistry_SB02_INV_005_declares_static_operation_requirements_for_high_risk_tools` and `EvaluateAsync_SB02_INV_003_denies_command_run_without_execute_external_action_operation` prevent that.

A shallow registry without target-scope/proof metadata would still force downstream policy/UI code to infer capabilities from strings. `ToolCapabilityRegistry_SB02_INV_006_declares_side_effect_target_scope_and_proof_metadata` prevents that.

## Anti-Stub And Artifact Evidence

- Anti-stub audit: `bundle://proof/SB02/anti-stub-audit.txt`
- Changed file hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Browser validation block note: `bundle://proof/SB02/browser/browser-validation-blocked.md`
