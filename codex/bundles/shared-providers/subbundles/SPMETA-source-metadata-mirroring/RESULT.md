# SPMETA result and operator handoff

State: `DONE`; completed-stage source, artifact-hash and proof gates passed. Current repair:
[full-catalog-repair.md](full-catalog-repair.md).
Original SB07 remains `BLOCKED`; this is not whole-bundle closure.

## Current full-catalog repair

The source runtime added built-in OpenAI choices, but the publication reader only used
persisted suggestions/default. They now use one effective catalog policy. The importer
receives the complete source-selectable set without adding unrelated driver defaults.
Source-normalized prices are mirrored; absent prices remain absent, not invented zeros.

Two downstream defects were also repaired: Simple Chats dropped display labels/source
ownership in its own adapters, and agent save required a price for any model override.
Shared agent selections now use the published model constraint, allowing advertised
unpriced models while rejecting unpublished or missing-constraint selections. Local
manual-provider pricing requirements remain unchanged.

The current test setup has identical source/client sets: 12 OpenAI chat models (10 priced,
2 explicitly unpriced), 3 private Ollama models with 3 matching price rows, and one image
model. All nine price fields and private flags match. Saved opaque route IDs remain stable;
the UI displays source names. No project/reference, migration or authorization change.

Validation: 52 unit, 52 integration, 24 component and 39 agent-save/consumer tests pass
(167 focused executions, not the whole suite). The isolated Simple Chats UI probe passes.
Both complete Chrome UI runs, full-catalog-ui-6 and full-catalog-ui-repeat, pass with
10 successful central operations and complete token/image usage per run. Each executes
gpt-4.1-mini and e2e-ollama-secondary
through agents, gpt-5.4-mini and e2e-ollama-vision through Simple Chats, plus image generation
and image analysis. Runtime proof confirms image input, fresh PNG, HTTP 200 health and
no application error headings in either engine during either run.

Both engines are rebuilt and running: source http://localhost:5210, client
http://localhost:5212, image `candoitall-shared-providers-ui:fullcatalog-20260827-2`
(`sha256:db76a05c23434b3fb660c9d4546c9dcd725add605ce76382441939f85d914d6d`).
Volumes and rollback containers are retained; 5032 and unrelated PostgreSQL are unchanged.
Refresh the browser to see the expanded catalog. Other clients must synchronize their
source after both engines are upgraded. No data reset or reimport is required.

The two-instance stack uses a deterministic upstream: these are routing/UI/persistence
tests, not live paid OpenAI/Ollama or image-quality proof. Central monetary price remains
Unavailable in this fixture; price metadata parity is not a billed-cost claim. Simple
Chats on the JWT-protected Docker client requires an authenticated browser with its
read/manage/execute scopes; the test creates that token through Settings, never bypassing
authorization. Final browser runs use installed Chrome 151 because the bundled browser
failed interactive authentication despite correct scopes.

Current proof: [full-catalog-manifest.md](proof/full-catalog-manifest.md). Architecture,
semantic and desktop self-review: [full-catalog-review.md](reviews/full-catalog-review.md).

## Historical metadata-only repair

The following earlier results did not establish full source model-list parity and are
retained as history, not substituted for the current repair's evidence.

### Outcome and root causes

- Public catalog labels were fabricated, then discarded by the runtime mapper. UI controls
  consequently displayed opaque routing IDs. The catalog now publishes actual source model
  names; runtime/display adapters preserve them while request values remain collision-safe IDs.
- Schema 1.0 did not carry prices/private status. The importer hardcoded false/empty values,
  and both local normalization and the pricing component filled missing data with OpenAI
  transport defaults. Schema 1.1 carries exact typed metadata; source-managed profiles bypass
  local defaults and expose read-only prices/private state.
- Source private-provider edits were overwritten by stale configuration JSON during save.
  The save boundary now writes the edited typed pricing state before normalization. Both
  toggle directions have failing-first regression proof and UI persistence checks.
- Legacy cached snapshots now explicitly show an incompatible state without breaking source
  management. Resynchronization recovers them. A removed-model selector warning also survives
  ordinary component rerenders.

No new project/reference, generic framework, runtime partial, migration, or copied upstream
credential was introduced. The cohesive snapshot reader and pricing adapter remain in the
existing ProviderManagement boundary. All source/test changes remain uncommitted.

## Validation

| Final lane | Discovered / passed | Evidence |
|---|---:|---|
| Catalog, protocol, materialization, pricing/save, feature and snapshot unit tests | 161 / 161 | proof/transcripts/metadata-private-edit-final.txt |
| Agent/import/process/workflow downstream consumers | 217 / 217 | proof/transcripts/metadata-save-consumers.txt |
| PostgreSQL/HTTP catalog, source synchronization and runtime projection | 46 / 46 | proof/transcripts/metadata-integration-final.txt |
| Model/pricing/agent/shared-provider components | 38 / 38 | proof/transcripts/metadata-components-closure.txt |
| Two-instance real Chromium UI acceptance | 1 / 1, twice | proof/transcripts/metadata-ui-closure-2.txt; metadata-ui-closure-repeat.txt |

The 462 non-browser test executions are focused selections, not the full repository suite.
The final Docker image and browser-test project build succeeded. Final boundary checks,
production source assertions and anti-stub audit passed. Proof index: [manifest.md](proof/manifest.md).

Both UI runs configured source prices/private state, reloaded to verify persistence, synchronized
the client through UI, compared all nine pricing fields, and checked exact model labels.
OpenAI chat had two published models, then one after source removal/resync; its input rate
changed from 1.23 to 9.87 and private status from false to true. Ollama retained only its
published model/rates, with private status true and no imported OpenAI default rows.
All three imported profile IDs and default routing IDs remained unchanged.

Both runs completed shared-provider chat, approved image generation and attached-image analysis.
Each central run window contains eight successful invocations with complete token/image usage.
The repeat independently asserts eight upstream HTTP 200 responses, verifies a newly written
68-byte fixture PNG (signature and SHA-256), and finds no error/critical/unhandled-exception
log headings. Both health endpoints return HTTP 200 Healthy.

## Running stack and upgrade notes

- Source/admin UI: http://localhost:5210/agents?tab=providers
- Client UI: http://localhost:5212/agents?tab=providers
- Both engines: `candoitall-shared-providers-ui:spmeta-20260827-3`.
- Image ID: `sha256:184a105104f916334d143cf42bc627221ad1e997f1141503c9beff567ebe79d6`.
- Named data/secret volumes preserved. Previous containers are stopped rollback copies with
  suffixes `before-spmeta-20260827-1`, `-2`, and `-3`; no volumes deleted.
- Port 5032 and unrelated PostgreSQL were not changed.

Upgrade source and client together: catalog schema 1.1 intentionally rejects 1.0 because its
missing metadata cannot be guessed safely. On an upgraded client, use source Synchronize;
do not recreate imports or agents. Local connector configuration remains schema 1.0.
The test UI-issued JWT lasts 120 minutes; after expiry issue a new scoped token on the
source and update the client source secret through Settings > Secrets.

## Limitations and remaining boundaries

This uses the existing deterministic upstream fixture, not paid/live OpenAI or a live Ollama
model server. It proves real application UI, HTTP relay, persistence, attachments, image
artifact creation and usage recording, not model quality. The ledger reports pricing
completeness Unavailable and no billed amount; source price metadata mirroring is verified,
but this work does not claim central billed-cost computation.

The current client database retains existing local/seeded provider entries and user test
history. All executed test agents use the three imported providers. This is a preserved-data
upgrade/resync validation, not a newly wiped provider-empty database; original empty-client
setup proof remains in the earlier two-instance acceptance lane.

CodeAnalytics impact selection returned no result after more than 20 minutes and was cancelled;
Components MCP transport was unavailable. Scoped baseline analysis, exact changed-contract
review, existing components, focused tests and boundary guards were used. No full-graph or
analyzer-selected-suite claim is made.

The bundle workflow required failing-first proof and a visual evidence review. That review
caught and reopened the private-flag save defect despite preliminary green UI comparisons.
Those preliminary passes are chronology only, not final private-state proof.

Reopen on changes to catalog serialization/revisions, source ownership, reconciliation,
pricing defaults/save normalization, or model selection. Historical SB07's three-app gate
and downstream locks are unchanged.
