# Execution Report

## Status

- `Completed`. Execution is authorized by inputs/04-execution-request.md; the source baseline is committed HEAD 3d5def561.
- Phase 0, SB01/SB02/SB03 focused implementation gates, combined failure-persistence cases, scoped architecture reviews and candidate builds passed.
- The first broad gate remains failed. The root reviewer accepts startup-specific progression with the retained failures below.
- Both candidates run after controlled replacement. Paired performance and safe real-tool rows passed. The user explicitly authorized the protected-file workflow in inputs/05-file-validation-approval.md. Five source-file runs, fresh-read follow-ups, full history reload, native pending-handle preservation and one approved conversion now have actual evidence. Native rejection and reload also passed; root accepted final host preservation and behavioral closure.

The original preparation-only request was completed before this execution request. Preparation evidence in reviews/00-bundle-self-review.md is historical, not a claim that current execution is documentation-only.

## Outcome and scope

Improve agent preparation on 5032 and 5214 while preserving context, skills, actual tools, approvals, errors and durable history. Recommendation 4/log batching remains excluded. Per-stage commits, flushes, journal schemas and publisher 5210 are unchanged. No global cache or provider/configuration edits are part of this implementation.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
|---|---|---|---|---|---|
| SB01 | Pass: Phase 0 and characterization | Pass: focused foundation and integrated closure | Windows/Linux unit and storage integration; SB03 focused gate | Completed; SB03 and final integrated proof passed | proof/SB01/manifest.md, proof/SB01/semantic-invariants.md and independent-review.md; 27 Windows + 27 Linux unit and 31 integration cases passed |
| SB02 | Pass: Phase 0 and isolated provider baseline | Pass: focused implementation and integrated closure | Relational validation, failure precedence and separate boundary review | Completed; combined live validation passed | proof/SB02/manifest.md, proof/SB02/semantic-invariants.md and results-summary.json; 76 unit + 35 integration cases passed, no skips |
| SB03 | Pass: SB01 foundation; SB02 combined prerequisite satisfied | Pass: focused implementation, real UI and performance | Fresh conflict checks, recovery, projections, cancellation and combined terminal failures | Completed; root accepted bounded broad findings and integrated UI/performance | proof/SB03/manifest.md, proof/SB03/semantic-invariants.md and results-summary.json; 20 unit + 89 unique integration cases passed |

## Additional gate results

| Gate | State | Evidence and limits |
|---|---|---|
| Phase 0 live baseline | Pass | proof/phase-0: twelve real UI/provider runs, exact HTTP-send correlation, frozen settings and sanitized traces |
| Combined failures/activity | Pass | Both startup- and provider-origin terminal-persistence cases passed in focused and full Integration runs; proof/SB03/combined-failures and combined-failure-full-integration-results.json |
| Candidate builds | Pass | Native and Docker builds completed; first native MAX_PATH failure and successful short-root retry retained in proof/deployment/native-build |
| First frozen broad execution | Failed; retained | 9,731 passed, 15 failed and one skipped; exact discovery expansion and focused attribution recorded below |
| Startup-specific progression | Accepted by root reviewer | Focused startup proof passed; retained unrelated guards and non-reproduced preview failures do not prevent the local UI/performance phase; broad gate is not green |
| Controlled candidate deployment | Pass; portable recorded deployment review complete | Native environment and historical approval hashes preserved; client rollback container stopped and retained; publisher 5210 unchanged |
| Both-host real UI/tools | Pass | Source-file/follow-up/history, error, pending-handle, approve-once, reject-without-execution and rejected-history reload evidence; independent eleven-image visual review passed within its stated limits |
| Paired performance | Pass; see final checkpoint below | Five warm fresh sessions per host, separate continuation and first-after-replacement observation; unchanged Phase 0 HTTP diagnostic helper |
| Scoped architecture | Pass | proof/architecture/sb03-after/comparison.md: eight unchanged project edges, four existing cycles, no new findings; separate SB02 two-project review retained |
| Final closure | Pass | Root and independent reviews accepted actual UI/performance/host preservation; canonical completed-stage validation passed; proof/closure-preparation/completed-validator.log |

Focused commands, discovery and TRX records belong to each unit manifest. The scoped CodeAnalytics impact result promoted all supplied Unit/Integration/Components workspaces for the named Frozen Integration checkpoint. Each broad suite ran once. Later focused attribution does not replace or erase that first result.

## Frozen broad results and root adjudication

| Suite | Discovered | Runtime case results | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|---:|
| Unit | 7,187 | 7,221 | 7,221 | 7,219 | 2 | 0 |
| Components | 1,191 | 1,191 | 1,191 | 1,189 | 2 | 0 |
| Integration | 1,330 | 1,335 | 1,334 | 1,323 | 11 | 1 |
| Total | 9,708 | 9,747 | 9,746 | 9,731 | 15 | 1 |

The 39 extra runtime cases are deferred theory expansion: 34 Unit and five Integration. No discovered method was omitted. The live Ollama case was skipped because its opt-in is disabled; it is not counted as passed.

The two Unit failures are repository guards reproduced against unchanged committed HEAD: an outdated Docker COPY assertion and a naming guard that finds an existing SB09 literal in a Playwright test. Neither the guards nor the source they inspect were changed to force a pass.

Ten Integration failures occurred during the owned PostgreSQL test server's 1 GiB tmpfs exhaustion, beginning with a write-ahead-log PANIC. Only that disposable server was replaced, keeping its image, credentials, endpoint and CPU settings and increasing the approved capacity. The full affected ProviderHistoryQuery and ProviderHistoryCapture classes then passed 23/23, including the million-entry case.

The remaining Integration assertion repeated on the healthy server: it requires one literal index name, but the planner chose an efficient primary-key bitmap scan (0.119 ms on retry; initially 0.108 ms). SQL, indexes, planner settings and test source were not changed. This strict planner index-name guard remains unresolved; it is not recorded as passed.

Both Components workflow-preview cases passed 2/2 on the single authorized quiet focused retry with the same frozen binaries and original 30-second waits. Their initial failures remain in the broad result. The cause is not proven: application builds had already finished before those failures, and an initialization race is only a possible explanation. Root authorized older-output comparison only if either candidate case failed again; that condition was not met, so no older comparison ran.

The original two-second checkpoint-deadline case and both combined failure-persistence cases passed in the full Integration run. The failure fixtures prove production terminalization, durable logs, caller/activity propagation and reopening. They do not inject a real HTTP-adapter failure; live provider/tool success belongs to the separate UI validation.

All authorized follow-ups are complete: history 23/23 passed, strict index-name 0/1 passed, preview 2/2 passed. The 13 frozen source hashes and 15 binary hashes matched before follow-ups. No broad suite was repeated and no application host was changed by that validation.

The root reviewer accepts candidate local UI/performance validation for the startup changes. Three assertions remain: the two pre-existing Unit guards and the strict planner index-name guard. The non-reproduced preview failures retain an unproven cause. This bounded progression decision does not assert an all-green suite or waive real UI/performance acceptance. The earlier broad report's request for root adjudication is satisfied by this paragraph.

Authoritative results: proof/frozen-integration/final-broad-and-attribution-summary.json and broad-gate-result.md. First summaries, focused summaries, exact commands and individual cases are retained. Full original TRX/transcripts remain in the owned artifact directory; sanitized portable proof preserves the findings and links.

## SB01 review

Only DurableFileWriter.EnsureDirectoryTree reuses known case facts for existing descendants whose literal ordinal root prefix makes containment case-independent. Unknown facts, newly created directories and case-variant inputs retain acquisition. Per-segment safety checks and external-callback/precommit refresh remain.

Baseline 19 Windows + 19 Linux unit and 31 integration cases passed. The expanded old-code 27-case suite failed exactly three expected count assertions. Candidate 27 Windows + 27 Linux unit and 31 integration cases passed. Policy constructions changed from 8/20/32 at depths 0/6/12 to 8/8/8. Actual Windows insensitive case-variant paths retain extra acquisition; actual non-root Linux root replacement/recreation and Unix permissions passed.

The OS denied an attempted Windows case-mode toggle. No actual toggle is claimed. Independent review accepts the ordinal containment argument and complementary real-platform/controlled callback evidence for this narrow change. Stage observers prove order, not physical flush syscall counts. Logging, flushes, locks and public contracts are unchanged.

## SB03 characterization limits

The focused baseline 70-case run retains one deadline failure during concurrent SB02 work, followed by the exact unchanged isolated case passing. Candidate execution split 69 normal cases plus the same one-case quiet deadline; all passed. The original two-second per-pass budget was unchanged. The 19 new cases and 20 projection unit cases also passed.

The old-code read characterization failed exactly the two expected read-count cases before the optimization. All fresh conflict reads and complete recovery checks remain. SB03 trades five redundant raw comparison reads for typed comparisons on changed Prepared payloads; reduced reads alone are not a net-latency claim. These failing-first and scheduling caveats remain part of proof/SB03.

## Phase 0 live performance

Five fresh sessions and one continuation per host were sent through Playwright MCP UI at 1920×1080, serially, with real provider replies and persisted Completed state. No app rebuild, test run or simultaneous benchmark conversation occurred during sampling. The existing ordinary 5032 Release process and Docker 5214 client were retained.

Native used Spreadsheet Analyst/gpt-5.4-mini in QuotationPDFs Tests. Client used Portfolio Architect/gpt-5.6-luna in Calculator with its existing AutoApprove policy. Agent/provider/effort/capabilities/policy were not edited.

| Host | Warm fresh created→dispatch min / median / max | Continuation |
|---|---|---|
| Native 5032 | 11.696706 / 12.152916 / 14.716154 seconds | 8.467535 seconds |
| Docker 5214 | 30.922540 / 31.669973 / 32.703587 seconds | 30.878336 seconds |

Individual samples, submit/content/terminal timing, run/session IDs, build/configuration identities, clock alignment and sanitized traces are in proof/phase-0. The bounded external EventPipe helper captures actual HttpRequestOut.Start. Exact parent-span plus trace correlation identifies each run's dispatch; UI Run-stage timestamps do not substitute for it. Client dispatch is client-to-publisher, without changing publisher 5210.

First-after-start baseline was unavailable because neither existing host was restarted for sampling. A controlled first-after-replacement sample must be reported separately. Browser first assistant content appears after persistence in the existing UI, so first-content and terminal UI timestamps can coincide. Cross-host clock uncertainty is recorded separately; created-to-dispatch uses each server's own clock.

The initial method review in proof/SB03/performance/independent-comparator-review.md did not itself claim a candidate calculation. The subsequent completed result verification is recorded in proof/SB03/performance/independent-result-verification.json. The read-only evidence helper's single baseline self-check is isolated under proof/SB03/ui/helper-selfcheck and is not candidate UI proof.

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
|---|---|---|---|---|---|
| SB03 integrated/native | 5032 project route f28c07cd-982c-4d2d-bcf2-3e60a32eca72/structure | 1920×1080 desktop | Baseline six runs/twelve messages in proof/phase-0; safe and source-file/follow-up/history rows passed; native approval acceptance/rejection, pending-handle preservation and rejected-history reload proven | Baseline baseline5032.jpg inspected; safe and successful-file/history screenshots inspected; native pending/reopened/rejected approval inspected by root; independent eleven-image review passed with stated limits | Pass within recorded native busy-Stop and visual limits |
| SB03 integrated/client | 5214 project route e008de34-69eb-4fea-9b47-b4c23991b17d/structure | 1920×1080 desktop | Baseline six runs/twelve messages in proof/phase-0; safe and source-file/follow-up/history rows passed with existing AutoApprove and exact source-content hash matches | Baseline baseline5214.png inspected; safe and successful-file/history/active-close screenshots inspected; independent eleven-image review passed with stated limits | Pass with unchanged AutoApprove and actual catalog Running evidence |

Baseline composition: transcript, composer and catalog were readable; client file-browser styles and the human-readable model label were present. A pre-existing narrow chat-header badge overlap is recorded in Phase 0 proof; no UI redesign is in scope. Candidate review must inspect normal, progress/tool/history and relevant dialog/error views, scroll ownership, clipping and accessible actions. Counts, screenshots alone or an agent's unsupported tool-use claim cannot pass the matrix.

## Authorized source-file UI evidence

The explicit reply in inputs/05-file-validation-approval.md resolved the transmission blocker without changing source files, model configuration or approval policy. Actual structured tool calls/results now prove the following five completed runs; full payloads and credentials are not republished.

| Host | Session | Runs and actual source behavior |
|---|---|---|
| 5032 | 939dd101-0453-4ad4-aaf5-85cd7ae4d44f | 9fc77bd0-4cbb-404e-9389-88df9128212b read the quotation spreadsheet and performed one approved PDF-to-Markdown conversion. Follow-up 78436614-b3b7-4efc-9e0b-b5d66446a4ae freshly read Pricing A2:D2 and answered using verified source facts, including the 6,500 difference. |
| 5214 | d989aa84-831a-4e32-8960-f13b27dad864 | 6994341e-3336-47c0-8cab-5474b41904c2 read both approved calculator Markdown/SVG assets. abb9340f-4546-49a3-b8f3-9b07d662c3d4 freshly reread SVG; 2673d6a8-04c7-403c-ab07-018c1b8841aa freshly reread Markdown. Every returned content hash exactly matches its approved reference asset. |

Structured source identities, call/result pairs and receipt timing are in bundle://proof/SB03/ui/5032/files-structured-calls.json and bundle://proof/SB03/ui/5214/files-structured-calls.json. The native before-approval snapshot records WaitingOnTool and the pending conversion with no conversion execution receipt; the same run later completed with one Approved decision and one conversion receipt. Its derived Markdown output differs from all original source assets. The client retained AutoApprove and recorded zero approvals without changing policy.

Root's Playwright full reload/history verification retained native five messages and latest run 78436614-b3b7-4efc-9e0b-b5d66446a4ae, and client six messages and latest run 2673d6a8-04c7-403c-ab07-018c1b8841aa, with no duplicate messages or missing tool logs. Evidence: bundle://proof/SB03/ui/file-history-verified-rejection-start.mcp.json and the per-host files-two-run/files-three-run persisted records. Native pending-handle closure/reopen preserved the exact approval before one Approve action; client active close showed disabled Stop and Send, then Keep active/catalog Running followed by correct completed reopening. These handle actions are not cancellation.

The separate native rejection run 3908f2a2-35f6-4009-80f5-f2d69e619bcd retained the exact approval ficc_call_3Ut7txS4EYKYA65Ifg8JSDC7 as Rejected after one UI decision. Before and after snapshots contain zero execution receipts; the paired scalar result contains the rejection marker. The wrapper Invoking log is not treated as conversion execution. The run settled with three messages and 31 logs; full reload retained the same run, three messages, no pending approval control and the Approval rejected log. Evidence: bundle://proof/SB03/ui/5032/rejection-paired-result.json, rejection-before-persisted.json, rejection-after-persisted.json and file-rejection-reloaded.mcp.json. The initial reload Available-tab timeout remains in file-rejection-reload-first-attempt.mcp.json; a corrected observed catalog/history path succeeded without an application-defect claim.

Independent saved-image review inspected eleven actual JPEGs and passed within the stated visible-state limits: bundle://proof/SB03/ui/independent-visual-review-file-validation.md. Root separately inspected native pending/close/reopened and rejection views. This proof does not infer offscreen content, session identities or receipts from screenshots. Root accepted final host and behavioral closure after the final rejection/reload screenshot inspection. Canonical completed-stage validation passed; the final command and result are recorded below.

## Raw Note Closure

| Raw note | Status | Proof |
|---|---|---|
| N001 first three improvements | Solved | proof/SB01/manifest.md, proof/SB02/manifest.md and proof/SB03/manifest.md bind the three scoped diffs and focused tests; proof/SB03/performance/independent-result-verification.json proves both-host improvement |
| N002 exclude fourth; preserve failure rationale | Solved | proof/deployment/independent-verification/final-source-review.md and proof/SB03/combined-failures retain every awaited durable stage and explicit terminal errors; recommendation 4 remains excluded |
| N003 real UI 5032/5214 conversations/tools | Solved | proof/SB03/ui/validation-summary.md binds genuine UI01–UI06, actual source-file/error tools, fresh follow-up, full history and applicable approval accept/reject/pending-handle evidence on both named hosts |
| N004 preserve working pipeline/errors | Solved | bundle://proof/SB03/ui/validation-summary.md and bundle://proof/SB03/combined-failures prove working tools/context/history and explicit errors; proof/deployment/final-checkpoint.json confirms preserved originals, source, hosts and historical approval |
| N005 preparation only | Solved for original request | Preparation audit in reviews/00-bundle-self-review.md; later explicit inputs/04-execution-request.md authorizes implementation/testing |

## Deployment and remaining work

Controlled candidate deployment and paired performance are complete. Native retained its 127-entry environment fingerprint and exact historical paused approval files; the old client is stopped and retained and publisher 5210 is unchanged. The first native MAX_PATH build failure and successful approved short-root retry remain in proof/deployment/native-build. The sole collector docker.exe resolution fix, rejected BusyBox clock format and three accepted coreutils clock brackets are recorded in proof/deployment/diagnostic-capture/executable-resolution-review.json. Both unchanged collectors stopped cleanly without app signals after the fourteen serial samples.

Safe actual tool/error/continuation, limited busy-Stop and error-flow history/reload checks passed, as detailed in proof/SB03/ui/validation-summary.md. Authorized protected-file comparisons, source-backed follow-ups and successful-file history now have actual evidence on both hosts. Native pending-handle preservation and approval acceptance passed; the separate rejection outcome and full reload also passed, corroborated by a rejected paired result and zero execution receipts before/after. The final host checkpoint and root closure review passed. Canonical completed-stage validation passed; no broad suite or performance batch needs repeating on current evidence. The retained unrelated broad assertions and non-reproduced preview failures are not relabeled as passes.

## Final bounded checkpoint

Independent performance verification passed all fourteen candidate runs, exact raw HTTP-parent-span joins, serial intervals, stable configuration, four helper source files, eight helper binaries and clean collector stop without app signals. Native warm median improved 47.982%; client improved 29.115%. No repeat trigger or submit regression. Context digests include freshness metadata and vary per run; identical full-context bytes are not claimed. See bundle://proof/SB03/performance/independent-result-verification.json.

Actual UI01/UI05 and precisely limited busy-Stop UI06 passed. UI02/UI03 and successful-file UI04 now have the five real source-file runs and full history reload detailed above. Native pending-handle preservation and approval acceptance passed; native rejection and reload also passed. Root accepted final host preservation and behavioral closure. Root-inspected screenshots, four exact stat-call runs and failed locator attempts are in bundle://proof/SB03/ui/validation-summary.md; no missing-path substitute or indirect workaround bypassed the restriction.

Controlled replacement proof: bundle://proof/deployment/runtime-replacement/independent-review.json. Sole collector executable-resolution fix and rejected clock format: bundle://proof/deployment/diagnostic-capture/executable-resolution-review.json. The final checkpoint at 18:02:15Z records native 134 runs/133 terminal plus the exact historical paused approval and 31 preserved files; client 27/27 terminal. Both have zero pending journals and no owned requests remain in flight. All four original asset hashes/sizes and thirteen frozen source hashes match; candidate identities and publisher start are unchanged. See bundle://proof/deployment/final-checkpoint.json and bundle://proof/deployment/native-quiescence-final-checkpoint.json. Native HTTP health was not resampled during this offline checkpoint. The earlier 131/24 run checkpoint remains in proof/deployment/final-checkpoint-before-protected-file-ui.json.

The extra native generic run is outside the fourteen performance samples. Its Running DOM locator timed out; no Hide/Cancel actions or corresponding JPG followed. It is not additional Running or quality acceptance. Original broad failures remain retained. Execution is Completed after root accepted the actual source/approval/error/history, performance and final preservation proof. The permission blocker is resolved and validated; the canonical final gate passed. Minimal retrospective SB01/SB03 command/TRX presentations and invariant/hash indexes were added without rerunning tests or changing original evidence. The retained earlier validator failures describe the earlier unfinished checkpoint; they are not rewritten as passes. The final governed validator result is recorded separately.

## SB01 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/00-original-request.md, N001/N004; SB03 additionally owns integrated N003 proof.
- Shipped behavior: Existing exact-root descendant case-probe reuse preserves fresh safety, callback and durability boundaries.
- Source proof: bundle://proof/SB01/manifest.md and bundle://proof/SB01/changed-source-hashes.json bind frozen production owners and hashes.
- Test proof: bundle://proof/SB01/transcripts/passing.txt preserves exact original commands and TRX case results; bundle://proof/SB01/semantic-invariants.json maps concrete assertions.
- Shallow-pass trap: Global filesystem caches or skipped real Linux path/root tests could conceal unsafe reuse.
- Adversarial negative proof: bundle://proof/SB01/transcripts/failing-first.txt retains the real old-code work-count failures. Existing security/recovery negatives already passed and remain regression guards, not invented failures.
- Semantic positive proof: bundle://proof/SB01/transcripts/passing.txt and independent implementation review cover payload/security/recovery; bundle://proof/SB03/performance/independent-result-verification.json proves measured improvement.
- Anti-stub audit: No matching production TODO/unimplemented/fixture/template-only marker; bundle://proof/SB01/transcripts/anti-stub.txt preserves the actual bounded scan and expected no-match exit. Actual protected-file positive proof is now recorded; native rejection and reload also passed. Root accepted final host preservation and behavioral closure.

## SB03 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/00-original-request.md, N001/N004; SB03 additionally owns integrated N003 proof.
- Shipped behavior: Changed Prepared payloads avoid five raw comparisons only after fresh conflict reads; matching/recovered targets retain canonicalization and full recovery.
- Source proof: bundle://proof/SB03/manifest.md and bundle://proof/SB03/changed-source-hashes.json bind frozen production owners and hashes.
- Test proof: bundle://proof/SB03/transcripts/passing.txt preserves exact original commands and TRX case results; bundle://proof/SB03/semantic-invariants.json maps concrete assertions.
- Shallow-pass trap: Recovered JSON trusted as Prepared, omitted fresh conflicts or changed typed-matching canonicalization could conceal corruption.
- Adversarial negative proof: bundle://proof/SB03/transcripts/failing-first.txt retains the real old-code work-count failures. Existing security/recovery negatives already passed and remain regression guards, not invented failures.
- Semantic positive proof: bundle://proof/SB03/transcripts/passing.txt and independent implementation review cover payload/security/recovery; bundle://proof/SB03/performance/independent-result-verification.json proves measured improvement.
- Anti-stub audit: No matching production TODO/unimplemented/fixture/template-only marker; bundle://proof/SB03/transcripts/anti-stub.txt preserves the actual bounded scan and expected no-match exit. Actual protected-file positive proof is now recorded; native rejection and reload also passed. Root accepted final host preservation and behavioral closure.

## SB02 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; reduce avoidable provider revision work without weakening corrupt-source, relationship, cache generation or credential semantics.
- Shipped behavior: Selected shared revision loads use two relational commands and canonical validation without effective-profile/model copies; set loading retains three typed reads and original conversion failure behavior. Local mapping and composite revision values are preserved.
- Source proof: bundle://proof/SB02/manifest.md binds six changed-file SHA256 values and bundle://proof/SB02/source-equivalence.json retains revision/mapper/validator context equivalence; bundle://proof/SB02/architecture-review.md records the narrow internal assembly collaboration.
- Test proof: bundle://proof/SB02/transcripts/unit-passing.md records76/76 and bundle://proof/SB02/transcripts/integration-passing.md records35/35 with exact original metadata/TRX identities; bundle://proof/SB02/semantic-invariants.md defines SB02-I01 through SB02-I09.
- Shallow-pass trap: Returning unchanged profile/import/source tokens without validating current publication bytes could preserve a corrupt warm lease; fake-only query counts would not detect the concrete relational three-command path or typed source conversion precedence.
- Adversarial negative proof: bundle://proof/SB02/transcripts/query-failing-first.md records the original concrete query regression (expected2, actual3, exit1). Separate unchanged-token corruption, duplicate import/source conversion and local/unrelated-source negatives are passing preservation assertions in the original characterization and candidate35-case proof, not invented failing-first semantic results.
- Semantic positive proof: bundle://proof/SB02/transcripts/integration-passing.md verifies exact full-load revision parity and warm rejection behavior, while bundle://proof/SB02/transcripts/unit-passing.md covers canonical availability, disabled projections, generation/dispatch boundaries and omitted model-copy allocation.
- Anti-stub audit: No stubs, fake production loader or tokens-only cache were introduced; bundle://proof/SB02/transcripts/anti-stub-audit.log rechecks current frozen hashes, real validation/query branches and retained exact TRX assertions without rerunning tests. Full host/UI/performance acceptance remains in the root-owned integrated evidence.

## Final closure validation

Result: Pass for the approved startup-performance scope. Independent behavioral claim review: bundle://proof/SB03/ui/independent-file-acceptance-review.md. Final host/root acceptance retains all four original assets, thirteen frozen source hashes, candidate/publisher identities and the exact historical native approval. This is not an all-green broad-suite claim; the three retained assertions and non-reproduced preview failures remain in the broad-results section.

The unchanged canonical validator ran with profile feedback, stage completed and the repository root. It exited 0; the exact executed command is retained in the linked command metadata. Exact UTC bounds, validator hash and command are in bundle://proof/closure-preparation/completed-validator.command.json; output is bundle://proof/closure-preparation/completed-validator.log.

The first final attempt is retained as completed-validator-first-attempt.log with its command metadata. Its findings were manifest-only: Linux runtime command paths/options were interpreted as artifact references, and a sanitizer sentence accidentally implied a moved-checkout proof capability. Exact Linux commands remain unchanged in the original transcripts/metadata; the manifest now references those artifacts. The sanitizer statement now says published artifacts omit connection secrets, without claiming an unperformed relocation check. The validator and all underlying behavioral evidence were unchanged. The earlier blocked checkpoint's six unfinished-status findings also remain retained.

The final status-link verification also caught a duplicated machine-specific validator path in this report. That command was replaced here by its portable metadata citation; the exact original invocation remains in the command JSON. This failed documentation check is retained as completed-validator-final-link-attempt.log.
