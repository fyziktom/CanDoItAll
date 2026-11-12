# Redesign Brief: Meaning Before Form

This brief turns the reconstructed product semantics into design decision criteria. It
does not prescribe a component library, visual style, density, or layout system. Those
are later implementation choices that must serve these product constraints.

## The design problem

CanDoItAll combines three kinds of work that are often separated into different tools:

1. **Deliver work** — organise a project, structure its work, plan it, assign it, and
   retain files/artifacts.
2. **Operate governed automation** — configure AI agents, processes, workflows and
   ordinary LLM conversations; launch them; then inspect state, cost, outputs, approvals,
   and recovery.
3. **Coordinate organisational capacity** — maintain parties, customer opportunity,
   workforce skills/capacity, recruiting, and the handoff of people/agents into projects.

The product needs to make their relationships legible without flattening them into a
single generic “workspace item.” This is an **Inference** backed by the entity model,
route families, and cross-area dialogs recorded in the other UI documents.

## Proposed conceptual model

This is a design lens, not a required navigation tree.

```text
Delivery context (Project)
├── Plan: structure, tasks, dependencies, calendar, Gantt
├── People and AI: assignments, staffing, project-aware agent access
├── Automation: attached/launchable processes and workflows
└── Evidence: files, assets, run outputs, activity, approvals

Reusable operational definitions
├── Processes, workflows, agents, Simple Chat definitions, prompts
└── have lifecycle/version/configuration independent of a Project

Operating foundation
├── Parties, workforce, recruiting, opportunities
└── providers, secrets, storage, plugins, memory, database/runtime profile
```

The key design decision is therefore **context first, then mode**:

- A user entering a project should see the delivery context and choose planning,
  coordination, automation, or evidence views from there.
- A user entering an automation catalogue should see reusable definitions and their
  lifecycle, then choose where/how to run one.
- A user entering a running instance should see execution state and evidence before
  configuration affordances.

## Design principles

### 1. Name the current object, owner, and scope

Every major surface should answer: “What am I looking at?”, “Which project/workspace is
it in?”, and “What can I safely affect?” A project-scoped Process view and the global
Process catalogue can share primitives, but they should not look like the same scope.

### 2. Separate authoring, launching, and operation

Definitions are edited and lifecycle-managed. Launch is an intentional bridge that
records configuration/context. Operation screens focus on live/historical instances,
state, output, approval, cancellation, rework and recovery. Do not turn a run-history
view into an editor merely because it can navigate back to its definition.

### 3. Make state an explanation, not decoration

Status must identify its subject and next consequence: for example, “workflow run waiting
for approval”, “Simple Chat recovery required”, “schedule paused”, or “project deletion
cleanup pending.” A colour/icon alone cannot carry the product contract.

### 4. Preserve evidence and causality

For executions, users need a traversable chain:

```text
definition/revision → launch context → run/operation → events and decisions → outputs/artifacts
```

The interface should make this chain inspectable and avoid implying that an artifact or
assistant response appeared without accountable input/context.

### 5. Keep structured work and conversation complementary

Chat is useful for asking, drafting, and acting with an agent. It must not obscure
structured project/workflow/process state. Agent chats should reveal their governed
context; Simple Chats should stay visibly ordinary, revision-pinned conversations without
agent/tool/workspace implication.

### 6. Treat human and AI capacity as coordinated, not identical

Both can be assigned or assessed, but UI must retain which details are relevant to each:
people/organisations, skills/capacity and privacy for workforce; providers, capabilities,
governance and execution evidence for agents. Shared assignment views should make the
common delivery commitment visible without erasing those differences.

### 7. Escalate only the decision that needs the user

Approvals, recovery, destructive deletion, script launch, provider grants and secret
configuration need deliberate confirmation. Present the affected scope, consequence,
available evidence, and viable choices. Do not use generic confirmation language for a
choice that changes durable delivery or execution state.

### 8. Treat configuration as readiness, not background clutter

Provider, storage, plugin, Memory and database-profile configuration are prerequisites
for parts of the product. Surfaces should communicate readiness, failed checks and exact
blocked capability near the affected user task, with a clear route to the owning setting.

## Non-negotiable cross-area journeys

A redesign should support these transitions without forcing users to reconstruct the
domain relationship themselves:

| From | To | Relationship that must stay understandable |
|---|---|---|
| Opportunity | Project | a commercial win becomes delivery work through explicit conversion |
| Party / workforce evidence | Project assignment | the selected participant is appropriate and allocated to that project |
| Project node/task | Process or workflow | automation is attached/launched in a named delivery context |
| Definition | Run/operation | a particular revision/configuration caused the instance |
| Run/operation | Approval/recovery/artifact | state and evidence explain what happens next |
| Prompt Gallery | Chat composer | a curated prompt is an input to a conversation, not a second transcript |
| Settings/readiness | blocked action | configuration ownership is clear without exposing secrets |

## Design acceptance checks

Use these questions when evaluating a proposed flow or visual redesign:

1. Can a user tell whether they are editing a reusable definition or viewing an executed
   instance?
2. Can they name the project/workspace and participants affected by an action?
3. Can they reach the immediate evidence for an automation result, failure, or pause?
4. Does a destructive, approval, or recovery action explain its durable consequence?
5. Does the design distinguish agent work from ordinary Simple Chat without requiring
   users to know implementation terminology?
6. Does it retain the handoffs between CRM/HR, project planning, and automation?
7. Does empty/loading/error state explain what is missing or blocked, instead of making
   the product look like it has no data?

An answer of “no” is a product/UX defect regardless of whether the individual controls
look polished.
