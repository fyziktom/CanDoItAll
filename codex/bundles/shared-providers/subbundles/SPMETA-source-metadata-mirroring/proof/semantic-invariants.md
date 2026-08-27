# SPMETA semantic invariants

Raw notes: ../inputs.md. Expected command transcript marker: SPMETA / META-*.

| ID | Expected production behavior | Disallowed shallow pass | Negative / positive proof |
|---|---|---|---|
| META-NAMES | Source model display names, unchanged collision-safe IDs | Rename ID to label or prettify hash | SourceMetadataNamesPreserveModelNamesAndDistinctRoutingIds; duplicate publication routes; selector value assertions |
| META-PRICES | Exact per-published-model standard/cache/long-context prices, no fabricated defaults | Show driver price table or zero substitutes | SourceMetadataPublishesPrivateFlagAndExactPricesWithoutDriverDefaults; empty-price component negative; full DTO round-trip |
| META-PRIVATE | Private flag travels from central to runtime and read-only client UI | Hardcode false or infer from OpenAI relay kind | Different private flags in catalog/projection; Ollama UI checked state |
| META-SETTINGS | Revision changes on metadata changes, source sync replaces stale metadata | Correct first import but stale subsequent edits | SourceMetadataPriceChangeInvalidatesRevisionWithoutChangingRoute; two-instance UI resync |
| META-E2E | UI-selected model routes to intended upstream and central usage records result | Screenshots or seeded records without execution | Real app UI chat/image/vision against deterministic upstream; production ledger queries and request captures |

## Production artifact matrix

Catalog metadata: producer SharedProviderCatalogProjector; consumer source sync and
SharedProviderRuntimeProfileMaterializer/SharedProviderProfileMapper; lifecycle catalog
revision/ETag invalidation and reconciliation refresh; negatives missing metadata, stale
revision, driver-default leakage, and wrong-label/right-ID versus right-label/wrong-ID.

Invocation records: producer SharedProviderRelayApplicationService and audit service;
consumer central usage UI/projection; lifecycle invocation begin/completion. Validate by
executing client UI actions, never inserting ledger rows as proof.

## Artifact-backed closure mapping

Every row below refers to the raw note in ../inputs.md and to exact before/after source hashes
in changed-files.json. Transcript labels are files in proof/transcripts/; full portable
references are in manifest.md. The command transcripts include all five META-* markers.

| Invariant | Failing-first / passing transcript | Production source and assertion | Adversarial case / dependent surface |
|---|---|---|---|
| META-NAMES | metadata-failing-first-authorized (SourceMetadataNamesPreserveModelNamesAndDistinctRoutingIds) / metadata-private-edit-final | CatalogProjection emits UpstreamModelId as DisplayName; mapper preserves ModelCatalog; selector uses labels with unchanged values | Duplicate source model names keep distinct routes; removed ID gets an explicit warning, including rerender (metadata-removed-model-rerender fails, metadata-components-closure passes); agent dropdown and chat route remain valid |
| META-PRICES | metadata-failing-first-authorized (SourceMetadataPublishesPrivateFlagAndExactPricesWithoutDriverDefaults) / metadata-private-edit-final | CatalogPrice and PriceMapper carry every rate; source-managed NormalizeImportedProfile and pricing editor skip defaults | Null is not zero; empty remains empty; negative/incomplete rates fail; Ollama UI has only the published model; source price-only defaults never become callable models |
| META-PRIVATE | metadata-private-edit-failing-first (Editor_private_flag_replaces_stale_configuration_and_survives_reload, both directions) / metadata-private-edit-final | CreateProfile writes edited pricing/private JSON before normalization; catalog, materializer and mapper carry source flag | Two equally wrong UI values are not proof: final UI reopens source and asserts the requested state before comparing client; 217 downstream save consumers pass |
| META-SETTINGS | metadata-failing-first-authorized (SourceMetadataPriceChangeInvalidatesRevisionWithoutChangingRoute) / metadata-private-edit-final | Canonical revision includes private flag and every price field; strict snapshot reader verifies revision and schema | Edited private/rate changes revision; removed secondary model disappears on UI resync; legacy snapshot becomes incompatible, not guessed; PostgreSQL sync/projection integration passes |
| META-E2E | Defect characterization above plus failed preliminary UI runs / metadata-ui-closure-2 and metadata-ui-closure-repeat | Production UI calls source services and client agent runtime; relay/audit creates ledger records; source-assertions scans all changed production | No directly inserted usage rows, no fixture branch in production, no credential copied to client; real approved image tool writes a new PNG and real vision request contains image data |

Source assertion transcript: transcripts/source-assertions.txt. Final runtime assertions:
transcripts/metadata-ui-closure-repeat-runtime.txt. The latter asserts 8 complete successful
central invocations, eight upstream HTTP 200 responses, a new PNG signature/mtime, two healthy
endpoints and zero error/critical/unhandled headings for the run.

Reviews: ../reviews/architecture-review.md, ../reviews/ui-review.md,
../reviews/semantic-review.md. All review claims are limited to this two-instance fixture lane.
