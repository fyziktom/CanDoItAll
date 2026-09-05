# SB07 governed integration manifest

Status: **Complete with documented follow-ups**. Final stable, browser, portability and artifact/source integrity gates passed. Owner-authorized implementation; baseline repo commit 68db2ee0e63a2ce6baa681e9722acc0a67877b21 on components-decoupling. No commits, staging or remote writes.

## Scope and source identity

See [execution-requirements.md](execution-requirements.md) for the complete per-ID disposition, including qualified requirements.

Owned input: careful UI component-edge refactoring without losing current functionality, with useful sandbox preparation and separate bookmarkability decisions. Scope is R-001–R-059, F01–F09 and B01–B30. Raw/normalized requirements remain in bundle://inputs and bundle://requirements; semantic adequacy is in [semantic-invariants.md](semantic-invariants.md), with production producer/consumer/lifetime and adversarial evidence.

[changed-files.json](changed-files.json) records baseline and current exact-byte SHA-256 for source/tests, the reviewed portability baseline and authored bundle changes, including absence at baseline for new files. Its canonical production/test/baseline records define the source patch SHA-256. [artifacts.json](artifacts.json) records all included phase proof files with purpose, current versus historical association, size, baseline presence and actual SHA-256. Integrity metadata and the verifier log are explicitly excluded from their own input hash graph; the root MANIFEST.sha256 authenticates those final outputs as well.

source-build-association.json and its transcript verify that all 40 changed source/test files predate the final solution build and that module, Web host, Components-test and Unit-test output copies contain the identical module assembly. Final verification checks these source hashes again.

Environment: Windows; .NET SDK 10.0.303; bUnit 2.7.2; Release validation. Live sibling Components c3e6aa03a878994c0ba8aed6af017d0be75f3796 and FileTools 7c7453c6583365ae5bd63f8fc6efc4a776e15818 remain clean. No physical project extraction or project-reference change.

## Commands and actual evidence

All shell commands run from repo:// (C:/repositories/CanDoItAll on this machine). Actual timestamp, invocation/run label and exit are in the cited logs. Discovery files preserve fully qualified methods/theory arguments, except verified synthetic credential fixture values replaced by redaction markers and their exact SHA-256 in proof-redaction.json. No zero-match selector is accepted. The verifier compares every discovered/executed method and its theory-case count.

| Gate | Command / exact selector source | Evidence |
|---|---|---|
| Focused Components | dotnet build tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --no-restore /m:1; dotnet test same project -c Release --no-build --no-restore --verbosity normal --filter exact union in discovery log | bundle://proof/SB06/transcripts/component-browser-corrected-build.log, component-browser-corrected-discovery.log and component-browser-corrected-results.log: **130 passed, 0 skipped**, exit 0 |
| Focused Unit | dotnet build tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-restore /m:1; dotnet test same project -c Release --no-build --no-restore --verbosity normal --filter exact union in unit-discovery.log | bundle://proof/SB06/transcripts/unit-build.log, unit-discovery.log and unit-results.log: **28 passed, 0 skipped**, exit 0 |
| Full solution | dotnet restore CanDoItAll.slnx; dotnet build CanDoItAll.slnx -c Release --no-restore /m:1 | transcripts/final-solution-restore.log and final-solution-build.log: exit 0 |
| Stable build/discovery | dotnet restore tests/Solutions/CanDoItAll.Tests.Stable.slnx; build -c Release --no-restore /m:1; test -c Release --no-build --no-restore --list-tests --filter stable filter /m:1 | transcripts/final-stable-restore.log, final-stable-build.log, final-stable-discovery.log and final-stable-expected-cases.txt: exit 0, **9,542 discovery entries frozen** |
| Stable execution | dotnet test tests/Solutions/CanDoItAll.Tests.Stable.slnx -c Release --no-build --no-restore --verbosity normal --filter stable filter /m:1 | transcripts/final-stable-results.log.gz: **9,597 passed, 0 failed, 0 skipped**, exit 0 |
| Portability | Complete scan including untracked proposed source; intentional reviewed baseline refresh once, then final enforcement without --write-baseline | portability-review.md; transcripts/portability-browser-corrected.log: **PASS, 14,251 reviewed entries**, exit 0. Complete 5,222-file / 28,670-raw-finding scan in portability-browser-corrected-scan.json.gz |
| Real browser | Actual managed Release DLL host and Playwright actions at 1600 x 1000 | browser-report.md, browser-final-actions.json, screenshots and transcripts/browser-final-console.log: representative flows pass, zero console errors/warnings, owned fixtures removed |
| Source anti-stub review | python proof/SB07/verify-proof.py audit (full bundle path from repo root) | transcripts/anti-stub-audit.log: actual production delegation/ownership, no stub/uninitialized/service-locator substitution; supplements real adapter tests |
| Document/artifact validation | python bundle://proof/SB07/verify-proof.py docs; existing repo artifact-secret scanner; freeze, verify, root-manifest, verify --root | transcripts/documentation-validation.log, artifact-secret-scan-final.json, transcripts/final-verifier.log and root MANIFEST.sha256 |

Seven unchanged MemberData theories expand at runtime: 9,542 discovery entries correspond to 9,597 executed cases. stable-theory-expansions.json records all seven methods, source members and row counts; the verifier requires every discovered method and exactly those source-backed expansions. This correction was reconciled after execution, not falsely represented as pre-run complete case discovery.

The full sanitized stable transcript is delivered losslessly as transcripts/final-stable-results.log.gz; its raw .log remains local and ignored. The final secret scan covered that complete raw text before compression, with no oversized text excluded.

The stable filter is Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true. This is the required stable aggregate, not an unfiltered claim covering external/live/quarantined environments. run-stable-gate.ps1 contains the exact ordered commands and fails on any nonzero exit.

[artifact-review.md](artifact-review.md) explains the verified synthetic fixture masking, original/delivered hashes and clean final text/provider-pattern checks. Historical scan findings are retained as historical evidence.

## Behavioral and architecture adequacy

[SB06 coverage map](../SB06/coverage-map.md) covers B01–B30 and connects the exact final case sets to preservation, new safeguards and the existing ambiguous/error behavior. [architecture-review.md](architecture-review.md) is a separate source/evidence review by the implementing agent, not a claim of another reviewer.

Meaningful failing-first evidence:
- bundle://proof/SB05/transcripts/lifetime-behavior-first-results.log: stale session publication and mutable submission; setup failures are excluded.
- bundle://proof/SB05/transcripts/target-echo-settled-results.log: acknowledged identity echo replaced EditContext.
- bundle://proof/SB05/transcripts/host-result-lifetime-results.log: stale old-target close result after Clear.
- bundle://proof/SB06/transcripts/catalog-refresh-first.log: Clear during awaited catalog refresh; ordinary completion control passed.
- bundle://proof/SB06/transcripts/page-save-echo-first.log and browser-initial-actions.json: real owning-page first-save echo opened a second editor.

All corrected cases occur in the final 130-case component gate. Actual workspace/EF/access/command adapters, real children and public host/page composition are exercised. Source-shape assertions or fake I/O alone do not establish acceptance.

## Reopen and invalidation record

SB03 was reopened for a post-await target guard and again after browser first-save page echo exposed duplicate presentation. SB04/SB05 lifetime and acknowledgement guards have their own failing-first proof. SB06 fixed a test teardown race by observing completion of the normally constructed real nested Simple Chat gateway; Simple Chat production behavior was unchanged.

The first stable run (9,541 then-current cases) was explicitly stopped and invalidated after that later production change; stable-results.log and stable-invalidated-processes.json retain the unsuccessful attempt. final-* transcripts are fresh final-source runs. The managed SourceRun shadow build's Windows path failure is separately documented; the already-built Release DLL supplied actual runtime proof. No build workaround was introduced into source.

## Readiness and limitations

[sandbox-navigation-handoff.md](sandbox-navigation-handoff.md) records six explicit dimensions. Semantic boundary, deterministic catalog rendering and interaction scenarios are proven at the documented scope. Lightweight project graph, standalone browser sandbox and production bookmarkability remain deferred.

The first small extraction candidate is the controlled catalog with real card/tree/assets; the editor's larger Memory/storage/provider/wizard graph is explicit. There is no measured warm watch speedup: the baseline host served the page but WatchReady contradicted readiness, so the three-repetition timing series remains unmeasured with a concrete next measurement protocol.

B12's existing blank editable core-load failure is characterized and remains unchanged because no separate repair approval arrived. The external AvatarPicker's own global toast lifetime remains a known child limitation; stale parent draft publication is guarded. These limitations are not concealed by a blanket claim that all defects were fixed.

