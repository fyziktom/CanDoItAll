# Navigation proposal

## Purpose

This is a proposed canonical URL model for the application. It responds to the current
state where major tabs, selected records, dialogs, and filters often have no durable URL.
It is a design proposal, not a record of implemented routes. For observed problems that
motivate it, see [navigation and structure](navigation-and-structure.md).

`✅` marks a route that already exists with that exact path. It does not mean its current
selection, tab, filter, or detail state is URL-addressable.

## URL rules

1. A stable, shareable object gets its own path with its durable ID. Examples: a project,
   process, run, agent, workflow, party, resource, schedule, or test plan.
2. A peer section gets a path segment, not an in-memory tab or a `tab` query parameter.
   `<SecondaryTabs>` is navigation between those paths.
3. Query parameters are for optional, reproducible view state: search, explicit filter,
   sort, pagination, and a selected subview that is not worth a route segment.
4. A deep link to an object must display that object even if a default filter would hide
   it. Default filters must not make a durable ID URL appear empty.
5. A dialog is for confirmation, picking, short edits, and preview. A substantial detail
   view must have a canonical page. A dialog may open from that page or offer an
   **Open full page** link.
6. URLs use lower-case, plural collection nouns and kebab-case only when a segment has
   several words. IDs are opaque; names are not put in canonical paths.
7. A project-scoped route is used when the project is the primary context. The same
   object still has one global canonical URL; project-scoped variants redirect to it or
   carry a return context rather than becoming competing permanent identities.

## Shell and top-level areas

| Area | Canonical routes | Navigation model |
|---|---|---|
| Dashboard | ✅ `/` | `/dashboard` redirects to `/`. |
| Projects | ✅ [`/projects`](http://localhost:5032/projects) | Portfolio/list. Selecting a project enters its project route family. |
| Processes | ✅ [`/processes`](http://localhost:5032/processes) | Definitions and operations; details use stable paths below. |
| Live processes | `/processes/runs` | A filtered run list, not a separate state hidden behind an unaddressable tab. |
| Agents | ✅ [`/agents`](http://localhost:5032/agents) | Agent overview/catalog; peer areas have named subroutes. |
| Workflows | `/workflows` | First-class top-level work area, rather than an implicit sub-area of Agents. |
| Collaboration | `/collaboration/inbox` | Inbox, threads, and escalations are explicit peer views. |
| CRM/HR | ✅ [`/crm-hr`](http://localhost:5032/crm-hr) | Module overview; directory, CRM, workforce, recruiting, agents and assignments are peer routes. |
| Resources | ✅ [`/resources`](http://localhost:5032/resources) | Registry root; browse is a peer route. |
| Prompt gallery | `/prompts` | List and prompt detail routes. |
| Scheduler | ✅ [`/scheduler`](http://localhost:5032/scheduler) | Calendar root; schedules and history are peer routes. |
| Test Lab | ✅ [`/test-lab`](http://localhost:5032/test-lab) | Plan list/root; a plan has a durable detail route. |
| Plugins | ✅ [`/plugins`](http://localhost:5032/plugins) | Catalog root; a plugin has a durable detail route. |
| Memory | ✅ [`/memory`](http://localhost:5032/memory) | Provider list/root; a provider has a durable detail route. |
| Settings | ✅ [`/settings`](http://localhost:5032/settings) | Configuration sections have their own paths and remain owned by Settings. |

The existing [`/prompt-gallery`](http://localhost:5032/prompt-gallery), [`/agents/workflows`](http://localhost:5032/agents/workflows), [`/processes/live`](http://localhost:5032/processes/live), and
[`/dashboard`](http://localhost:5032/dashboard) routes should remain redirects during migration.

## Project family

[`/projects`](http://localhost:5032/projects) is the only many-project portfolio route. Once an individual project is
opened, every page below has one unambiguous `projectId` context and uses the same
project-level `<SecondaryTabs>`.

| Canonical route | Tab label | Responsibility |
|---|---|---|
| `/projects/{projectId}` | Details | Project identity, status, people, summary and high-level actions. |
| ✅ [`/projects/{projectId}/structure`](http://localhost:5032/projects/{projectId}/structure) | Structure | Project tree, nodes, dependencies and structure editing. |
| `/projects/{projectId}/gantt` | Gantt | Timeline, tasks and planning. |
| `/projects/{projectId}/files` | Files | All project files; no separate portfolio/dialog file homes. |
| `/projects/{projectId}/management` | Management | Calendar, activity, reporting, risks and management controls. |
| ✅ [`/projects/{projectId}/processes`](http://localhost:5032/projects/{projectId}/processes) | Processes | Processes in this project context. |

Project-object details may be nested only when their identity is meaningful primarily
inside the project, for example `/projects/{projectId}/structure/nodes/{nodeId}`. A task
selected in the Gantt should use a query such as `?taskId={taskId}` only if it is a
transient inspector; otherwise it also receives a durable nested route.

## Processes and runs

Process definitions and process runs are separate durable objects. A run is not merely a
filtered card in a live dashboard.

| Canonical route | Tab or page purpose |
|---|---|
| ✅ [`/processes`](http://localhost:5032/processes) | Definitions list. |
| `/processes/{processId}` | Definition overview. |
| `/processes/{processId}/design` | Definition/editor, steps and graph. |
| `/processes/{processId}/roles` | Roles and assignments. |
| `/processes/{processId}/activity` | Definition-specific activity and events. |
| `/processes/{processId}/runs` | Runs for this definition. |
| `/processes/runs` | All runs; default filters may be applied but must be visible in the URL. |
| `/processes/runs/{runId}` | Canonical run details: status, timeline, output, files, agents, events and recovery. |

`/projects/{projectId}/processes` is the project-scoped list. It may link to
`/processes/runs/{runId}?projectId={projectId}` when preserving return context helps, but
`/processes/runs/{runId}` remains the canonical shareable run URL.

Examples:

```text
/processes/runs?range=last-24-hours&status=failed
/processes/runs/ddf82973-9196-406f-a0e0-8f2dd44d04ae
/processes/{processId}/runs?range=all
```

The second URL must open the run regardless of the default “last hour” list filter. The
run detail page replaces the current unbookmarkable run-details dialogs; a small dialog
can remain a preview only.

## Agents, teams, capabilities, and workflows

The current `tab` query parameter on `/agents` hides major peer areas. Give the catalog,
provider configuration, chats, capabilities, governance and diagnostics named routes.

| Canonical route | Purpose |
|---|---|
| ✅ [`/agents`](http://localhost:5032/agents) | Agent catalog/list. |
| `/agents/{agentId}` | Agent overview. |
| `/agents/{agentId}/configuration` | Identity, model/provider and editable settings. |
| `/agents/{agentId}/capabilities` | Skills, MCP servers and allowed capabilities. |
| `/agents/{agentId}/governance` | Access, approvals and policy. |
| `/agents/{agentId}/activity` | Executions, threads and logs. |
| `/agents/{agentId}/chats` | Agent conversations. |
| `/agents/teams/{teamId}` | Team overview and members. |
| `/agents/teams/{teamId}/members` | Team members, if separate from overview. |
| `/agents/providers` | Provider profiles. |
| `/agents/capabilities` | Capability catalog. |
| `/agents/diagnostics` | Runtime diagnostics. |
| `/agents/chats` | General/simple chat workspace. |
| `/workflows` | Workflow catalog. |
| `/workflows/{workflowId}` | Workflow overview. |
| `/workflows/{workflowId}/design` | Workflow designer. |
| `/workflows/{workflowId}/runs` | Workflow runs. |
| `/workflows/runs/{runId}` | Canonical workflow-run detail. |

The agent-details dialog becomes a page (with its sub-tabs), as do agent execution log
and significant runtime details. Confirmation dialogs, icon pickers, member pickers and
short setup wizards remain dialogs.

## Collaboration

The current tabs are overlapping filters, while the URL carries only `threadId`. The
proposal makes the visible list a route and the selected conversation a detail route.

| Canonical route | Purpose |
|---|---|
| `/collaboration/inbox` | Inbox items. |
| `/collaboration/threads` | All discussion threads. |
| `/collaboration/escalations` | Escalations. |
| `/collaboration/threads/{threadId}` | Canonical thread detail and replies. |

List filters are query parameters, for example
`/collaboration/inbox?read=unread&kind=escalation`. Opening a thread from any list uses
its canonical detail route. The detail page may include a back link that preserves the
originating list URL; it must not silently retain a selected thread that the visible list
no longer contains.

## CRM/HR

CRM/HR keeps its module-level `<SecondaryTabs>`, but detail records move out of dialogs
and query-only selection where they are substantial records.

| Canonical route | Purpose |
|---|---|
| ✅ [`/crm-hr`](http://localhost:5032/crm-hr) | Module overview. |
| ✅ [`/crm-hr/directory`](http://localhost:5032/crm-hr/directory) | Party directory. |
| `/crm-hr/directory/{partyId}` | Party detail; contacts, relationships, notes and assignments as its tabs. |
| `/crm-hr/accounts` | CRM account list (replaces the ambiguous `/crm-hr/crm`). |
| `/crm-hr/accounts/{partyId}` | Account detail. |
| `/crm-hr/opportunities/{opportunityId}` | Opportunity detail. |
| ✅ [`/crm-hr/workforce`](http://localhost:5032/crm-hr/workforce) | Workforce list. |
| `/crm-hr/workforce/{partyId}` | Workforce profile/detail. |
| ✅ [`/crm-hr/recruiting`](http://localhost:5032/crm-hr/recruiting) | Applications list. |
| `/crm-hr/recruiting/{applicationId}` | Application detail. |
| ✅ [`/crm-hr/agents`](http://localhost:5032/crm-hr/agents) | Business-facing agent list. |
| `/crm-hr/agents/{partyId}` | Business-facing agent detail. |
| ✅ [`/crm-hr/assignments`](http://localhost:5032/crm-hr/assignments) | Staffing and project assignment list. |
| `/crm-hr/assignments/{assignmentId}` | Assignment detail. |
| `/crm-hr/staffing-requests/{requestId}` | Staffing request detail. |

Small selection and confirmation dialogs remain appropriate. Account, opportunity,
party, application, agent and assignment details are bookmarkable pages because they
have independent identity and related subviews.

## Other durable records

| Area | Canonical routes | Notes |
|---|---|---|
| Resources | ✅ [`/resources`](http://localhost:5032/resources), `/resources/browse`, `/resources/{resourceId}` | Registry and browse are peer routes; a resource detail is a page. Storage-object promotion remains a dialog. |
| Prompts | `/prompts`, `/prompts/{promptId}` | Prompt content, versions and metadata are detail tabs. A picker remains a dialog. |
| Scheduler | ✅ [`/scheduler`](http://localhost:5032/scheduler), `/scheduler/schedules`, `/scheduler/history`, `/scheduler/schedules/{scheduleId}` | Calendar, schedules and history become route-addressable tabs. Edit may be inline on the schedule page. |
| Test Lab | ✅ [`/test-lab`](http://localhost:5032/test-lab), `/test-lab/plans/{planId}` | Plan detail tabs: overview, cases, evidence and runs. |
| Plugins | ✅ [`/plugins`](http://localhost:5032/plugins), `/plugins/{pluginId}` | Plugin detail tabs: info, executors, settings, connections, logs and grants. Package installation remains a dialog. |
| Memory | ✅ [`/memory`](http://localhost:5032/memory), `/memory/providers/{providerId}` | Provider configuration and operations become a durable detail page. |
| Settings | `/settings/workspace`, `/settings/data-sources`, `/settings/storage`, `/settings/files`, `/settings/secrets` | Each existing settings tab has a path. Provider settings belong at `/agents/providers`, linked explicitly rather than masquerading as a Settings tab. |

## Dialog decision guide

| Keep as dialog | Make a page |
|---|---|
| Confirmation/destructive-action prompts, record pickers, icon pickers, short import/export flows, attach-file flows, temporary previews, and compact wizards. | Project, process definition, run, workflow, agent, team, party, account, opportunity, workforce profile, recruiting application, assignment, staffing request, resource, prompt, schedule, test plan, plugin, and memory provider details. |

If an existing dialog is a page candidate, migration can preserve the old interaction by
opening the canonical path in a routed overlay first. The URL must still work directly on
refresh and when pasted into a new browser session.

## Migration and compatibility

1. Add canonical routes and make detail views load directly from the ID in the path.
2. Change list cards, toolbar deep links, activity events and notifications to target the
   canonical route.
3. Convert old selection/query links to redirects where a durable ID is available, such
   as `/processes/live?runId={runId}` → `/processes/runs/{runId}`.
4. Retain old routes as redirects during a compatibility window. Preserve useful query
   filters when they have an equivalent new representation.
5. Only then remove or reduce dialogs that have been superseded by page details.

This order allows route migration without requiring a wholesale visual rewrite.
