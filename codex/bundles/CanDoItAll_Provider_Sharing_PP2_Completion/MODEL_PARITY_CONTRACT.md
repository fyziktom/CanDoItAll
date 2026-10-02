# Model names, routing identities and metadata parity

The operator requires the model to have the same NAME after sharing. Do not implement this by
replacing client routing IDs with source names. R19 defines stable opaque `sp1...` route IDs,
R20 publishes display names, and R21 materializes the imported snapshot. R23 expressly avoids
applying local OpenAI suggestion filtering to a source-managed import.

## Independent identities

| Value | Must remain true |
|---|---|
| Source instance + publication ID | Identifies the remote publication, not its editable provider label |
| Source upstream model token | Exact model accepted by the source driver/upstream; canonical source value is the oracle |
| Catalog model ID | Opaque route scoped to publication; duplicate names in different publications stay distinct |
| Catalog DisplayName | Exact accepted source model name; no kind-derived substitutions, truncation, case changes or token labels |
| Local imported provider ID | Stable for existing import per current native rules; independent between clients |
| Client's selected wire model | Exact public routing ID, NOT the displayed label; default inheritance is not an explicit override |
| Local provider alias | Operator-editable provider label only; cannot rewrite model names, publication identity or source ownership |

A source upstream-model rename is a different routing key under the existing codec, not a demand
to keep the old model route. Provider display rename, unchanged model metadata updates and
unpublish/reappearance must preserve identities according to the current native contract.
Do not invent ID stability beyond what the protocol supplies.

## Two catalog layers, not a misleading count assertion

Compare the complete eligible publication model set, default, exact labels, prices, Thinking
and capability flags against the source owner BEFORE comparing a UI selector. The selector may
intentionally show suggested models plus a default/saved selection rather than the whole catalog.
R20 publishes IsSuggested using the existing native OpenAI policy; the client must honor that
public fact, not rerun OpenAI name heuristics on opaque route IDs. Compute expected options from
captured source/public metadata and the selector contract, independently of the client mapper.

Test main suggestions, dated/unsuggested allowed models where shipped, default excluded from the
suggestion filter, and a saved allowed non-default selection. A removed saved model remains visibly
unavailable; do not silently bind a different model with the same label. Sorting may be deliberate;
compare identity-keyed content and documented order separately. Default wrappers such as
'Provider default (name)' are expected UI decoration, not a changed model name.

## Required provider fixtures

| Fixture | Purpose |
|---|---|
| Persisted native OpenAI Chat with as-delivered accepted model definitions | Main operator requirement; source presets are not rewritten for a synthetic-only test |
| Native OpenAI with operator-customized catalog/default | Presets must not override explicit source configuration after import/restart |
| Native OpenAI Image, if current driver supports publication | Names/purpose/image prices; native fixture image dispatch and artifact hash |
| Native Ollama with multiple models, default/non-default and supported vision | Imported transport may be OpenAI-compatible but source names/capabilities remain Ollama's |
| Two publications with identical upstream model names | No collision, cross-publication default, wrong source or name-based routing |
| Client personal provider with colliding name/default | No implicit fallback during source outage/denial/removal |
| Runtime-only seed and ineligible publication controls | Preserve current refusal; does not replace the eligible native-default test |

Use deterministic fixture responses behind real adapters. Native-defaults refers to current
repository/application metadata, not today's public OpenAI catalog and not a new online lookup.
Only add more native driver families if they are already shipped and relevant to a changed seam.

## UI surface matrix

For source and each client check catalog rows/tree tooltips/accessibility descriptions,
Connection default, Runtime model text, Prices row labels/coverage, Thinking model names and
control defaults, Sharing imported facts and source discovery catalog, Agent Runtime/Images
selectors, Simple Chat definition selector and persisted reopen, and Workflow picker when it
uses the shared model selection contract. Never use DOM option values as human labels.
Opaque IDs may be present in safe explicit diagnostics, hidden identifiers and network protocol;
do not blanket-ban them from all HTML and accidentally force unsafe routing changes.

## Mutation/resync variants

Change default without changing model set; add/remove a non-default; remove the current default
only through valid source setup; source provider rename; client alias change; price null/zero;
Thinking automatic/configured/unknown/None distinction; independent client selections; imported
local disable; credential failure; source identity mismatch; stale load after newer revision;
central/client restart; clean reimport and ETag/304. Assert source-to-client equality only for
remote-owned fields. An operator's explicit client alias/enabled intent remains local.

All numeric prices must keep units and precision and distinguish null/unpriced from zero.
Thinking settings do not create upstream support; direct local imported edits must be refused
at the real owner even if a disabled UI handler is invoked manually. Source tokens and upstream
credentials are distinct, and neither enters metadata snapshots.

## Evidence

Export one safe parity snapshot per meaningful phase, naming the actual source/image and accepted
publication revisions. `templates/model-parity.json` and `tools/check_model_parity.py` help catch
missing/extra models, wrong labels/defaults and publication confusion. They check supplied data
only; pair them with actual container identities, browser assertions and captured dispatch facts.
The template has no accepted result. Do not use helper success as a replacement for executions.
