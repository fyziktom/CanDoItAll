# Full-catalog architecture and proof review

Reviewer: primary implementing agent, explicit self-review; no independent reviewer claim.
Baseline: 0ecb6307823576e80f79074187668771b166609a.

## Architecture decision

Source publication and runtime selection now share the existing effective catalog policy
through ProviderModelCatalogPolicy, a pure 21-line helper in ProviderManagement. The
duplicated runtime-only policy was removed. Source-side price normalization matches the
actual runtime editor; the importer still never manufactures missing source rates.
Ollama/image isolation and the expanded 128-model limit have behavioral tests.

Simple Chats already had runtime/Application/presentation adapters, so model labels and
source ownership are added to those existing typed records and mapped through them.
Opaque values remain selection/routing identifiers. No new framework/interface/project,
reference edge, composition-root registration, database migration or runtime partial.

AgentDefinitionFactory now reuses the existing fail-closed ProviderModelSelectionPolicy
for source-managed profiles. Published selections do not require client-editable pricing.
Missing constraints and unpublished routes are rejected; personal manual-price policy
is unchanged. Five targeted cases cover those semantics including the local rule.

Rejected shortcuts: copying a built-in model list into test data, teaching the importer
OpenAI defaults, decoding routing hashes for display, guessing missing prices, accepting
any priced model as authorization, or comparing the publication preview only to itself.

## Proof review

The primary oracle is the source Runtime model list and independent source agent selector.
The client provider metadata, agent selector and Simple Chats selector must match it.
UI setup changes source/provider/agent definitions using real controls. Persisted source
usage is read-only evidence, never inserted or pre-seeded as successful proof. The upstream
is explicitly deterministic; it does not establish current vendor/model availability.

Focused tests: source/catalog/snapshot/Simple Chats resolver 52; PostgreSQL/HTTP integration
52; component consumers 24; agent save/provisioning/workspace/import consumers 39. All pass
with nonzero discovery. The initial parity test failed before the policy fix; four save
cases failed before the downstream save fix. Full-suite proof is not claimed.

CodeAnalytics returned incomplete low-confidence impact information because production
references were not loaded with the four supplied test projects. Manual reviewed boundary
selection and exact direct factory callers are documented in full-catalog-repair.md.
Components MCP was unavailable; existing shared components were reused without layout work.

## Desktop inspection

1920x1080, existing editor/dialog scroll owners, native model popup. Visually inspected
full-catalog-ui-3 source OpenAI and client OpenAI/Ollama open selectors. Model names are
readable; the long OpenAI list scrolls inside its popup, and Save/Close remain reachable.
Source/private model-policy messages remain explicit. No new layout or CSS introduced.

Also inspected the installed-Chrome Simple Chats open selector, returned Ollama response,
and read-only three-row Ollama prices in full-catalog-simple-chats-probe-2. The native
OpenAI popup opens upward in this headless capture (its first row extends beyond the
capture); all twelve options are independently asserted and non-default selection/save
works. This is not claimed as a new layout correction. No stylesheet was changed.

Both complete runs full-catalog-ui-6 and full-catalog-ui-repeat pass. Each run's ledger
independently confirms ten complete successes, four non-default selections, a newly
written PNG and healthy engines without application error headings. The repeat's OpenAI
agent popup was also visually inspected; the same native-popup capture limitation applies.
Completed-stage source/hash gate passes (proof/transcripts/full-catalog-closure.txt).
Disposition: PASS for SPMETA's full-catalog repair; original SB07 is not closed.
This self-review is supported by the behavioral proof and gate, not independent review.
