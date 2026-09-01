# Information Architecture

This is a map of current product responsibilities, not a recommendation to preserve the
present navigation chrome. Route names and page components are evidence that an area is
already a first-class surface.

| Area | Current entry surfaces | User responsibility | Primary objects |
|---|---|---|---|
| Dashboard | `/`, `/dashboard` | Orient to the current workspace and active work. | Workspace, snapshots, live items |
| Projects | `/projects` | Browse/create projects, portfolio and hierarchy. | Project, subproject, files |
| Project workbench | `/projects/{id}/structure`, `/projects/{id}/calendar` | Structure, plan, inspect and operate one project. | Project node, task, asset, dependency, schedule |
| Processes | `/processes`, `/processes/live`, project-scoped process routes | Define, launch, monitor, recover repeatable procedures. | Process, role, step, process run, artifact |
| Agents and chats | `/agents`, `/agents?tab=simple-chats`, `/chats` redirect | Configure/govern agents, run them, and use both agent and Simple Chat conversations. | Agent, team, provider, capability, session, definition, conversation, operation |
| Workflows | `/agents/workflows` | Define, validate, version, run and inspect automation workflows. | Workflow definition, component, workflow run, checkpoint, artifact |
| CRM/HR | `/crm-hr` plus directory, CRM, workforce, recruiting, assignments and agents subroutes | Maintain organisational/workforce context and hand it off to delivery. | Party, account, opportunity, workforce profile, skill, application, assignment |
| Prompt Gallery | `/prompt-gallery` | Curate reusable prompt assets and use them in conversation composition. | Prompt item, version, tag |
| Resources | `/resources` | Manage reusable workspace resources and their stored content. | Resource, storage object |
| Scheduler | `/scheduler` | Schedule process/workflow launches and review their history. | Schedule plan, planned target, schedule run |
| Workspace settings | `/settings`, `/settings/runtime-capabilities` | Set data/runtime/storage/provider/secret/API operating prerequisites. | Database profile, storage catalog, secret, provider profile, capability fact |
| Plugins | `/plugins` | Install and operate integrations. | Plugin, package, grant, connection, log |
| Memory | `/memory` | Configure and inspect memory providers/operations. | Memory provider, operation, event, feedback |
| Collaboration | `/collaboration` | Triage durable notifications/escalations and discuss them in their process, launch, or automation context. | Inbox item, collaboration thread, participant, message |
| Test Lab | `/test-lab` | Plan and record delivery assurance, evidence and execution results. | Test plan, test case, evidence record, test run |

## Navigational relationships

```text
Workspace / selected database profile
├── Projects ──> Project workbench ──> structure, tasks, files, schedule
│                         ├── process/workflow launch and run evidence
│                         └── staffing and agent context
├── Agents and Chats ──> agent execution OR ordinary Simple Chat
├── CRM/HR ──> parties, workforce, opportunities, recruiting ──> project staffing
├── Delivery assurance ──> Test Lab ──> cases, evidence, execution history
├── Governed exceptions ──> Collaboration ──> context-linked threads and escalations
├── Shared operating assets ──> prompts, resources, Memory, plugins
└── Operating configuration ──> providers, secrets, storage, runtime readiness
```

The relationship diagram is **Corroborated** except for the exact top-level grouping,
which is an **Inference** intended to guide redesign discussion.

## Screen-contract coverage and remaining work

The initial screen-contract pass is complete: [screen contracts](screen-contracts.md)
cover the workspace shell; dashboard; collaboration; portfolio/workbench/schedule;
processes; agents/chats; workflows; scheduler; CRM/HR; prompts/resources; operating
configuration; and Test Lab. These contracts describe responsibilities and user actions,
not current layout. Generic runtime recovery routes are intentionally excluded.

The documentation-evidence pass is complete. Future automated-test expansion is useful,
but does not block this product/design reconstruction:

- Test Lab’s page/model audit establishes its aggregate editing behavior; add dedicated
  UI characterization tests later if that surface changes materially.
- Collaboration’s page/contract audit establishes filtering, creation, reply and context
  navigation; add component tests when its interaction model evolves.
- Agent tool-governance evidence has been reduced into the user-facing boundary above;
  retain the broader runtime suite as implementation assurance.

The remaining items in [the reconstruction overview](README.md) are stakeholder product
decisions (roles, navigation positioning, and ownership), not source-discovery gaps.
