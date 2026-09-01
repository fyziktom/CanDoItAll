# Reconstruction Evidence Index

This index is the audit trail for the product/domain-and-UX reconstruction. Source code,
OpenAPI, and schema remain authoritative; this folder records how their user-facing
meaning was interpreted.

## Confidence rule

| Label | Minimum evidence |
|---|---|
| Confirmed | Public route/contract, persisted domain model, or maintained product documentation directly states the behaviour. |
| Corroborated | Confirmed evidence plus a page/UI string or test demonstrates how the product exposes it. |
| Inference | A synthesis that helps design discussion but is not directly stated. |

## Primary sources

| Area | Source of truth | Supporting UI/test evidence |
|---|---|---|
| Product boundary | [repository README](../../../README.md), [module map](../../architecture/modules.md) | shell, dashboard and navigation tests |
| Projects/workbench | `src/App/CanDoItAll.Web/Api/ProjectsApi.cs`, `ProjectStructureAgentApi.cs`, `src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`, `src/Modules/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs` | Projects, Project Structure, Gantt, Calendar and task tests |
| Processes | `ProcessesApi.cs`, `ProcessRunRecordsApi.cs`, `src/Processes/**` contracts/projections | Process definition, workspace shell, live dashboard and run-files components/tests |
| Agents | `AgentsApi.cs`, agent event/provisioning APIs, AgentFramework module services | Agents home/catalog/details/governance/chat components/tests |
| Simple Chats | [LLM Chats product and API](../../llm-chats-api.md), LLM Chat endpoint/contract files | LLM Chat definition, conversation workspace and shell tests |
| Workflows | `WorkflowsApi.cs`, workflow run/event APIs | Workflows page, canvas/editor, overview and analytics tests |
| CRM/HR | [CRM/HR API](../../crm-hr-api.md), `CrmHrApi.cs`, CRM/HR models/services | directory, CRM, workforce, recruiting, assignments and privacy tests |
| Workspace | Workspace models/services and settings page | database/storage/provider/secret settings tests |
| Prompts/resources | `PromptGalleryApi.cs`, Prompt/Resource models | gallery, picker, composer and resource tests |
| Plugins/memory/scheduler | respective API and module files | Plugins, Memory Provider and Scheduler Planner page tests |
| Current page/dialog inventory | [`CanDoItAll.Components` UI/UX refactoring inventory](../../../../CanDoItAll.Components/docs/ui-refactoring/app/README.md) | route-level screen contracts and dialog ownership |
| Visual snapshot | sibling `CanDoItAll.Screenshots/current/desktop_light_default` | shallow empty-data frames only; use for current shell/page composition, not domain semantics |
| Assertion-level UI tests | [Test scenario evidence](test-scenario-evidence.md) | reviewed test bodies, their confirmed UX behavior, and the remaining audit queue |
| Product-owner intent | [walkthrough validation](product-owner-walkthrough-2026-08-23.validation.md), [raw timestamped transcript](product-owner-walkthrough-2026-08-23.transcript.md) | a 37-minute Czech end-to-end demonstration; automatic transcript is supporting evidence, with uncertain wording explicitly marked |

Paths in this table are repository-relative. The source is intentionally named rather
than copied into this documentation so the reconstruction cannot diverge from code.

## Evidence collection workflow

1. Start with bounded module README and public route family.
2. Read contracts/models to identify lifecycle, ownership, and allowed operations.
3. Read the page and short UI labels to identify the user-facing vocabulary.
4. Read tests to establish preconditions, transitions, empty/error/safety behavior, and
   cross-area handoffs.
5. Record a confirmed/corroborated claim or explicitly record an open question; do not
   fill a gap using generated product copy.

## Known gaps for follow-up collection

- Extract a screen contract for every routed page and major in-page tab.
- Produce a machine-generated route/DTO inventory from OpenAPI, then link it here.
- Inspect database entities/migrations for cross-area retention and deletion semantics.
- Generic Error/Not Found routes are intentionally outside this product/domain-and-UX
  reconstruction. Treat them as runtime/application recovery concerns.
- Obtain stakeholder review for user roles, audience, success metrics, and terminology
  that cannot be proven from code.
