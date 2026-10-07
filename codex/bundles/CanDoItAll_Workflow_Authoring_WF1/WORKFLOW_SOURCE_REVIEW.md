# Current Workflow source review

These observations concern pre-existing code selected for WF1, not regressions attributed to CA1.
They are source-derived unless explicitly reported otherwise. Build actual reproductions with valid
native fixtures before classifying a product defect. Sources and inspected ranges are in SOURCES.md.

## WF-R1 — reduced canvas mapping can discard unedited definition data

`WorkflowCanvasDocument` stores only a subset of the definition. `FromDefinition` and `ToDefinition`
in S18 do not carry definition InputParameters. The Save request in S17 sets only constructor fields,
while native S20 assigns `InputParameters = SnapshotInputParameters(request.InputParameters)`.
Thus native parameters are replaced by the omitted/default list, not inferred from the previous
version. A simple valid parameterized workflow edited only in its title is the required reproduction.

Also inventory node value shapes/schema, explicit ports and edge port IDs, embedded component/version
snapshots, coordinates at zero, policy and extension data. The mapper regenerates ports/shapes and
applies coordinate defaults. Do not assert loss for every field before testing its native semantics,
but do not limit the round-trip test to nodes count and names. See LOSSLESS_DOCUMENT_CONTRACT.md.

## WF-R2 — Save receipt and live editor are conflated

The canvas awaits native Save, immediately replaces `document`, then awaits validation and parent
notification. A secondary failure gets a generic unconfirmed-save message, and later local edits
can be lost by replacing the whole document. S17. The page additionally keys the canvas using
workflow/version (S14–S15), so preserving the child model alone may still lose it when parent Save
changes the key. Solve both layers with the original acquisition and submission, not by disabling
all editing indefinitely or removing version checks.

Known committed workflow/version IDs precede optional validation/read-back. An uncertain persistence
acknowledgement is genuinely different and must not be blindly retried. Native head/concurrency
rules are already present (S20); preserve them instead of adding duplicate last-writer logic in UI.

## WF-R3 — Prompt selection creates a distinct durable component

`SavePromptGalleryComponentAsync` in S17 checks compatibility before setting isBusy, then saves a
new component and awaits a parent library callback. Preserve the exact selected prompt/version,
provider/model, original node and accepted component ID across those stages. A delayed compatibility
answer, removed node, changed profile or failed refresh must not create/bind another component by
accident. Setup of the component and subsequent definition Save are not one transaction.

## WF-R4 — reads/progress need document and operation ownership

The original canvas initializes secrets before applying the intended definition, loads preview
project options into shared fields, validates asynchronously and observes run progress through a
callback capturing the component. A newer document can reuse the same node IDs. Require control of
late success, failure, completion and progress so that no predecessor targets a successor. An
initial toolbar or heading is not editor readiness. Existing repository test guidance already warns
about this (S22, S28).

## WF-R5 — the renderer is not just the visible canvas

The page retains template preview, two preview-input paths, run and event dialogs (S14). The actual
canvas contains floating windows, Prompt picker and nested settings components (S16–S17). The image
settings renderer directly loads providers and receives module Security types (S19). Complete the
whole descendant/asset graph without bringing these owners into the new leaf.

Current custom renderer selection maps trusted Application/BundledPlugin origins to their owner and
schema (S17). Keep this check in its owner. A generic fallback is not an escape from explicit denial.

## Existing protections to retain

Native persistence checks exact head/version, snapshots graph and input parameters, enforces stable
identity uniqueness and returns the saved definition after SaveChanges (S20). Preview converts an
Active graph into an unsaved Draft with a new version where required; it never labels unsaved content
as an immutable saved Active version (S17). Page routes and caller callbacks already carry page/
target/navigation identities (S15). Existing shell/analytics are already extracted (S12–S13).

Preserve safe event formatting, bounded payloads, explicit read/retry and native owner authorization.
Refactoring code length or file count is not an acceptance criterion.
