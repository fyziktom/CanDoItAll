# C# Testability Plan

## Unit Tests

| Area | Required tests |
| --- | --- |
| Generic image prompts | Empty prompt, custom prompt, multi-image prompt, deterministic evidence inclusion, and absence of software/UI terms. |
| Capability scope compiler | Deny by skill key, runtime tool name, MCP server, MCP tool, tag, operation classification, and runtime provider key. |
| Required capabilities | Missing required capability, denied required capability, satisfied required capability. |
| Allow-only semantics | Tests proving allow-only actually suppresses non-matching capabilities or fails if not implemented. |
| Runtime provider descriptors | Provider-generated tools contain stable provider identity metadata. |
| Process template parsing | Valid scope, invalid selector, invalid effect, unknown target kind, and scoped instruction validation. |
| Assignment persistence | Effective scope is saved, loaded, repaired, and projected. |
| Metadata handoff | Scope metadata serializes/deserializes and fails predictably on invalid JSON or invalid selector values. |

## Integration Tests

| Scenario | Required proof |
| --- | --- |
| Management-only step suppresses development skill | Skill is absent from attached context and listed as excluded in manifest for that step only. |
| Process-required tool is absent | Governed execution blocks with required-capability diagnostics. |
| MCP server denied | MCP tools from that server are absent and diagnostics include selector/rule details. |
| Runtime provider denied | Provider tools are not attached and diagnostics include provider key. |
| Development image analysis owner | UI screenshot analysis behavior is available only through development package/process scope, not common MAF. |

## Architecture Tests

- Project-reference scan for forbidden dependencies.
- Text scan for domain leak terms in common MAF.
- Context manifest assertions for included/excluded sources.
- Existing allowed-operation policy tests remain green.

## Validation Commands

Run during execution after relevant subbundles:

```powershell
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProcessLaunchPromptTests"
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~AgentFrameworkWorkspaceSeedIntegrationTests"
dotnet build CanDoItAll.slnx
```
