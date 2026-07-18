# CRM/HR assignments workspace

Status: Accepted UI slice; whole-module architecture gate blocked, 2026-07-18

## Outcome and visual thesis

The assignments page is a project-scoped workspace, not a dashboard made from several forms. A compact project context bar keeps the selector, status, counts, and icon-only Structure/Gantt transitions on one responsive row. Four server-rendered tabs separate the distinct jobs: resource schedule, project relationships, staffing requests, and allocations. The resource schedule is the default overview and uses the shared Gantt component as a read-only projection of saved assignments. Editors mount only when their tab is selected; create-only party, skill, and candidate data is deferred again until the user opens a `+` dialog.

Relationship, staffing-request, and allocation tabs are list-first. Their search and typed filters occupy one toolbar row; records use a responsive two/three-column card grid with bounded paging. Inline create forms were replaced with controlled dialogs, and failed saves deliberately keep the dialog open. Allocation candidate selection is part of the allocation dialog instead of a second full-height page surface. Assignment cards show the active project and expose on-demand details for the live linked Workbench item.

The page hierarchy is:

1. portfolio-level CRM/HR navigation and staffing metrics;
2. selected-project context and compact project Structure/Gantt actions;
3. one tabbed assignment workflow at a time;
4. a thin filter row and paged card grid for the active list;
5. create and linked-work-item details in controlled dialogs;
6. explicit empty, open-boundary, and invalid-date states inside the resource schedule.

No new visual language is introduced. The implementation uses the existing BaseLib workspace tabs, cards, badges, alerts, empty states, form controls, and the existing Gantt package.

## Canonical ownership and dependency direction

Projects owns the public assignment vocabulary and integration ports, including `ProjectPartyAssignmentDetail`, `ProjectPartyAssignmentRole`, `ProjectPartyType`, and `IProjectPartyIntegrationBridge`. CRM/HR owns the canonical `ProjectPartyAssignment` persistence model and its mutation implementation. Workbench owns canonical project-node knowledge. The assignments page continues to load and mutate through the existing CRM/HR orchestration path; the Gantt receives only an immutable projection of the already loaded assignment details.

```text
Projects assignment contracts
        ^
        |
CRM/HR assignment persistence and orchestration
        |
        +--> Assignments page --> list-first editors and controlled dialogs
        |
        +--> ProjectAssignmentGanttProjectionAdapter --> Gantt contracts --> read-only chart

Workbench canonical ProjectObjectRecord
        |
        +--> IProjectNodeDetailsBridge (Projects-owned port) --> on-demand details dialog
```

The projection adapter has no database, navigation, notification, or component lifecycle dependency. The shared Gantt package receives no CRM/HR or Projects domain type. Linked work-item details are not copied into assignment persistence: a Projects-owned query contract is implemented by Workbench and reads the canonical node only after Details is requested. The Workbench registration explicitly replaces the Projects no-op default, while the default uses `TryAdd`, so module registration order cannot silently select the no-op implementation.

## Responsibility inventory

The module currently contains these major owners in one Razor assembly:

- `PartyDirectoryService`: party aggregate reads and writes;
- `PartyDirectoryManagementService`: relationships, deduplication, merge, and CSV workflows;
- `CrmService`: account, stakeholder, interaction, and opportunity orchestration;
- `HrService`: workforce profiles, skills, capacity, staffing demand, and candidate search;
- `RecruitingService`: application, interview, onboarding, and lifecycle workflows;
- `AiAgentService`: agent-party projection and technical-agent bindings;
- `ProjectPartyIntegrationService`: assignment persistence and Projects bridge implementation;
- CRM/HR pages: presentation plus substantial asynchronous state orchestration;
- persistence configuration, search, audit, memory, and automation adapters.

`CrmHrServices.cs` is a 5,699-line hotspot containing five primary services. The largest pages also retain hundreds of lines of orchestration. Several services are split through partial classes for search integration; those splits are source organization, not enforceable boundaries. The assignment service's former one-method validation partial was removed in this slice and replaced by a focused invariant policy shared by both mutation paths.

## Pattern selection record

The selected pattern is a focused adapter/projector. It converts `ProjectPartyAssignmentDetail` records into Gantt tasks with stable IDs, deterministic UTC calendar boundaries, and Person/AI-agent assignment decorations supported by the Gantt contract. Inclusive assignment end dates become UTC end-of-day chart boundaries so both the bar and built-in task table preserve the saved calendar date. Open ranges are clipped to an explicit display horizon without changing authoritative data. Invalid or unrepresentable ranges are omitted from the chart and reported to the user while remaining available in the existing editors.

A generic builder, provider registry, new interface, or new application service was rejected. There is one closed mapping with no persistence or replaceable runtime behavior; extra indirection would add ceremony without creating a boundary. Reusing the interactive Workbench Gantt panel was also rejected because it owns task mutation, dependencies, dialogs, and project-structure concerns that do not belong in CRM/HR.

The project selector/context bar is a presentation component because it is shared by all four page workflows. Relationship, staffing, and allocation editors remain presentation/orchestration components, but now expose explicit `PrepareCreate` and success-returning save callbacks. The page owns the typed load flags and data lifetime; editors own controlled-dialog visibility, local filtering, and paging. This keeps fetch policy out of visual components without inventing another application layer for a single page.

## Testability contract

The projection adapter is directly unit-testable with a supplied UTC date. Tests must prove:

- stable task identity and assigned-resource decoration;
- inclusive end-date conversion;
- deterministic clipping of open boundaries;
- explicit exclusion of reversed and unrepresentable ranges;
- allocation percentage is presentation text, not Gantt progress.

Component and browser coverage must prove that project switching remains authoritative, all existing editors remain reachable, the schedule mounts only when selected, and the chart exposes no mutation actions. Component freshness tests also record requested load flags: assignments may load for the default Gantt, staffing requests load only after their tab is selected, and party/skill/candidate catalogs load only when the corresponding create dialog opens. Existing assignment persistence integration tests remain the source of confidence for saves and deletes; Workbench integration coverage verifies that linked details come from the canonical node.

## Module-level architecture assessment

The assignment contract direction is sound: Projects does not reference CRM/HR, and consumers operate through Projects-owned ports. The CRM/HR module as a whole does not yet meet strict UI/Application/Domain/Infrastructure separation. UI pages call concrete services, services access EF and cross-module integrations directly, entities and EF configuration are colocated, all folders share one namespace, and runtime correctness depends on replacing no-op bridge registrations in the composition root.

The architecture verdict for the whole module is therefore **blocked**, not a claim that the module is already properly separated. The assignments slice closes its local projection and mutation invariants, but a UI task must not smuggle in broad transactional rewrites. The blocking follow-up work should proceed by cohesive responsibility:

1. make party merge type-safe and aggregate-complete before deleting a source party; cover CRM profiles, stakeholder links, workforce profiles, AI bindings, and real foreign keys;
2. make opportunity-to-project conversion atomic, or model it as a durable idempotent saga with reconciliation;
3. make `PartyType` immutable after creation or introduce an explicit aggregate-migration command;
4. move search and sensitive-data projection to an idempotent transactional-outbox path so committed canonical state cannot leave stale discoverable documents;
5. enforce logical assignment uniqueness in the database and add composition tests proving real assignment/node bridges replace no-op defaults;
6. centralize time-bounded role evaluation and propagate committed sensitivity/access state into agent-chat projections;
7. extract the five services from `CrmHrServices.cs`, replace cross-file private partial extensions with explicit collaborators, and move large-page orchestration behind focused test seams;
8. separate EF configuration from domain-facing models, then evaluate project splits only after source-level dependencies are explicit.

New partial classes are prohibited for this work. The assignment validator was converted from a one-method partial into a focused invariant policy used by both mutation paths. A project split is deferred until source-file extractions expose stable dependency seams; splitting the current dependency cluster immediately would create cyclic references or an anemic contracts project without reducing coupling.

## Closure gates

- Every existing assignment, staffing-request, candidate-search, allocation, delete, reset, project-structure, and project-Gantt action remains reachable.
- The selected project is visible and switchable outside the workflow tabs.
- Record lists are responsive paged grids, and create forms do not consume page height while closed.
- Linked work-item detail is queried on demand from Workbench rather than copied into CRM/HR records.
- The Gantt is read-only and built from the current project selection only.
- Invalid and open ranges are explicit; no fallback silently changes persisted data.
- The adapter has direct positive and negative tests.
- Targeted unit, component, integration, and browser checks pass.
- The Web host is rebuilt, port 5032 is restarted, and the page is validated interactively.
