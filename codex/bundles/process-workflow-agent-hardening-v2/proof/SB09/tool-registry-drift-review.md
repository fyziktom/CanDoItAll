# SB09 Tool Registry Drift Review

## Decision

Pass.

## Evidence

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/transcripts/passing-tool-capability-registry.txt`
- `bundle://proof/SB09/transcripts/adversarial-contract-and-tool-policy.txt`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`

## Reviewed Behaviors

- Unknown tools classify as `Unknown`.
- Unknown read-like tool invocation is denied.
- Every known catalog tool is registered.
- High-risk command/local launch/browser tools carry operation, side-effect, idempotency, approval, and proof metadata.
- `workspace_command_run` requires `ExecuteExternalAction`.

## Residual Risk

Any future tool id added outside `ToolContractCatalog` or hosted MCP conventions must update `ToolCapabilityRegistry` and registry completeness tests.
