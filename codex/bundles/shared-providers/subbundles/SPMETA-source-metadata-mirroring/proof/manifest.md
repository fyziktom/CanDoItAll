# SPMETA governed proof manifest

Status: COMPLETE for this operator repair lane. Owned requirements:
META-NAMES, META-PRICES, META-PRIVATE, META-SETTINGS, META-E2E.
Raw notes: bundle://subbundles/SPMETA-source-metadata-mirroring/inputs.md.
Contract: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/semantic-invariants.md.
Compatible legacy layout maps the work-unit proof/ directory to the governed proof role.
Original root bundle/SB07 is NOT complete.

## Changed files and provenance

Baseline HEAD f092472ab83d36caf0e0fb52119d57d7aad35a65; initially clean worktree.
No commit/staging/discard operation. No skill files changed.
Before/after SHA-256 for every changed source, test and bundle file plus proof artifacts:
bundle://subbundles/SPMETA-source-metadata-mirroring/proof/changed-files.json.
Before means raw baseline Git blob bytes, not a reconstructed claim about checkout CRLF.
New files have before=null. After means actual worktree bytes. The hash manifest and its own
closure transcript are excluded from self-hashing; that exception is explicit in the JSON.

## Final focused lanes

Every .txt below has command, cwd, run label/start, discovery and exit status; the matching
.trx is retained alongside it and machine-checked by Validate-Closure.ps1.

| Lane / reason | Expected and actual discovery / result | Transcript |
|---|---|---|
| Public metadata, save boundary, private state, runtime profile | New expanded named selection inspected at discovery: 161; 161 passed | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-private-edit-final.txt |
| Changed Core save consumers: agent provisioning/import, process selection, workflow execution | Expanded named consumers inspected: 217; 217 passed. WorkflowExecutorTests substring also includes MemoryWorkflowExecutorTests | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-save-consumers.txt |
| Persisted catalog/sync/runtime HTTP integration | Expected 46, discovered 46, passed 46 | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-integration-final.txt |
| Pricing/model/agent/source/import/publication components | Expected 38, discovered 38, passed 38 | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-components-closure.txt |
| UI source setup/reload, import/resync, model names, chat/image/vision | Expected 1, discovered 1, passed 1 | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-ui-closure-2.txt |
| Repeat of same final UI contract | Expected 1, discovered 1, passed 1 | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-ui-closure-repeat.txt |

462 final non-browser executions; two final browser executions. Not a whole-repository suite.
Unit/component tests use Debug .NET 10; the application image is a fresh Release publish.
The only later test-code change was browser readiness; it did not invalidate application
image 3 or the non-browser tests.

Manual selection rationale: every changed producer/contract/materializer/mapper/selector/save
boundary is covered directly, then persisted source sync and selected dependent agent/process/
workflow paths. CodeAnalytics supplied no result after >20 minutes:
bundle://subbundles/SPMETA-source-metadata-mirroring/proof/impacted-tests-unavailable.json.
No analyzer-selected, negative-impact containment or complete graph claim is made.

## Required build and source gates

- Final Release image build: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/docker-closure-build.txt.
  Image candoitall-shared-providers-ui:spmeta-20260827-3;
  sha256:184a105104f916334d143cf42bc627221ad1e997f1141503c9beff567ebe79d6.
  Exported directly from successful Docker build history p2an2z4qx0ybsrhatbhsk7bny.
- Final browser-test build: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/playwright-closure-build.txt.
- Boundary script and diff check: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/architecture-closure.txt;
  bundle://subbundles/SPMETA-source-metadata-mirroring/proof/provider-boundary-closure.json.
- Production assertions / anti-stub: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/source-assertions.txt.
  Checks actual code, all 22 changed production files; no TODO/NotImplemented/fixture branches,
  no project change or new partial boundary. These checks supplement behavior tests.
- Scoped baseline: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/codeanalytics-baseline.json.
- Closure validator: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/Validate-Closure.ps1;
  bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/closure-validation.txt.

## Genuine failing-first and same-test passing evidence

| Defect / negative | Before production fix | Same tests pass in |
|---|---|---|
| Names fabricated; prices/private missing; metadata edits do not invalidate revision | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-failing-first-authorized.txt (3 failed) | metadata-private-edit-final.txt (same 3, plus contract regressions) |
| Removed shared model warning lost on rerender | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-removed-model-rerender.txt (1 failed) | metadata-components-closure.txt |
| Edited private flag overwritten by stale JSON, both directions | bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-private-edit-failing-first.txt (2 failed) | metadata-private-edit-final.txt |

The sandbox-denied metadata-failing-first.txt is NOT semantic proof. Preliminary compilation,
fixture registration and UI synchronization failures remain chronology, with their corrected
passing transcripts. Early image-2 UI passes are explicitly rejected for private-save proof.
The first image-3 run failed the secret-helper readiness race; final runs use the corrected
readiness check while retaining secret hash equality.

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Public model metadata / revisions | Real projector + canonical protocol tests in metadata-private-edit-final | Runtime mapper + component labels; UI exact comparisons | Source UI save, reload, sync; second model removed and prices/private changed | Three failing-first source metadata tests; invalid/missing fields rejected |
| Imported profile metadata | Production source sync/reconciliation via PostgreSQL/HTTP | Materializer, MAF profile, pricing/model UI, agent runtime | Stable profile/route IDs across upgrade and repeated sync | Legacy snapshot fails closed; empty prices stay empty; foreign defaults absent |
| Invocation/usage records | Production relay/audit driven by real UI requests | Read-only central ledger queries | Begin/completion persisted in each run window | No ledger rows are inserted as proof; eight complete successes asserted |
| Generated image / vision input | Real client tool, relay and deterministic upstream response | Workspace PNG and attachment-aware upstream request | PNG modified during repeat; approval/resumption and following chat complete | Signature/mtime assertions; request body has image content; no pre-seeded artifact claim |

Producer locations:
repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs,
repo://src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderCanonicalRevision.cs,
repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderProfileMapper.cs.
Other changed owners and exact hashes are in changed-files.json.

## Browser and runtime evidence

- Final screenshot directory: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/browser/metadata-ui-closure-2.
- Repeated final screenshots: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/browser/metadata-ui-closure-repeat.
- Reviewed open popup: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/browser/metadata-ui-closure-2/metadata-agent-models-open.png.
- Private/prices after resync: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/browser/metadata-ui-closure-repeat/metadata-resynced-client.png.
- Ollama without OpenAI defaults: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/browser/metadata-ui-closure-repeat/metadata-ollama-client.png.
- Runtime ledger/capture/health: bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-ui-closure-2-runtime.txt.
- Strong repeat assertions, PNG signature/mtime/hash, zero error headings:
  bundle://subbundles/SPMETA-source-metadata-mirroring/proof/transcripts/metadata-ui-closure-repeat-runtime.txt.

Both apps are on image 3, ports 5210/5212; named volumes and import IDs retained. Port 5032
unchanged. No upstream credentials or bodies are printed. Screenshot of issued JWT is redacted.
Ledger pricing is Unavailable: usage is proved, not computed billed cost. Deterministic
fixture PNG is 68 bytes; this is transport/artifact proof, not live image-generation quality.

## Reviews and closure boundary

Architecture: bundle://subbundles/SPMETA-source-metadata-mirroring/reviews/architecture-review.md.
Visual/composition: bundle://subbundles/SPMETA-source-metadata-mirroring/reviews/ui-review.md.
Adversarial verifier: bundle://subbundles/SPMETA-source-metadata-mirroring/reviews/semantic-review.md.
Execution/handoff: bundle://subbundles/SPMETA-source-metadata-mirroring/reviews/01-execution-report.md.
These are primary-agent reviews, not independent-agent verification.

Reopen on altered serialization/revisions, source ownership/sync, pricing normalization or
model selection. Original SB07, its three-app proof and its downstream locks are untouched.
