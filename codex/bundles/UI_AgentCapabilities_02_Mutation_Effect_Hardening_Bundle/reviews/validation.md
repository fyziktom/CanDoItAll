# Current validation decision

The bounded feature gates pass. This is not repository merge readiness.

| Gate | Current evidence |
|---|---|
| Unit | 30 discovered, 30 passed, 0 skipped |
| Components | 73 discovered/passed plus one distinct details-ownership case discovered/passed; 74 distinct, 0 skipped |
| Integration including API | 20 discovered/passed plus one real registered SQL/runtime revision case discovered/passed; 21 distinct, 0 skipped |
| Direct production builds | Core, Module, Web, lightweight UI and broad Components, Release --no-restore /m:1, all exit 0 |
| Browser | Full isolated 1600 x 1000 acceptance PASS; actual dialogs, file/database persistence and one real managed chat |
| Architecture | Scoped analyzer and manual dependency/owner review PASS; no new project edges |
| Portability | 14302 reviewed executable-source findings unchanged after regeneration; enforcement without --write-baseline PASS |
| Scanner self-tests | 6 portability and 4 secret scanner cases PASS |
| Complete proposed source secrets | All text files plus binary inventory; 245 baseline matches, 0 added, 0 removed; no suffix/size truncation |
| Retained artifact secrets | All retained text artifacts, decompressed gzip and UTF-16 included, 0 findings/errors; screenshots inventoried separately |
| Documentation | FAIL: same 118 historical tracked .log files, one grouped finding; no new finding. Historical proof is preserved as instructed; this child adds no .log files |

The counts describe exact execution, not a test-count architecture invariant. [Owning case index](../proof/tests/owning-case-index.json) identifies every passing case; [semantic map](../plan/required-topics.json) maps each requirement to its executed case. [Build commands](../proof/inventory/direct-builds.json), compressed transcripts, discovery and TRX remain manifest-covered. Negative evidence is retained separately from passing execution; historical predecessor evidence is not relabeled.

Portability review accepted only six added/four stale MCP source fingerprints in the two real dialog owners: an IsCurrent admission guard, a temporary result awaited with the owner token, and result assignment after the stale check. No process path/launcher/OS policy changed. The final baseline diff is limited to those fingerprints and metadata. Genuine defects were not hidden by disabling patterns or enforcement.

No whole-solution stable rerun was justified: no common method signature, schema, provider contract, project graph, routing or BaseLib component changed. The owning runtime tool, editor composition and API selections cover the changed legacy verification behavior. Direct affected builds were performed once at the final source checkpoint. Later fixture-only repairs did not edit production or permanent tests, so they did not invalidate those gates.

The inherited documentation gate is an explicit repository-level blocker, not missing feature proof and not a newly accepted unsafe behavior. Repairing historical bundle retention is separate work; no merge/history cleanup is authorized here. No claim that every repository gate is green is made.

Additional formatting check: whole-diff Git whitespace validation reports original CRLF proof bytes (the initial check found 9836 diagnostic lines, none outside proof). Those bytes are preserved. Automatic approval review rejected changing whitespace settings because it could weaken validation; no such setting was changed. This formatting result is reported separately from passing source/build/behavior/manifest gates.

[Retention map](../proof/retention-map.json) resolves original command-output names to compressed retained files with original-byte hashes. Raw execution records are preserved rather than rewritten to pretend compression was their original output format.
