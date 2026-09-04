# Component boundary assessment — AgentDetailsDialog

## Identity

- feature-owned technical-agent editor;
- remains in current module and file location;
- future destination: AgentFramework feature UI, not `AppComponents`.

## Rendering responsibility

Ten editor sections, editor draft fields, section-specific validation/presentation,
sticky actions, confirmation/wizard presentation, and result completion.

## Current external responsibilities to remove

- load of agents/providers/capabilities/secrets/projects;
- existing/new editor construction;
- persistence and post-save refresh;
- delete mutation;
- capability persistence and verification;
- external-root/save canonicalization requiring infrastructure services.

## Target public contract

```text
Guid? AgentId
AgentDetailsSection SelectedSection
EventCallback<AgentDetailsSection> SelectedSectionChanged
AgentEditorSession? InitialSession
EventCallback<AgentDetailsDialogResult> Saved
```

`InitialSession` is a legitimate scenario/test/sandbox seam. Production may omit it and
use `IAgentEditorController.LoadAsync`.

## Target injected dependencies

- `IAgentEditorController`;
- `DialogService`;
- `NotificationService`;
- cascading `DialogReference`.

Child components may retain their own documented technical dependencies. The dialog
itself must not inject Workspace, provider administration, Projects, Secrets,
`IExternalTargetPathRegistryFactory`, EF, or `IServiceProvider`.

## Error semantics

Preserve current distinctions:

- core agent/capability load failure prevents a valid editor session and is presented as
  an editor load error;
- provider and secret catalog failures remain partial errors with usable remaining data;
- project access items remain lazy and independently retryable;
- failed save/delete leaves the editor open and usable;
- successful dialog deletion completes through the dialog result channel exactly once.

## Rejected extraction

- ten immediate section wrapper components;
- a generic form-controller base;
- one interface per reference-data source;
- moving all pure UI toggles/formatters into the controller;
- preserving numeric tab indexes in public/test contracts.

## Readiness after bundle

- route-ready: stable details section and page-owned target, URL deferred;
- sandbox-ready: yes at component level using `InitialSession` and fake controller;
- project-extraction-ready: partial, blocked by child-component and model project
  dependencies to be inventoried in closure.
