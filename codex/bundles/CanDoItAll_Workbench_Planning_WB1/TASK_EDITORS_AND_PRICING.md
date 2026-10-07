# Task forms, resource selection and pricing

Sources S21–S27. Both form families and all shared children are in scope; leaving the hard
fields or pricing widget in the module would make WB1 incomplete.

## Keep the forms' intentional differences

| Mode | Fields / intent that must survive |
|---|---|
| Gantt create | Title, UTC interval, delivery presets, effort/cost and optional supported resource; insertion position belongs to original timeline |
| Gantt edit | Original task identity, expected interval/progress, execution snapshot, cost basis, direct-assignment revision and additive definition resource |
| General Structure create | Title, status lane/subtitle, due UTC, repository reference, notes, effort/cost; person/agent direct assignee, not a fabricated resource child |
| General Structure edit | Existing general fields plus execution and permitted additive Workflow/Process attachment; preserve multiple original assignees |

Inspect complete current TaskCreateDialog state, CanvasTaskDialogCoordinator, task policies
and all child types during the caller census. Do not infer general form save semantics from
Gantt alone. Keep title limits and native invariants, but retain raw invalid text instead of
clamping, dropping it or submitting the last valid value after an unrelated field changed.
One stable draft/EditContext per original editor survives section changes and async quotes.

Reuse actual `ProjectStructureTaskEstimateEditor`, `ProjectStructureTaskExecutionEditor`,
`ProjectStructureTaskResourcePicker` and `ProjectStructureTaskResourceCostEstimator` behavior
through light seams. Shared children must not expose the full Workbench service graph.
Currency display/configuration comes from narrow native facts, not Infrastructure DI inside
an otherwise isolated renderer. Preserve value identities and serializer/wire contracts.

A person/agent is a direct assignment; a Workflow/Process is an additive definition attachment.
Clearing a pending attachment must not remove the assignee. Invalid multiple direct-assignee
sets stay intact and disable only unsafe replacement. Selecting an agent grants no project
access and attaching a definition does not run it. Create/Apply must follow actual parent
semantics, not a newly invented universal Save operation.

## WB1-Q1 implementation obligation

First make controlled tests fail for NotStarted -> Started/Unknown during an in-flight quote,
and drive the same transition through one real task editor. The quote origin includes the
original project/profile lifetime, resource, estimate revision, execution eligibility and
specific opening/operation. A cached answer also belongs to that scope. Re-evaluate eligibility
before applying any success, unavailable result, error, status or callback. A-B-A must not
resurrect an old operation just because values compare equal again.

Detach a superseded cancellation owner before retiring it; its resources are disposed when
that operation has safely unwound. Do not dispose a CTS that a still-running resolver may
still access. Close/dispose retires callbacks and clears owned sensitive/transient state.
A valid explicit cached request can avoid another read; renderer updates and ordinary field
rendering must not produce network/DB quote loops. Preserve explicit force-refresh.

Historical cost is not recomputed for tasks that no longer permit authoritative repricing.
Unknown is not implicitly NotStarted. Changing effort or manually editing cost while a quote
is pending preserves newer input. Unavailable valid quote clears only its own preview amount
and currency (not zero); a failed read does not assert a missing authoritative rate. Do not
silently convert currencies or sum amounts across currencies.

## Native save and recovery

TaskApplicationService checks original execution, estimate, cost basis and direct-assignment
revision and re-reads authoritative prices (S27). Gantt mutation applies original title,
progress and other expected values under native coordination (S26). These remain mandatory.
Preview pricing never substitutes for Save-time authority or a locked snapshot.

Document the real commit order for each create/edit path, including task creation, assignment,
pricing, row ordering, definition attachment and compensation. Use existing result/exception
facts. A new narrow owner outcome is allowed only if needed to communicate a real gap;
no generic durable protocol, new task table or schema redesign is in scope.

Capture immutable submission before async work. A returned ID/version belongs to that exact
operation and is retained before a follow-up read or parent callback. Failure after task save
cannot be relabeled total failure. Compensation can fail or be refused by changed authority;
retain original and compensation failures without retrying on a replacement project.
A lost acknowledgement must preserve the draft and exact known identities and prevent blind
create replay. Do not infer successful compensation from absence of one UI row.

Parent dialogs close only their own reference; no global close-all. New editor/target and
same-record section change are different transitions. Test queued Submit/Enter/Cancel and
late quote/readback with two stacked independent dialogs. Safe errors/logs omit private
connection details, command content and credentials.
