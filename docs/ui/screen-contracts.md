# Screen Contracts

This is the second reconstruction pass: a behavioral contract for every current routed
product surface, grouped where routes share one purpose. It intentionally describes
user intent, object context, actions, state, and handoffs—not present layout or visual
components.

The route inventory comes from the maintained
[`UI/UX Refactoring` page list](../../../CanDoItAll.Components/docs/ui-refactoring/app/README.md).
That inventory is **Corroborated** by the routed Razor pages and component tests. A
route appearing here does not by itself prove that a surface should remain a top-level
destination in a future IA.

The test references in individual contracts are discovery pointers. The assertion-level
audit is maintained separately in [test scenario evidence](resources/test-scenario-evidence.md);
do not interpret an unreviewed test filename as independent proof of a UX claim.

## Application shell

### Workspace shell

**Routes:** all routed application screens.

**User goal:** Know the active operating context and move safely between work.

**Can see/do:** navigate product areas; manage open workbench tabs and recent tabs; open
floating conversations; open settings/runtime capability checks; inspect and switch the
active database profile.

**Safety/lifecycle:** a database-profile change is significant: routed content waits for
the active profile rather than silently showing a false empty state. The shell reports
workspace, route, project/phase context, and live/open item counts.

**Evidence:** `MainLayout.razor`, `MainLayout*.cs`, `AppShellTests`,
`MainLayoutDatabaseProfileTests`, and `MainLayoutTopBarTests`.

## Overview and collaboration

### Dashboard

**Routes:** `/`, `/dashboard`.

**User goal:** Orient quickly to current operational work.

**Can see:** quick links to Projects, Agents, Live Processes and Scheduler; recently
updated projects; active/recent workflow and process runs; observed agent usage/cost;
and the freshness of the five-minute snapshot.

**Can do:** refresh the snapshot and navigate to its source work item.

**Safety/lifecycle:** a refresh failure preserves the last successful snapshot and
labels it stale; no snapshot is presented as current after a failed load.

**Evidence:** `Home.razor`, `HomePageTests`, `DashboardSnapshot*Tests`.

### Collaboration

**Route:** `/collaboration`.

**User goal:** Review and triage durable notifications, human threads, and escalations
in their delivery or automation context.

**Can see/do:** switch among Inbox, Threads, and Escalations; filter unread items; create
a notification or escalation; inspect a selected thread’s participants and preserved
messages; reply; mark it read; and follow its context link when one exists.

**Safety/lifecycle:** unread state and escalations are explicit counts, and the module is
the canonical read model even when external systems project signals into it. Threads are
durable records with Open/Closed state; the current page exposes read/triage, not a
user-facing close/resolve action. Read status is not a substitute for resolving an
escalation.

**Evidence:** `CollaborationHomePage.razor`, `CollaborationContracts.cs`,
`CollaborationIntegrationTests`, and `MainLayoutCollaborationTests`.

## Delivery

### Project portfolio

**Route:** `/projects`.

**User goal:** Find, create, organise, and open delivery contexts.

**Can see/do:** browse portfolio cards/board; create/edit/delete projects; inspect and
change hierarchy; import/export portfolio records; open project files and enter the
project workbench.

**Safety/lifecycle:** project deletion can involve clean-up and completion notices; it is
not merely removal of a list row.

**Evidence:** `ProjectsPage.razor`, project API routes, project modal/hierarchy/files
dialogs, `ProjectsPageTests`.

### Project Structure workbench

**Route:** `/projects/{projectId}/structure`.

**User goal:** Model, inspect, and operate the work inside one project.

**Can see/do:** work with a graph/canvas plus object index, selection details, signals,
activity/summary, task creation/editing, assets/attachments and previews, links and
dependencies, status/progress/priority/markers, and project-level process/workflow
launches. Open an item’s linked artifact, process, workflow, Gantt, or file surface.

**Safety/lifecycle:** destructive node/attachment operations present an explicit choice
about managed files; script execution and runtime launch can require approval; the page
has validation and unavailable-after-database-switch states.

**Evidence:** `ProjectStructurePage.razor`, `ProjectStructureAgentApi.cs`, its dialog
inventory, and the `ProjectStructure*Tests` family.

### Project schedule views

**Routes:** `/projects/{projectId}/calendar`; Gantt is also available from Project
Structure.

**User goal:** Understand timing and delivery dependencies without editing a different
copy of the project plan.

**Can see/do:** view linked phase windows, validation activity, tests, milestones and
tasks; select an event/task and open its linked artifact or edit details in context.

**Safety/lifecycle:** view/selection state is restored and invalidated correctly when the
active database profile changes. The projection is read/edit-through project truth, not
a separate scheduling domain.

**Evidence:** `ProjectCalendarPage.razor`, Project Structure Gantt components, and
Calendar/Gantt tests.

### Process design and project-scoped processes

**Routes:** `/processes`, `/projects/{projectId}/processes`.

**User goal:** Define a reusable process and use it in the relevant project context.

**Can see/do:** browse/create/edit process definitions, roles and steps; compose branch
and artifact expectations on a definition canvas; choose templates; launch a process;
inspect run detail/events/agent execution logs.

**Safety/lifecycle:** definition authoring and process-run operation are intentionally
separate. Project scope filters the delivery context but does not change the process
identity into a project node.

**Evidence:** `ProcessesPage.razor`, `ProjectProcessesPage.razor`, Process definition
components/dialogs, `ProcessesApi.cs`, `ProcessWorkspaceShellTests`.

### Live Processes

**Routes:** `/processes/live`, `/projects/{projectId}/processes/live`.

**User goal:** Observe, diagnose, and recover execution already in motion.

**Can see/do:** filter live/historical process-run activity, inspect run/agent details,
events and scoped files, and use permitted dispatch/cancel/rework/recovery actions.

**Safety/lifecycle:** a run’s state, event history, files, and recovery path survive the
screen; controls must reflect whether a transition is currently legal.

**Evidence:** `LiveProcessesPage.razor`, `LiveProcessesDashboard.razor`,
`ProcessRunFilesDialog.razor`, process run APIs and tests.

## AI and automation

### Agents and conversations

**Route:** `/agents`; `/chats` is a compatibility redirect.

**User goal:** Configure, govern, and use AI workers or hold an ordinary LLM
conversation.

**Can see/do:** manage agent catalogue/details, teams, provider profiles, capabilities,
memory, governance, diagnostics, usage and active executions; create/continue agent chat
sessions and floating conversations; use the Simple Chats definition and conversation
workspace.

**Safety/lifecycle:** cloning/import/export/template conversion and deletion are distinct
agent actions. Agent execution approvals, artifacts, checkpoints and logs are distinct
from Simple Chat turns. Tool/capability use and project-structure access are explicit,
scoped grants; governed process tool calls fail closed when their process restrictions
cannot be established. Simple Chat definitions and conversations carry their own
revision/archive/recovery lifecycle.

**Evidence:** `AgentsHomePage.razor`, Agent dialog inventory, `AgentsApi.cs`,
`llm-chats-api.md`, and agent/conversation component tests.

### Workflow studio

**Route:** `/agents/workflows`.

**User goal:** Build, validate, publish, and operate reusable automation workflows.

**Can see/do:** manage workflow definitions/versions/components, import/export and use
templates, edit a workflow canvas, configure/test input, start test or ordinary runs,
and inspect run detail, events, artifacts, checkpoints, analytics and pending requests.

**Safety/lifecycle:** publish, suspend, archive, delete and validation are separate
operations. External/pending requests pause the run for a deliberate response rather
than being silently completed.

**Evidence:** `WorkflowsPage.razor`, `WorkflowCanvasEditor.razor`, `WorkflowsApi.cs`,
and workflow page/editor/analytics tests.

### Scheduler

**Route:** `/scheduler`.

**User goal:** Arrange future process/workflow launches and understand what did or did
not dispatch.

**Can see/do:** view scheduled and dispatched runs on a calendar; list/filter/create/edit
schedules; select a process/workflow target; pause/resume/delete a plan; inspect history
and failures; open the scheduler agent context.

**Safety/lifecycle:** schedule state, next fire time, time zone, last error, approval
wait, failure, and no-message outcomes are part of the contract—not background detail.

**Evidence:** `SchedulerPlannerPage.razor`, Scheduler Planner tests and models.

## Organisation and workforce

### CRM/HR home and directory

**Routes:** `/crm-hr`, `/crm-hr/directory`.

**User goal:** Discover and maintain the organisation’s identities and relationships.

**Can see/do:** move into CRM/workforce/recruiting/assignments/agent areas; browse/search
Party records; create/edit parties; manage affiliations, addresses, contact methods,
relationships, and import/export; merge duplicates.

**Safety/lifecycle:** list views are privacy-safe summaries, while detail/editor views
may contain sensitive data. Relationship replacement is authoritative for the selected
party and must not be represented as a casual incremental toggle.

**Evidence:** CRM/HR Home and Directory pages, CRM/HR API/privacy docs/tests, Party
dialogs.

### CRM pipeline

**Route:** `/crm-hr/crm`.

**User goal:** Manage commercial opportunities and deliberately hand successful work into
delivery.

**Can see/do:** view accounts/opportunity pipeline and financial context; create, inspect
and edit opportunities; choose a project target and convert a won opportunity.

**Safety/lifecycle:** conversion is an explicit handoff, not an automatic side effect of
editing status.

**Evidence:** `CrmHrCrmPage.razor`, Opportunity dialogs/board/pipeline, CRM/HR routes.

### Workforce and assignments

**Routes:** `/crm-hr/workforce`, `/crm-hr/assignments`.

**User goal:** Understand available delivery capacity and allocate it to project work.

**Can see/do:** manage workforce records, delivery units, roles, skills and capacity;
browse/select parties and projects; create staffing requests; make allocations; inspect
assignment details and Gantt/capacity context.

**Safety/lifecycle:** selection and allocation must preserve the distinction between a
party identity, workforce evidence, and a project-specific assignment.

**Evidence:** workforce/assignments pages, picker/allocation/staffing dialogs, CRM/HR
API and component tests.

### Recruiting and CRM Agents

**Routes:** `/crm-hr/recruiting`, `/crm-hr/agents`.

**User goal:** Assess candidates—including AI-agent candidates—before they become
workforce participants.

**Can see/do:** create/review applications, interviews, lifecycle/support assignments,
and evidence; convert a candidate to workforce; create/edit the CRM-facing AI-agent
record and attach/classify assessment evidence.

**Safety/lifecycle:** recruitment evidence and workforce conversion form a staged
transition; they should not be visually or semantically collapsed into generic agent
configuration.

**Evidence:** recruiting/CRM-agent pages and dialogs; CRM/HR recruiting routes and tests.

## Shared operational assets

### Prompt Gallery and Resources

**Routes:** `/prompt-gallery`, `/resources`.

**User goal:** Curate reusable inputs and durable materials for product work.

**Can see/do:** search/filter/edit/version/archive/favorite prompt items, evaluate
compatibility and send a prompt into a chat composer; browse resource records/files and
promote a storage object to a resource.

**Safety/lifecycle:** prompt status/version/archive and storage-to-resource promotion are
explicit state changes, not simple text editing.

**Evidence:** Prompt/Resource pages, dialog inventory, API routes and component tests.

### Plugins, Memory, and Settings

**Routes:** `/plugins`, `/memory`, `/settings`, `/settings/runtime-capabilities`.

**User goal:** Make the local operating environment capable, secure, and diagnosable.

**Can see/do:** install/enable/configure plugins, grants/OAuth/connections/logs; configure
Memory providers and inspect operations/events/query/ingestion/feedback; configure data
sources, storage, secrets, providers, pricing, file associations and API access; inspect
runtime capability/readiness facts.

**Safety/lifecycle:** credentials and raw secret values are protected configuration;
plugin grants and external connections are explicit authority boundaries; configuration
test/health feedback must remain visible.

**Evidence:** corresponding pages/tabs/dialogs, API routes, settings/memory/plugin tests.

### Test Lab

**Route:** `/test-lab`.

**User goal:** Maintain one accountable assurance record for a delivery scope.

**Can see/do:** find plans by project, phase, or result; select/create a plan; set its
project, phase, responsible party, coverage goal and Playwright specification; add/edit
test cases, evidence references and run results; and retain the latest result alongside
the plan summary.

**Safety/lifecycle:** a plan is a durable assurance aggregate, not an ephemeral test
screen. Case status distinguishes Planned, Implemented, Passed, Failed and Blocked;
evidence is an explicit labelled artifact reference, not an implied attachment.

**Evidence:** `TestLabModels.cs`, `TestLabPage.razor`, and the CRM/HR-to-Test-Lab
Playwright flows.
