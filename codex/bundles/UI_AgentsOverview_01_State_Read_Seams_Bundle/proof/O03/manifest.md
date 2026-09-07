# O03 governed proof manifest

Status: CLOSED; final byte seal verifies these artifacts. Owned requirements: OV08 and OV11-OV15, preserving OV01-OV10 through the final owning gate. Raw authorization: bundle://inputs/02-implementation-authorization.md. Semantic contract: [semantic-invariants.md](semantic-invariants.md).

This compatible bundle records the required roles without converting historical O01/O02 Behavioral proof to a different template. The final manifest.sha256 will hash all files in this proof directory except itself. The root execution MANIFEST.sha256 is separate; its original preparation version is archived here before refresh.

| Required role | Durable artifact |
|---|---|
| Entry, source state and authority | ../A0/committed-handoff.json, ../../inputs/02-implementation-authorization.md, raw/planned-edits.json |
| Source before/after, movement and production assertions | raw/moves.json, final/proposed-source-and-assertions.json, raw/evaluated-graph-final.json; final hygiene receipt when complete |
| Pure ownership and anti-stub review | architecture-review.md, verifier.md, final/proposed-source-and-assertions.json |
| Exact direct build commands/results | final/direct-builds.json with adjacent compressed per-project transcripts; final/stable-prerequisites.json when complete |
| Compiled discovery and exact expanded-name equality | final/owning-selections.json, final/focused-discovery-verification.json, final/focused-execution-overlap.json; tests/o03-final-* discovery/results/TRX |
| Meaningful earlier RED/GREEN | ../O00/adjudicated-cases.json and closure.md; ../O01/closure.md; ../O02/closure.md and raw expanded dialog/effect results |
| Actual O03 product RED and correction | browser-sandbox-r2 failed long-content image/DOM, responsive-reentry.md, final browser-sandbox-r3 results |
| Test/harness errors retained honestly | tests/ first ImmutableArray assertion failure; browser-sandbox first missing async wait; raw timing calibration decorative-SVG mismatch; none relabeled semantic RED |
| Production producer versus consumer | Registered PostgreSQL query integration tests plus fixture/receipt.json and immutable fixture/overview.json; independent public failure adapter Web/browser proof |
| Real browser / image review | browser-review.md, visual-inspection.json, browser-web-r2 and browser-sandbox-r3; later blank-line hygiene browser receipt separately |
| Same-source baseline and comparison | Original measurements/ retained as superseded; corrected-pre-owner/replay-receipt.json, pre-owner hashes/graph and full repeated measurements; final/measurement-runs.json and final/measurements/ |
| Statistics, classification and limitations | final/measurements.md, final/measurement-summary.json, measurement-reproduction.md |
| Source/asset/process restoration | final/measurement-restoration.json, final/measurement-final-source-check.json, corrected-pre-owner/replay-receipt.json, final/owned-ports-before-hygiene.json and per-run owned-exit ledgers |
| Static and source/retained secret proof | final/static-validation.json and compressed commands/scan files; final retained/source scan receipts, repeated after final retention |
| Named broad dependent-flow gate | One stable run for O02 additive shared navigation API plus O03 moved assembly/project graph; final stable summary/TRX retained |
| Current repository/sibling state | final/closure-repository-state.json and final/sibling-byte-verification.json, refreshed before closure |
| Verifier, prior manifest trust and progression | verifier.md; closure.md, manifest.sha256 and final link/manifest verification after all remaining gates complete |

## Production behavior artifact matrix

| Artifact / producer | Consumer | Lifetime and negative evidence |
|---|---|---|
| AgentsWorkspaceQuery independently composes header/overview/usage values from actual registered sources | Per-page session, persistent header/context and pure presentation mapper | O00 RED/O01 integration and state GREEN; route/scope/disposal and malformed/partial cases; no fake zero fact |
| Page-lifetime session accepts only current request data and immutable collections | Real page and controlled UI surface | Generation/reference/scope cancellation cases; same-scope stale versus initial failure; history host suppression |
| Surface typed intent | Page-owned navigation/HR/Defaults/usage dialog action | Real UI clicks, accepted-scope guard, one team navigation, owned-dialog cancellation, no unrelated CloseAll |
| Canonical isolated fixture rendering export | Full-app canonical query and sandbox immutable specimen | Registered exporter validates observations; browser actual charts/assets; no production provider/runtime registration in sandbox |
| Direct-watch durable edit and actual browser predicate | Raw first-visible/settled measurement ledgers | Corrected pre-owner replay, reverse restoration, document/process/update classification, failed attempts and outliers retained |

Broad success, its explicit deferred-theory/display adjudication, bounded hygiene and final static checks are complete. The final byte checker seals this proof before downstream work. Existing historical documentation debt remains visible and prevents a repository merge-readiness claim; it is not hidden by the manifest.
