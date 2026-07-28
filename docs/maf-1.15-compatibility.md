# MAF 1.15 Compatibility

CanDoItAll targets the Microsoft Agent Framework 1.15 release train.

## Package Source Of Truth

Versions are centralized in `src/MAF/MicrosoftAgentFramework.Packages.props`:

| Property | Current value | Use |
|---|---:|---|
| `MicrosoftAgentsAIStableVersion` | `1.15.0` | stable MAF packages |
| `MicrosoftAgentsAIPreviewVersion` | `1.15.0-preview.260722.1` | preview-only A2A and hosting packages |

Projects must consume those properties instead of introducing independent MAF version literals. The central props file and each consuming `.csproj` are authoritative; this page records compatibility policy rather than duplicating the entire package graph.

## Adopted Runtime Contract

The current adapters target the 1.15 behavior in these areas:

- chat-agent options are composed through the current runtime factory path
- tool approvals use stable tool identities and policy-bound approval wrappers
- session state is serialized through the MAF 1.15 session contract
- direct and streaming workflow handoffs share bounded routing and depth rules
- required finalizer tools capture machine-readable completion independently of assistant prose
- tool, finalizer, workflow, and runtime-provider traces preserve correlation metadata
- A2A outbound endpoints require explicit valid configuration and credentials

Inbound A2A workflow-event mapping is intentionally not advertised as supported. Preview package presence does not make every preview protocol surface a product contract.

## Upgrade And Persisted State

Approval continuation state created against the former 1.13 adapter contract is not reconstructed or silently translated. Drain compatible in-flight work before deployment, or cancel and reissue it under the 1.15 runtime. Fabricating tool identities or approval payloads would bypass the policy binding that the continuation is meant to preserve.

Apply the same rule to any persisted MAF state that fails current deserialization: surface an explicit incompatibility, retain diagnostics without secrets, and require an operator decision. Do not fall back to a fresh session while presenting the old execution as resumed.

## Validation

Use the repository's current gates rather than the counts captured during the upgrade:

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test .\CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

The focused MAF proof areas are documented in the local README for `CanDoItAll.AgentFramework.Maf`: tool loop, context providers, finalizers, errors, approvals, MCP result bounding, A2A configuration, workflow mapping, and trace correlation.

The [MAF 1.15 upgrade execution report](../codex/bundles/maf-1-15-upgrade-architecture/reviews/01-execution-report.md) is retained as historical evidence of the branch migration. Its dates, test counts, warnings, and rollout observations are not a statement about the current branch.

## Release Guidance

- Restore must resolve one coherent stable/preview release train.
- Release validation must include the current stable gate and any affected live provider, workflow, or A2A slice.
- Persisted approval/session incompatibilities must be visible to operators.
- A package restore and compile do not establish production rollout readiness on their own.
