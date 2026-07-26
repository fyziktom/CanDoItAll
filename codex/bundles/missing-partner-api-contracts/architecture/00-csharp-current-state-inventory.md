# C# Current-State Inventory

## Evidence

- CodeAnalytics snapshot `snap-20260725222007-d4d57050`.
- 9 scoped projects, 440 documents, no blocking load errors.
- Relevant large files: `AgentsApi.cs` (675 lines), `WorkflowsApi.cs` (806 lines),
  `CrmHrApiContracts.cs` (618 lines).
- No scoped project-reference cycles; two existing module/type cycles in
  `Modules.AgentFramework` must not be expanded.

## Current Owners

| Responsibility | Current owner | Evidence/risk |
| --- | --- | --- |
| Agent HTTP routes and request DTOs | `CanDoItAll.Web/Api/AgentsApi.cs` | file owns catalog, teams, providers, capabilities, chat, execution |
| Agent catalog mutation/import | `AgentFrameworkWorkspaceCatalogService` partial group | `Agents.cs` is 705 lines; package import remaps provider/capability ids |
| ZIP package serialization | `ZipAgentPackageService` | existing archive boundary to harden/reuse |
| Structured runtime output | `AgentStructuredOutputContract` and execution service | `.NET Type` is valid internally but invalid public transport |
| Workflow HTTP routes | `CanDoItAll.Web/Api/WorkflowsApi.cs` | file owns authoring, run control, analytics |
| Workflow idempotency | launch models/service/store | durable primitive exists; HTTP bridge always chooses `NotRequested` |
| Workflow template provenance | catalog definitions/materialization | stable data may exist internally but is absent from public lookup |
| CRM-HR interview rows | `RecruitmentInterview` and `RecruitingService` | application-centric prose, no typed agent/run evidence |

## Construction And Tests

- Web maps grouped Minimal API endpoint classes from `Program.cs`.
- Module composition registers agent/workflow persistence and application services.
- Integration test host can enable OpenAPI and authorization configuration.
- Existing targeted suites: `WorkflowApiIntegrationTests`, `CrmHrApiIntegrationTests`,
  and agent API/workspace integration tests.

## Missing Test Seams

- Archive validation and remote import orchestration must be instantiable without Web host.
- External-key and idempotency claim/fingerprint logic must be directly unit testable.
- JSON Schema validation must be testable without billable provider execution.
- Readiness derivation must be testable without constructing CRM-HR UI/application state.

## Partial-Class Policy

- Existing partial catalog/execution services are legacy owners. No new partial file may be
  the final boundary for these responsibilities.
- New behavior goes in top-level services/validators; existing partials may delegate only
  as a temporary compatibility facade with shrink/delegation proof.
