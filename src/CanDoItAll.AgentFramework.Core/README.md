# CanDoItAll.AgentFramework.Core

## Purpose

Provider-neutral AgentFramework application services, execution contracts, workspace orchestration, and runtime abstractions.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`

Framework references:

- None

Direct package references:

- `OpenTelemetry.Api (1.15.3)`

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. MAF-specific workflow adapters and checkpoint helpers belong in `CanDoItAll.AgentFramework.Maf`. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

## Runtime Tool Ownership Receipts

Tool receipts can include optional runtime-provider ownership through `RuntimeToolProviderKey` and `RuntimeToolProviderName`. These fields are populated when a provider-aware runtime sets `AgentRuntimeToolOwnershipContext` around a tool invocation; older receipts and tools that are not supplied by an `IAgentRuntimeToolProvider` keep both values empty.

Consumers must treat empty provider ownership as unknown, not as evidence that a receipt is invalid. Receipt validity still comes from the existing fields: tool family/name, risk class, approval mode, isolation guarantee, request summary, working directory, exit summary, and timestamps.

## Governed Process Capability Matrix

Process roles must be staffed with the tools and skills they are expected to use. Missing capability checks should use `AgentCapabilityRequirementEvaluator` so callers receive typed `AgentCapabilityDiagnostic` values instead of prose-only capability gaps.

The `candoitall-api-*` skills are Codex/operator skills for controlling the running app from outside the app process. Internal agents receive template-backed capabilities from `Templates/Capabilities`; they should use current workspace tools, MCP servers, provider-native tools, registered runtime-provider tools, and project-structure bridge tools composed by MAF.

| Role | Required capabilities | Process access | Runtime rule |
| --- | --- | --- | --- |
| Process author | Operator path: `candoitall-api-processes`; internal-agent path: project-structure process definition link/start tools when authoring through project structure | Read/write for allowed definitions only when a current API or UI path exists | Definition mutation must use a current process API/UI path, not direct database or file edits. Direct `processes_*` runtime tools are not current. |
| Process manager | Operator path: `candoitall-api-processes`; internal-agent path: current process context plus approved project-structure bridge or external-action tools | Read/write for managed runs through the API when operating externally | Managers inspect run detail/history before dispatch, cancellation, or rework. |
| Step executor | Workspace tools required by the work brief; validation tools named by the step; project-structure subprocess launch tool only when the process operation contract allows it | Usually read-only process access unless the step explicitly allows external action or subprocess launch | Executors do not invent process state transitions when the current API/tool path is unavailable. |
| Reviewer or QA | Workspace read tools; validation/browser tools named by the step; current process readback routes | Read access unless an explicit current mutation route is authorized | Review decisions must cite current-run evidence and required receipts. |
| Template curator | Process template authoring UI or future API/tool path when reintroduced | Read/write only when importing or publishing templates through a current surface | Template inspection is read-only unless import or publish is explicitly supported by current implementation. |

Anti-improvisation is enforced in two layers. `DefaultAgentToolInvocationPolicy` denies tools that are not in the composed capability set and denies known tools with no registered classification. `AgentCapabilityRequirementEvaluator` catches missing, stale, or retired role capabilities before dispatch so callers can block or restaff explicitly.

Current gap: policy constants and some tests still mention legacy or planned direct `processes_*` runtime tools. The current source tree does not contain a concrete process runtime tool provider. Reintroduce that provider deliberately or remove the stale capability names in a hardening pass.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Process/MAF/provider implementation map: `docs/processes-maf-providers-implementation-map.md`
