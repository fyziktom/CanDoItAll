# SB02 Proof Manifest

## Scope

Subbundle: `SB02 Canonical tool capability registry and policy decomposition`.

This pass replaces split policy metadata with `ToolCapabilityRegistry`, removes the unsafe unknown-tool fallback to read-only behavior, adds target-scope/side-effect/proof metadata, and introduces policy component boundaries while preserving the existing evaluation order.

## Source Changes

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
  - Adds the canonical capability registry for catalog tool names, process operation requirements, target-scope metadata, side-effect descriptors, approval defaults, browser proof role, and idempotency descriptor.
  - Unknown tool names now classify as `Unknown` except explicitly modeled provider-native, hosted MCP, and local MCP tool families.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
  - Delegates public metadata to `ToolCapabilityRegistry`.
  - Routes operation requirement resolution through `ToolOperationRequirementResolver`.
  - Separates browser proof bounds, external target boundaries, script side-effect boundaries, stale proof/source checks, repeat invocation guarding, and dotnet-new template consistency into cohesive policy components.
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
  - Adds SB02 registry completeness, unknown-tool denial, command-run operation, high-risk metadata, provider/MCP, target-scope, browser-proof, and capability-flag assertions.

Changed file hashes:

- `bundle://proof/SB02/changed-file-hashes.txt`

Source assertions:

- `bundle://proof/SB02/source-assertions.txt`

## Failing-First Proof

- `bundle://proof/SB02/transcripts/failing-first-tool-capability-registry.txt`
  - Replayed the old split metadata behavior.
  - Result: targeted tests failed because unknown tools fell back to `Read`, `workspace_command_run` was not explicitly classified as a mutation, and governed command execution was not denied without `ExecuteExternalAction`.

## Passing Proof

- `bundle://proof/SB02/transcripts/passing-tool-capability-registry.txt`
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SB02_INV" --no-restore`
  - Result: 16/16 passed.
- `bundle://proof/SB02/transcripts/passing-agent-tool-policy-tests.txt`
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentToolInvocationPolicyTests" --no-restore --no-build`
  - Result: 142/142 passed.
- `bundle://proof/SB02/transcripts/passing-agent-finalizer-policy-tests.txt`
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentFinalizerPolicyTests" --no-restore --no-build`
  - Result: 18/18 passed.
- `bundle://proof/SB02/transcripts/passing-capability-and-drift-tests.txt`
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentFrameworkExecutionCapabilityFilteringIntegrationTests|FullyQualifiedName~ProcessContractDriftScannerTests" --no-restore --no-build`
  - Result: 6/6 passed.

## Browser Validation

- `bundle://proof/SB02/browser/browser-validation-blocked.md`
  - Browser validation was attempted through a generated proof route.
  - The Browser plugin blocked both `data:` and `file:` proof URLs under its URL policy and explicitly instructed not to work around the block.
  - The browser-tool policy behavior is therefore covered by registry metadata tests and full `AgentToolInvocationPolicyTests`; full app/browser E2E remains assigned to SB04 and SB08.

## Anti-Stub Audit

- `bundle://proof/SB02/anti-stub-audit.txt`
  - Scanned changed production policy files for `TODO`, `NotImplemented`, `throw new NotImplementedException`, `fixture`, `fake`, and `stub`.
  - Result: pass.

## Production Behavior Artifact Matrix

| Behavior artifact | Producer | Consumer | Lifecycle | Negative proof | Positive proof |
| --- | --- | --- | --- | --- | --- |
| Canonical capability metadata for every known catalog tool. | `ToolCapabilityRegistry` | `AgentToolInvocationPolicyMetadata`, operation resolver, policy tests | Static registry; changes fail completeness test. | `failing-first-tool-capability-registry.txt` | `passing-tool-capability-registry.txt` |
| Unknown tools deny instead of silently becoming read-only. | `ToolCapabilityRegistry.Classify` | `DefaultAgentToolInvocationPolicy` and composed capability filtering | Per invocation classification. | `failing-first-tool-capability-registry.txt` | `passing-agent-tool-policy-tests.txt` |
| High-risk command/local launch/browser tools carry explicit operation and proof metadata. | `ToolCapabilityRegistry` | Governed process operation authorizer and UI/API metadata consumers | Static registry metadata. | `failing-first-tool-capability-registry.txt` | `passing-tool-capability-registry.txt` |
| Policy branches are routed through cohesive components. | `DefaultAgentToolInvocationPolicy` component fields | Tool invocation evaluation | Per invocation; evaluation order preserved. | Source assertions | `passing-agent-tool-policy-tests.txt` |

## Raw Note Closure

SB02 closes the raw-note slice for skipped/omitted work where split tool metadata and default-read fallback could hide unregistered side-effecting tools. Remaining raw-note slices for cost reconciliation, real process E2E proof, proof quality, active skill-root sync, UI proof, and final QA remain assigned to SB03-SB09.

## Downstream Impact

SB04 real E2E and SB08 UI proof must use tool ids present in `ToolCapabilityRegistry` and must expect unknown tool names to be denied. Downstream process/workflow proof can rely on explicit metadata for command execution, browser proof tools, local MCP launch, provider-native families, hosted MCP families, and project/process tool ids.
