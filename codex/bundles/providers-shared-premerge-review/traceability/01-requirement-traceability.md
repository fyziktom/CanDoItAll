# Requirement and input traceability

| Input / requirement | Finding / evidence | Owning units | Current proof | Closure |
| --- | --- | --- | --- | --- |
| N01 / R01,R09 | Original review and preserved manual merge/host constraints | SB01, SB09 | Final owning Integration179; browser1; frozen Stable9,424/9,424 with source-verified deferred theory expansion | Partially solved: final host/export/independent review remain |
| N02 / R01,R02,R05,R06 | SP-01..04; performance findings; DC02 | SB01, SB02, SB04, SB05, SB06 | Public SDK negatives, imported graph, orphan lifecycle, paired allocations, generated schema +28 validator cases | Solved for identified repairs |
| N03 / R03,R04 | H01/H02; retention/canonical ownership | SB03, SB04 | Decrypted synthetic capture, actual driver timeout, actual recorder retry expiry, bounded PostgreSQL cleanup | Solved for identified repairs |
| N04 / R05,R10 | Requested performance/C# skills | SB01–SB06, SB09 | Before/after workload report; nine direct builds; refreshed nine-project CodeAnalytics, architecture audit | Solved in implemented scope |
| N05 / R06,R07,R08,R09 | DC01–04; API/skill/schema inventory | SB06, SB07, SB08, SB09 | Docs197; skill sources4; two actual upgrade lanes; generated SQL; final export/install still blocked | Partially solved |
| N06 / R10 | Engineering instructions | Every unit | Typed minimal changes; no new project/runtime partial/XML; safe disclosure; no merge or authority reset | Solved in implemented scope |

| Finding group | Primary owner | Reopen dependency |
| --- | --- | --- |
| SP-01/SP-03/SP-04 | SB01 Completed | SB05/SB06/SB07/SB08/SB09 |
| SP-02 | SB02 Completed | SB05/SB06/SB07/SB08/SB09 |
| H01/H02 | SB03 Completed | SB07/SB09; SB06/SB08 if wire changes |
| Orphan input retention | SB04 Completed | SB05/SB07/SB08/SB09 |
| Cache/allowlist/response-copy work | SB05 Completed | SB06 if wire changes; SB07/SB09 |
| DC02 | SB06 Completed | SB07/SB08/SB09 |
| DC01 | SB07 Completed | SB08/SB09 |
| DC03 | SB08 Blocked on final capture/install | SB09 |
| DC04 | SB07 done; SB09 still gated | Historical three-application authority/proof remains unchanged |

Current source and evidence: bundle://reviews/01-execution-report.md and bundle://proof/SB09/manifest.md. The new execution request is retained in inputs/03-execution-request.md. Documentation and hash-only edits do not invalidate frozen product test proof; code/config/dependency changes reopen their owning gates.
