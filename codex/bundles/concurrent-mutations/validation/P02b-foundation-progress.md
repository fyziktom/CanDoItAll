# P02b foundation validation progress

This is an interim development record, not closure of P02b or the whole engine rollout.

The frozen operation contract/identity/normalized-intent/canonical codec slice built
successfully through the Projects production project in Release. Build-backed Unit
discovery found exactly 41 expected cases for
`FullyQualifiedName~ProjectDefinitionOperationContractTests|FullyQualifiedName~ProjectDefinitionOperationCodecTests`.
All 41 passed, none skipped, in 101 ms of test execution. Raw discovery, test log and
final owning-assembly TRX are under `.artifacts/concurrent-mutations/validation` with
the `p02b-contract-codec` prefix. The Unit solution also reported no matching tests in
three unrelated assemblies; those are not claimed as tested.

The initial epoch/lifecycle source subsequently compiled through Projects. The first
canonical migration build exposed one in-progress governed adapter property name;
it was corrected from a nonexistent property to `SourceProjectAdmission`. The failed
build log is retained. No database migration or epoch/receipt correctness proof is
claimed by these compilation results.

Further operation-protocol/writer, native recovery, lifecycle, epoch and governed
adapter edits invalidate the earlier builds as final integration evidence. Their
focused tests, canonical migration/model parity, retained-capacity benchmark and
mandatory final portability enforcement are still required. Broad Stable runs at
the frozen integrated P02 foundation checkpoint as recorded in the execution plan.

The next combined Unit lane discovered 75 cases and passed 65, failed 10. Nine
activation-file failures exposed missing-file handling through the safe-path API;
that implementation and a leaked test lease were repaired. The remaining case used
an unintended broad grant in its fixture. The following build-backed discovery found
47 cases (activation files 9, offline CLI parser 13, governed bindings 10, tool proposal
codec 8, MAF source restoration 7). Execution passed 46, failed the same read-revocation
case at its later assertion. Its fixture was then corrected to remove actual persisted
project grants: the access normalizer intentionally makes CanRead true when grants
remain. This repaired case still requires execution. All activation and offline parser
cases passed. Logs use `p02b-activation-cutover-unit`; no skipped cases.

The lightweight pending-operation session lane discovered and passed all 13 cases
(`p02c-session`). It is adapter evidence, not native page/browser integration proof.

The integrated production graph compiled through Web during Unit/Integration discovery.
The first direct Web attempt found two governed adapter contract mismatches (required
externalIdentity argument and flat acknowledgement fields); both were corrected.
The first Integration filter accidentally omitted the two governed operation methods;
the corrected no-build discovery against those freshly compiled outputs found exactly
42 cases. Execution passed 41, failed one, with no skips, in 2m27s. All 25 lifecycle,
epoch, projection and activation-coordinator cases passed. Sixteen protected operation
cases passed, including actual commit acknowledgement loss followed by provider restart,
small-pool receipt recovery, historical replay and real expired Process authority.

The failed Serializable duplicate test timed out at its rendezvous: the actual source
authority serialized calls before the intended owner-snapshot race. The corrected test
explicitly replaces that source lease with an independently acquired fixture seam while
preserving real Process-derived caller/effect/read policy and real Serializable owner
transactions. The separate actual expired Process authority proof remains unchanged.
The renamed test requires fresh discovery and execution; it is not yet passing evidence.
Logs/TRX use `p02b-owner-lifecycle-integration`.

These PostgreSQL tests exercised the current canonical schema. Final model parity,
offline CLI process execution, transfer races, native and HTTP integration, retained
capacity and final portability enforcement remain outstanding. Ongoing adapter edits
require fresh production builds before the integrated P02 checkpoint.

The native integration candidate built directly through Projects.UI (1.67s, no warnings)
and Web (38.56s, existing NuGet advisory warnings). The combined pending/editor session
filter discovered exactly 20 cases and passed all 20, none skipped, in 83ms. The separate
capture-validation filter discovered and passed one case in 488ms, proving the host can
reject input before the editor clones its collections. Logs use `p02c-editor-session`
and `p02c-capture-bounds`. These are lightweight state/component proofs, not the native
PostgreSQL host or browser proof.

Independent review then found a duplicate-operation admission race: its initial missing
receipt observation can precede another attempt's commit, while its owner admission
arrives after epoch closure. The writer must reconcile after that refusal and preserve
uncertainty if the fresh lookup fails. The same distinction applies to a generic failure
before this attempt commits: it does not establish absence of an earlier acceptance at
the same operation address. Repairs and deterministic cases are being integrated; this
reopens the production writer build and focused operation tests. Native known seed
completion after editor retirement is also being tightened before host-level execution.

The 14 HTTP body-boundary cases were moved into the existing Integration friend assembly
after their initial Unit compilation failed on internal helper access. No production
visibility or new friend declaration was added merely to accommodate those tests.

After the common recovery repair and native retired-seed cleanup, direct Projects and
Web Release builds passed in 5.71s and 38.85s. Initial Integration discovery found two
missing test namespace imports; the corrected build-backed discovery found exactly
64 cases. Execution passed 56, failed eight, none skipped, in 2m47s (`p02b-boundary`).
All 24 protected owner/Process recovery cases passed, including the corrected
Serializable source-lease seam, replay-horizon corruption check and all six new
admission/lookup recovery cases. HTTP/body cases passed 25/28; three failed test
expectations were corrected to accept the JsonReaderException subtype and the actual
401 authentication rejection after registry scopes invalidate a credential. Those
three repairs are not yet rerun.

The governed subset passed seven and failed five. Diagnosis found update-tool filtering
before retained journal recovery, lost Process step identity on saved-source JSON
roundtrip, and a MAF test that paused for approval before its injected completion
failure. The first two require production repairs; the last needs an explicitly
approved fixture. Current read/write/source checks must remain enforced. These repairs
reopen the owning build and focused governed proof; the lane is not green.

The unchanged transfer filter discovered exactly 77 cases against that freshly built
assembly and is now executing. Its owner imports, target exclusions, source snapshots
and retained-receipt checks remain a separate result, not inferred from writer tests.

The transfer lane completed with 75 passed, two failed, none skipped, in 4m50s
(`p02b-transfer`). One assertion still expected six Projects tables instead of nine.
The other exposed a real ordering defect: lifecycle row validation established the
Serializable target snapshot before later target-table lock waits. An unprotected
SharedProviderSource writer could commit during that wait and remain invisible to the
final reference guard, causing a later foreign-key refusal instead of the intended
pre-effect guard result. The repair preserves Serializable and introduces an explicit
complete target lock plan before any snapshot-producing target SQL. No passing transfer
closure is claimed until that repair and its regressions pass.

The frozen pre-optimization Projects writer performance harness discovered exactly two
cases in the same built Integration assembly. The fixture had no other nonadministrative
database sessions before execution. This serialized lane measures the previously built
writer/receipt/search path while independent governed and transfer source repairs are
prepared; it is a baseline measurement, not a claim that those later edits are built.

That performance run passed the create/replay/lookup/cleanup case and failed the
replacement case after all 1,200 uncontended measured writes had been accepted. The
failure is an EXPLAIN diagnostic parser expecting integer Actual Rows from PostgreSQL
18, which emits a decimal average. No concurrent replacement sample ran. The parser
repair and one pure regression are prepared; see P02b-protected-performance-analysis.md
for bounded extracted evidence, allocation/latency costs and capacity limitations.
No production optimization or complete performance closure is claimed.

The separate offline executable filter discovered exactly two LiveProcess cases from
the existing built assembly. Both failed before a Prepared activation record existed
(`p02b-offline-cli`, 17 seconds). Review found the command unnecessarily registered
the whole runtime graph without its Web UI dependencies. Its repair retains Web
configuration precedence but composes only Infrastructure, the activation coordinator
and validated owner options, resolves the explicitly requested profile directly, and
never starts the host. The tests now report only allowlisted exception/service type
diagnostics. The command and tests require fresh build/discovery/execution. After the
failed lane, the owned PostgreSQL fixture contained no leased databases and no offline
activation child process remained. The fixture container is retained for the next lanes.
