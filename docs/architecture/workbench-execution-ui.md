# Workbench execution presentation

Status: implementation in progress under WB6. This record does not claim demo readiness.

## Boundary decision

The remaining Workflow and Process presentation belongs in a single
`CanDoItAll.Workbench.Execution.UI` Razor library and an independent
`CanDoItAll.Workbench.Execution.UiSandbox`. The Workbench module remains the native host.
The existing Structure, Planning, Operators, Authoring, Insights and Content boundaries
remain in place. No other module extraction is part of this work.

The leaf renders the Workflow add/input/preview/start forms, Process link/confirmation,
the full staffing workspace and its actual candidate picker, candidate details and switch
confirmation. A Workbench wrapper may adapt native values and callbacks; it must not retain
the extracted form markup or supply a native child through a render fragment. Managed-file
delete disposition completes the existing Content family.

Use immutable presentation snapshots and callbacks captured for the displayed opening.
Only role, candidate, saved definition/version and opening identifiers needed for UI events
cross the boundary. Provider/model names are display values. Routing, credentials, authority,
prepared requests, native services and mutable host sessions remain in the module. A child
result names its original opening and role; it cannot use a later parent parameter after
an await. Close retires the view and does not undo an accepted operation.

Use the existing neutral BaseLib/CanvasLib overlay, record picker and layout components.
Do not introduce a generic workflow engine, transaction coordinator or service locator.
The module maps native launch state to explicit presentation flags; the renderer does not
infer authority from a label or from the presence of an arbitrary object.

## Original Workflow lifetime repair

Each opening captures its project admission, actor, runtime generation, original node and
navigation revision. Each submission has a handler-level busy gate. Option and input reads
also carry a request revision. Completion may update only the original current opening;
the bounded native outcome history retains accepted results after its view retires.

A saved Workflow start keeps its original intent, definition version and accepted run.
Observation uses that admission and does not call Start again. A known rejection can be
corrected; an unknown effect remains locked to observation. Accepted creation retains the
created node even when refreshing the canvas fails. Canonical task attachment keeps its
existing native owner and compensation evidence.

Projected project/task nodes are reconstructed on reads. Their temporary record IDs are
not persistent identity. The parent check compares owner identity and project admission;
persisted nodes additionally retain their record ID. Changing the saved Workflow version,
input settings or original persisted node invalidates a fresh start.

## Proof and closure

Validation must cover actual native owners, stale callbacks, two views, replacement project
lifetimes, duplicate submission and failed observation after acceptance. Renderer tests
exercise the actual child family. The standalone sandbox must publish and run without
Workbench, Process application/runtime, provider execution or persistence dependencies.
Evaluated build references, runtime assemblies, rendered children and asset/watch inputs
are separate checks; unresolved references do not prove isolation.

All browser evidence uses 1920 by 1080 pixels at DPR 1. Keep deterministic evidence separate
from genuine-model runs. Recovery preserves the data-protection keys, storage catalog and
original host-binding identity together with the database. Closure requires the final
native candidate, repeated recovery, signed source commits and the Czech operator runbook.

## Workflow checkpoint

The Workflow add and start forms now render in the Execution library. The native adapter
preserves full input state, uses typed Workflow identities and captures callbacks for each
opening. Independent renderer tests passed 5/5; the native Workflow lifetime suite passed
26/26. Two desktop scenario views preserve independent raw input and observation state.
The sandbox requires Development for the source-assets loop; published assets are validated
separately at final closure. Process and final candidate proof remain in progress.
