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

## Governed Process Capability Matrix

Process roles must be staffed with the tools and skills they are expected to use. Missing capability checks should use `AgentCapabilityRequirementEvaluator` so callers receive typed `AgentCapabilityDiagnostic` values instead of prose-only capability gaps.

| Role | Required capabilities | Process access | Runtime rule |
| --- | --- | --- | --- |
| Process author | `candoitall-api-processes`; `processes_definition_editor_get`; `processes_definition_save`; `processes_definition_publish`; `processes_templates_list`; `processes_template_get`; `processes_template_import` when importing templates | Read/write for allowed definitions | Definition mutation must use process tools and approval policy, not direct database or file edits. |
| Process manager | `candoitall-api-processes`; `processes_runs_list`; `processes_run_detail_get`; `processes_analytics_get`; `processes_step_transition`; `processes_assignment_resolve`; `processes_artifact_record` | Read/write for managed definitions | Managers inspect run detail before transitions and record assignment/artifact evidence through governed tools. |
| Step executor | Workspace read/write tools required by the work brief; validation tools named by the step; `processes_artifact_record` only when the executor records its own process artifact | Usually read-only process access unless the step explicitly records artifacts | Executors do not invent process state transitions when the tool is unavailable. |
| Reviewer or QA | Workspace read tools; validation/browser tools named by the step; `processes_run_detail_get`; `processes_artifact_record` when review evidence is required | Read access; write access only for review artifacts or transitions assigned to the role | Review decisions must cite current-run evidence and required receipts. |
| Template curator | `processes_templates_list`; `processes_template_get`; `processes_template_mermaid_get`; `processes_template_baseline_scenarios_list`; `processes_template_live_run_profiles_list`; `processes_template_import` | Read/write only when importing or publishing templates | Template inspection is read-only unless import or publish is explicitly requested. |

Anti-improvisation is enforced in two layers. `DefaultAgentToolInvocationPolicy` denies tools that are not in the composed capability set and denies known tools with no registered classification. `AgentCapabilityRequirementEvaluator` catches missing, stale, or retired role capabilities before dispatch so callers can block or restaff explicitly.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
