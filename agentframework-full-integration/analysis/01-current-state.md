# 01 — Current State

## Repositories And Composition Roots

### CanDoItAll

- Solution root: `/mnt/data/work/cando/CanDoItAll-development/CanDoItAll.slnx`
- Web composition root: `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Program.cs`
- Module registry: `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Composition/ModuleAssemblies.cs`
- Shell navigation: `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Web/Composition/ShellNavigation.cs`

CanDoItAll dnes registruje moduly `Security`, `Workspace`, `Projects`, `Workbench`, `Resources`, `Prompts`, `Factory`, `Processes`, `Validation`, `TestLab`, `Activity`, `Automation` a `CrmHr`. Samostatný agent module ani collaboration module zatím neexistují.

### AgentFramework

- Solution root: `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/CanDoItAll.AgentFramework.sln`
- Main hosting registration: `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- Scenario harness: `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Sandbox/Hosting/ScenarioHarnessSupport.cs`
- Sandbox pages: `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Sandbox/Components/Pages`

AgentFramework je dnes navržený jako separátní sandbox host se svými providers, agents, chatem, capabilities, memory a scenario harness UI.

## Relevant CanDoItAll Surfaces Already In Place

| Concern | Existing surface | Evidence | Practical implication |
| --- | --- | --- | --- |
| Provider persistence | `Workspace.ProviderProfile` + `SecretService` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`, `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Security/SecurityModels.cs` | Master data a secret ownership už existují a nemají se duplikovat. |
| Provider runtime | `ProviderRegistry`, `OpenAiProviderAdapter`, `OllamaProviderAdapter` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs` | Tato vrstva se překrývá s AgentFramework runtime a musí být retirená nebo shimnutá. |
| Durable messaging transport | Automation envelopes, dispatcher, retries, dead-letter | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs` | Skvělý backplane pro outbox/inbox transport, ale ne pro user-facing conversation store. |
| Activity / audit projection | `IActivityStream` + Activity module | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.SharedKernel/ActivityStream.cs`, `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Activity/ActivityModels.cs` | Lze použít pro projection a audit, ale ne jako canonical notification center. |
| Resource pool | Parties, project assignments, AI agent profiles, staffing requests | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`, `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` | CRM-HR už nese resource foundations, takže nesmí vzniknout druhý resource registry v AgentFrameworku. |
| Process role model | Roles, steps, responsibilities, runtime assignments | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs`, `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs` | Process module už zná role a run assignments, ale neumí launch planning ani messaging policy. |
| Durable process boundary | `ProcessOutboxService` + worker | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessOutbox.cs` | Ideální boundary pro asynchronní spuštění agent runs a artifact propagation. |
| Existing executor seam | `IProcessExecutorRegistryBridge` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs` | Už existuje místo, kam se dá napojit agent/resource katalog, ale zatím je to no-op. |

## CRM-HR Findings

### What already exists

- `AiAgentProfile` business entity s poli `PartyId`, `ProviderProfileId`, `DefaultModel`, `ExecutionMode`, `OwnerPartyId`, `CapabilityJson`, `ValidationStatus`, `Notes`.
- `ProjectPartyAssignment` jako mixed resource binding pro projekty (`AiAgent`, `Manager`, `TeamMember`, ...).
- `StaffingRequest` jako obecná demand entity pro projektové staffing.
- UI route `/crm-hr/agents` s quick-create AI agent party flow a profile editingem.

### Why it matters

CRM-HR dnes **už** funguje jako resource/business registry. Technická agent definice proto nesmí být jen znovu namodelovaná uvnitř CRM-HR. Je potřeba explicitní resource-to-agent binding.

## Processes Findings

### What already exists

- `ProcessRoleRequirement` obsahuje `StaffingIntent`, `PreferredExecutorKind`, `PreferredProjectAssignmentRole`, `RequiresExplicitApproval`.
- `ProcessRunAssignment` ukládá roli, party, executor kind a binding reason.
- `ProcessDecisionRecord`, `ProcessWorkBrief` a `ProcessArtifactRecord` už existují.
- Canvas dnes umí structural, decision, role-participation a artifact linky.

### Current limitation

`ProcessesService.StartRunAsync(...)` dnes:
- načte published definition,
- vytvoří `ProcessRun` rovnou ve stavu `Active`,
- pokusí se předvyplnit resources z `IProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(...)`,
- uloží assignments a work briefs,
- rozjede outbox.

To je výrazně méně než požadovaný flow `request resources -> HR recommendation -> manager/human approval -> provisioning -> actual run start`.

## AgentFramework Findings

### What already exists

- `ProviderProfile` model s provider-specific runtime properties.
- `AgentDefinition` s permissions jako `CanAskOtherAgents`, `CanEscalateToHuman`, `RequiresApprovalForExternalCalls`.
- `ChatSessionRecord`, `ExecutionRunRecord`, `ExecutionArtifactRecord`, `ExecutionApprovalRecord`, `ExecutionWorkflowCheckpointRecord`.
- `AddAgentFrameworkCore(...)` a `AddAgentFrameworkIntegrated(...)`, ale integrated varianta je zatím jen alias na sandbox-style core registration.
- Scenario harness `SC01–SC08`.

### Current limitation

- Persistence je převážně file/sandbox oriented (`FileSandboxWorkspaceStore`).
- Workspace root je jeden globální adresář, ne project/process scoped context.
- UI a hosting jsou navázané na sandbox shell.
- Provider credential ownership počítá s environment variables místo main-app secret bridge.

## Duplicate Or Conflicting Concepts Detected

| Concern | CanDoItAll side | AgentFramework side | Risk |
| --- | --- | --- | --- |
| Provider profile shape | `Workspace.ProviderProfile` | `AgentFramework.Models.ProviderProfile` | Split master data a split runtime metadata. |
| Provider execution runtime | `Workspace.ProviderExecution.cs` | AgentFramework runtime + provider registry | Dva canonical execution paths. |
| Business AI agent identity | `CRM-HR.AiAgentProfile` | `AgentDefinition` | Hrozí dvojí editable registry pro totéž. |
| Human-visible messaging | žádný canonical conversation store | chat session models hlavně pro agent chat | Chybí procesně auditovatelná collaboration vrstva. |
| Runtime approvals | process decisions + workflow approvals | pending tool approvals / execution approvals | Bez bridge vznikne dvojí approval lifecycle. |
| Artifact evidence | `ProcessArtifactRecord.ManagedStoragePath` | `ExecutionArtifactRecord.RelativePath` | Bez bridge se ztratí canonical evidence owner. |

## Shared Helpers And Platform Services Worth Reusing

- `AppDbContext` a stávající EF module patterns pro nové DB entity a migrace.
- `IClock` pro timestamps místo `DateTimeOffset.UtcNow`.
- `IActivityStream` pro audit/projection events.
- `SecretService` a `ISecretProtector` pro provider credentials a případné system tokens.
- `IAutomationMessagePublisher` / `IAutomationMessageDispatcher` pro durable transport a retries.
- `ProcessOutboxService` pro orchestration boundary mezi process runtime a agent runtime.
- `IProjectPartyIntegrationBridge` pro cross-module resource lookups.
- `IProcessExecutorRegistryBridge` jako seam pro resource/agent registry.
- `IStorageCatalogService` a managed storage abstractions pro canonical artifact persistence.
- Existing component/page patterns v `CanDoItAll.Web` a module page scaffolds pro jednotný UI shell.

## Current-State Conclusion

Repo už obsahuje dost stavebních bloků na čistou integraci, ale neobsahuje hotové end-to-end řešení. Nejsilnější foundation je v `Processes`, `CRM-HR`, `Automation`, `Activity`, `Security` a shell composition. Nejslabší místa jsou:
- chybějící Collaboration modul,
- split provider ownership,
- split business vs technical agent model,
- sandbox-based workspace scoping,
- chybějící staged launch flow,
- chybějící process-governed messaging policy,
- a chybějící reálný integrated scenario validation plan.

Tahle bundle proto neřeší jen „přidat modul“, ale definuje controlled migration programu, který minimalizuje architektonický dluh.
