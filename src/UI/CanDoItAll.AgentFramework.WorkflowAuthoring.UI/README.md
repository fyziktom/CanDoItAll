# Workflow authoring rendering

The production canvas, floating toolbox/selection/component windows, graph and node
inspectors, route fields, execution policy and image settings live here. The same
components render in the independent WorkflowAuthoring.UiSandbox.

`WorkflowCanvasSurface` owns the editable document, canvas viewport, selected node
occurrence, nested dialog origins and draft revisions. The native module supplies
typed document, Prompt binding and preview operations. An accepted save updates its
identity before notifying the host; later edits remain in the document. A baseline
of the complete native definition preserves fields that the editor does not change.
Invalid raw JSON, GUIDs, numbers and route indices remain visible.

`WorkflowRouteFields`, `WorkflowExecutionPolicyEditor`, the provider/model selector
and image settings surface share the actual forms. The native settings slot keeps
renderer key, owner, trust and schema checks. No provider, database or runtime
service is resolved by this library. Opaque source model route IDs remain exact.

`WorkflowPreviewInputDialog` serves both native entry paths. Template catalogue and
read-only template canvas, run detail and event detail are also actual reusable
dialogs. Native owners retain reads, redaction, authority capture and admission.
Unknown effect acknowledgements never enable an automatic retry.

The existing `Workflows.UI` five-tab shell remains separate and lightweight. This
library consumes its catalog/result presentation; it does not introduce a reverse
reference. The evaluated sibling-source closure is 17 projects; the shell remains
the same four projects. See [WF1 evidence](../../../docs/architecture/workflow-authoring-ui-wf1.md)
for qualified validation and remaining work.
