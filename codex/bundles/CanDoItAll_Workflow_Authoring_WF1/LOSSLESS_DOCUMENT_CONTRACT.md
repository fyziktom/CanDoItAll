# Lossless Workflow editing

The original immutable native definition is the baseline. A canvas view is a projection of it, not
a replacement authoritative schema. Preserve supported fields the editor does not expose, and
retain raw values needed to show/fix invalid input. This does not authorize new designer features.

## Required comparison matrix

For each family, construct a valid native definition, save it through the existing owner, load it
through the actual editor, change one supported field, save through the new path and fetch that
exact accepted version from a fresh native owner. Compare semantic values before/after with a
small explicit allow-list of intended changes and native identity/time normalization. Never remove
a difference from the test simply because the current mapper loses it.

| Family | Must remain intact unless explicitly edited |
|---|---|
| Definition identity | Workflow ID, expected/current version semantics, original creation identity and stable external/template provenance as governed by the owner |
| Input parameters | Order, exact keys, requiredness, defaults, type/schema and configuration of each descriptor |
| Graph | Start identity, supported node/edge kinds, ordered data where meaningful, exact node/edge IDs and all unrelated nodes |
| Ports and shapes | Explicit source/target ports including null semantics, port IDs/direction, richer input/result schemas and supported metadata |
| Node settings | Component/agent/subworkflow IDs, external request kind, exact instruction content, executor settings/policy, provider/model selections and hidden settings |
| Immutable dependencies | Native component/version/prompt snapshots; no implicit rebind to a new library head merely because a catalog was refreshed |
| Routes | Conditions, typed routing operators/value kinds, case flags, fan-out indices, labels and non-edited branches |
| Presentation coordinates | Legitimate zero/negative coordinates, moved nodes, viewport/selection lifetime; document data is distinct from local viewport state |
| JSON and extensions | Supported unknown/nested fields and key case; invalid raw input is retained and cannot silently become an empty object |
| Neighboring aggregates | Other workflow definitions/versions, prompts/components, project contents and scheduled jobs unchanged unless the operator requested their specific action |

Use current model definitions and native serializers as the source of truth. S18 identifies reduced
mapping; S20 proves the current native replacement semantics. Do not infer all possible lost fields
from a name list alone. The `templates/roundtrip-matrix.json` is an unexecuted checklist, not a JSON
schema or replacement native model.

Prefer retaining the baseline and applying explicit edited deltas, or another demonstrably lossless
projection. Do not expose privileged StructureAuthority in the editable document. Native save may
legitimately create a new immutable version and canonicalize values; record the exact allowed changes.

If an input cannot be faithfully represented/edited, show an explicit unsupported/read-only state
preserving original bytes and references. Never load a rich existing definition as a blank draft,
change its status to runnable automatically, or save an approximation. Before claiming the selected
family complete, the intended ordinary valid imported/template/runnable fixtures must all work.

After own confirmed Save, adopt the returned workflow/version and immutable dependency facts, merging
only untouched fields. Later edits and raw validation survive. A foreign save remains an optimistic
conflict; do not attach its latest expected version to a stale whole-document submission.

For unsaved previews, hold one immutable submitted graph/input and label its origin. Validation,
preview and Save may each see a different draft revision only when the operator explicitly submitted
that revision. The renderer cannot certify a newer revision with an older validation/preview result.
