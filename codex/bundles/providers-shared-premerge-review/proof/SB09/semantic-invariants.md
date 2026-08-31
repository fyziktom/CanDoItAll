# Semantic invariants

Owned raw inputs: N01–N06; requirements R01–R10. Current final closure is BLOCKED, not a claim of all gates passing.

| ID | Expected behavior and rejected shallow fix | Negative / passing evidence | Production assertions and downstream |
| --- | --- | --- | --- |
| INV-RELAY | error:null permits a completed buffered response; unsuccessful status and incomplete streams cannot appear successful. Reading optional diagnostics must preserve status/Retry-After. Internal outcome-only assertions are insufficient. | sb01-sdk-before.trx: 8 failures before HTTP abort; sb01-08-final.trx: pinned SDK, length/chunked errors, completion cases pass | SharedProviderHttpRelayClient maps/bounds diagnostics and validates body; SharedProviderInferenceApi aborts post-header failure; downstream SDK test consumes first delta |
| INV-NETWORK | The imported runtime allows policy-valid loopback HTTP but never gains arbitrary private/public HTTP authority | URI-policy unit and RuntimeProjection integration positives/negatives in final owning runs | Composition selector delegates to existing URI policy and hardened client |
| INV-CAPTURE | Quoted credentials redact before encryption; safe timeout cause survives sanitization; caller cancellation and observed terminal evidence stay distinct | sb03-unit-before.trx; sb03-boundary-before.trx (two shared timeout cases fail); sb01-08-final.trx persisted redaction/real driver timeout; sb01-04-unit-owning.trx | HistoryTextCapture → HistoryTextProtector; Providers safe boolean → MAF outcome translator; no raw exception retained |
| INV-RETENTION | Expired empty unreferenced input rows disappear in bounded batches, retained retries survive and deleted tombstones do not allow late input recapture | Existing late-retry regression caught naive deletion in sb01-04-owning-after.trx; refined + sb05-08-final.trx pass; actual recorder case in sb05-after.trx | Recorder freezes context/revision deadline; persistence checks it; maintenance runs bounded partition-locked cleanup; response deadlines stay per attempt |
| INV-CACHE | Repeated routing avoids full settings/model work but external profile/publication/secret changes invalidate selection | Before/after sb05 paired workloads; existing cache invalidation Unit; final catalog cross-scope tests | Cheap persisted stamp; miss stores loaded-row stamp; actual dispatch target validation remains |
| INV-CONTRACT | Five operations and typed wire scalars/enums; representative accepted/rejected requests agree with actual JSON Schema validator | sb06-schema.trx; sb01-08-final.trx; sb06-final-conformance.log: 28 Draft 2020-12 cases | Web-only schema transformer; no protocol framework dependency; final exported bytes still blocked |
| INV-UPGRADE | Exact-development existing records survive; reviewed-head sharing/history identities, ownership and supported transfer survive | sb05-08-final.trx: both lanes pass; sb01-08-final.trx broader migrations/persistence pass | Actual IMigrator, production projection/backfill/outbox, transfer guard; no fictional feature tables seeded at development |
| INV-UI | History/content/manage remain explicit; preview cancellation does not shorten existing retention | sb09-ui-complete.trx; seven inspected screenshots; ui-review.md | Real UI/application/persistence with explicit visual fixture; not provider-producer proof |
| INV-ARCH | Retained boundaries and executable production behavior; no fixture branches, hidden fallback or fake decomposition | sb09-source-audit.log, sb09-codeanalytics.json, csharp-architecture-gate.md | Changed-file hashes in changed-files.json; no project/reference changes |

All referenced TRX paths resolve beneath bundle://reviews/test-results. The manifest and artifact index identify current source/artifact hashes. Failing-first evidence is explicitly distinguished from setup failures; no retrospective test failure is invented.

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Attempt and terminal metadata | Real recorder/driver in capture integration | Authorized read/persistence assertions, UI supporting view | Actual completion, timeout/cancel and terminal preservation tests | Sanitized timeout baseline; quoted credential failure baseline |
| Shared input deadline | Actual recorder BeginAsync and same typed context/revision | DetailStore/Protector enforce deadline | Actual_recorder_keeps_input_expiry_after_orphan_cleanup_but_allows_new_revision | Naive tombstone deletion regression failed before deadline refinement |
| Canonical source evidence | Development rehearsal creates original source records and invokes actual source/backfill producers | Production outbox processor and index read assertions | Locator/journal acknowledgement and canonical file hash preservation | No copied standalone detail; transfer with sharing explicitly rejects unsafe transfer |
| Catalog stamp | Current persisted publication/profile/secret rows | Production catalog query and dispatch route | Cross-scope mutation and secret deletion | Cache never masks deletion or retains old revision |
| UI fixture rows | Deliberately seeded visual data only | Real authorized history UI | Paging/details/policy/cancel | Explicitly excluded from production producer claim |
