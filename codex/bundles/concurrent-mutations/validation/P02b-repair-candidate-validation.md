# Publication, observer and documentation repair checkpoint

Status: reviewed repairs integrated; all five standard Release builds, final
portability enforcement and all **178 selected runtime cases** passed. The previous frozen
[361-case result](P02b-next-candidate-validation.md) remains 347 passed and 14 failed.

The source transfer contains only the reviewed Windows writer/recovery design and
eight new tests, H2 pooled-connection probe, B1 exact-backend wait observer, and
OpenAPI reference annotations/structural assertions. The document transformer has
one explicit registration before the existing description normalizer. C2/W1b and
remaining runtime work stay in the separate source-preparation checkout.

Direct affected production builds are Infrastructure and Web. Unit, Components and
Integration refresh their standard Release outputs before discovery. The complete
portability scan includes untracked source, reviewed deltas and final no-write
enforcement. No broad Stable or capacity result is inferred from these repairs.

Builds completed with no errors: Infrastructure 3.91 s, Web 70.39 s, Unit 43.23 s,
Components 37.05 s and Integration 39.26 s. Existing package/analyzer warnings remain.
Tool self-tests passed 6 +4 +7; the rendering register passed all 1,196 inputs.
The final complete scan covered 8,450 of 8,475 candidate files, skipped 25 large or
binary files and found 36,453 findings without truncation. The three added executable
findings are exactly the reviewed Windows branch, replacement delegate and staging
rename. The inspected baseline delta contains those three entries and its timestamp
only; final enforcement without `--write-baseline` passed with **15,457 allowances**.
Documentation validation passed for 426 maintained Markdown files.

Scan SHA-256: `fae579a08f7255d46f145854f9eb3986fee8994098c3178bc3d03c39b0a050b4`.
Baseline SHA-256: `de3ca2398fb920114be7e343651f72785900e66e82079e36ea77ef4ffb16522b`.

All 13 build-backed discoveries matched the source-derived **178 rows**. Execution
matched the same labels, test definitions and unique execution IDs with no failures
or skips; whole source and output identities remained unchanged across every lane.

Source identity: `c031ed38343f671cdbe13b3de729bda8fdaceb9e389624a3cd5b29fa767b5be7`.
Selection manifest: `e59322cec81634d63502f0aca54f884521a16db3cea055074c91504a894c4be5`.
Ignored proof root: `.artifacts/concurrent-mutations/validation/p02b-repair-validation`.
The per-lane result files retain runtime assembly identities, exact TRX hashes and
command records. `completed-summary-v2.json` corrects null aggregate totals in the
first summary; that reporting correction changes no lane or runner evidence.

The selected results are:

| Selection | Passed rows |
| --- | ---: |
| H2 current-write Unit / observer / PostgreSQL | 10 +8 +12 |
| H1 authority Unit / Components / HTTP | 45 +6 +3 |
| Session boundaries | 10 |
| API documentation / structural schema | 23 +3 |
| B1 exact-set basis capture Integration | 10 |
| Existing durable writer / new publication recovery | 17 +8 |
| Owner commit-knowledge regression | 23 |

The structural selection contains the same three Facts from the prior passing
49-row HTTP/schema selection; its other 46 unchanged HTTP rows retain their prior
proof. The general writer change justifies rerunning file authority, session and
owner outcome regressions. Unchanged C1's 16 rows, R1b's four rows and R1a's 91 rows
retain their previous candidate evidence; they are not added to this execution count.

New publication tests contain four Facts plus four fixed InlineData rows. Six rows
require actual Windows replacement execution; a conditional return elsewhere is
not affirmative Windows proof. The unchanged held-production-read-handle case
remains the real primitive regression. No test weakens the original reader sharing,
document coverage, rollback, deadline or current-authority assertions.

The owned PostgreSQL inventory after execution contained only the administration
database and the previously documented, policy-blocked residual lease with zero
clients. No new leased database remained. The original cleanup rejection and pending
signed SPA commit remain unresolved; neither is described as completed. The held-reader,
pooled connection-release, exact-backend wait and reference-description regressions
now pass. This checkpoint does not close the incomplete broad Stable run or prove
capacity, maximum payload costs, later owners or runtime policy activation.
