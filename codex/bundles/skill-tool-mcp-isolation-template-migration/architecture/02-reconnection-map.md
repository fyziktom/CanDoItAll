# Reconnection Map

| Existing surface | Current responsibility | New owner | Reconnection subbundle |
| --- | --- | --- | --- |
| `MafAgentRuntime.Capabilities.cs` | Composition orchestration and nested config models | MAF adapter consuming typed capability services | SB08, SB09 |
| `MafAgentRuntime.Capabilities.Tools.cs` | Built-in tool switch, workspace tool creation, approval wrapper | Tools implementation + MAF tool adapter | SB02, SB05, SB08, SB09 |
| `MafAgentRuntime.Capabilities.Skills.cs` | File/inline/registered skill loading | Skills implementation + MAF skill adapter | SB03, SB05, SB08, SB09 |
| `MafAgentRuntime.Capabilities.Mcp.cs` | Hosted/local/http MCP creation, allowed tools, secret binding, command policy | MCP runtime + MAF MCP adapter | SB04, SB05, SB08, SB09 |
| `AgentCapabilityRequirementEvaluator` | Required capability checks and stale/retired diagnostics | Shared capability access evaluator with required and denied-required diagnostics | SB01, SB05, SB08, SB11 |
| `ProcessToolOperationAuthorizer` | Tool-only operation gating through string operation names | Typed operation selectors feeding shared access policy evaluation | SB01, SB06, SB08, SB09 |
| `AgentRuntimeContextIntent.AllowedOperations` and runtime flags | Process/runtime capability hints and coarse booleans | Typed access policy snapshot or effective access context | SB01, SB06, SB08 |
| `SandboxWorkspaceSeedBuilder` | Hardcoded default capabilities and stable IDs | Template loader + seed materializer | SB06, SB07 |
| `SandboxWorkspaceSeedAssets` | Embedded skill roots and inline text | `Templates/Capabilities` and optional checked-in skill resources | SB06, SB07 |
| `CapabilityProofService` | Hardcoded proof rules by capability kind | Capability setup/test services with typed results | SB04, SB10 |
| `ToolContractCatalog` and `ToolCapabilityRegistry` | Static tool names, side effects, operation requirements | Template-backed generated constants plus policy registry | SB01, SB02, SB12 |
| `CapabilitySetupWizardDialog` | Skill/MCP-only setup wizard | Capability setup UI for Skill, Tool, MCP, and access policy editing/preview | SB10 |
| `AgentsApi` capability endpoints | Generic capability CRUD and verify | Add setup-test/list-tools/external-tool-test and access-policy preview endpoints | SB10 |
| Existing unit/integration/component/e2e tests | Regression guardrails around seed and runtime | Expanded tests against isolated loaders/invokers/adapters | All, especially SB11 |

## Reconnection Rule

Every old hardcoded path must be either deleted after proof or explicitly marked as a compatibility adapter. It must not remain as a silent fallback when a template fails to load.

Every external tool or MCP reconnection path must preserve the structured diagnostic category and repair details described in `architecture/03-error-and-diagnostics-model.md`.

Every capability restriction path must go through the shared access evaluator described in `architecture/05-capability-access-policy.md`. MAF, process execution, workflow execution, and UI preview must not each implement separate skill/tool/MCP suppression rules.
