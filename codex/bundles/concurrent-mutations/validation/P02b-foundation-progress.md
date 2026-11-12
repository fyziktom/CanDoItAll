# P02b foundation validation progress

This is an interim development record, not closure of P02b or the whole engine rollout.

The import/HTTP/encoding candidate built directly in standard Release through
Infrastructure (3.40s), Projects (10.14s), Workbench (29.49s) and Web (53.99s),
with no build errors and existing package advisory warnings. First Integration
discovery found a missing SharedKernel import in the new package fault fixture;
after that test-only correction, build-backed discovery confirmed the expected
62 cases. Execution passed 58 and failed four, none skipped, in 2m18s under
`p02bc-final-repair`. This set includes
55 HTTP/body/epoch cases, six import commit cases and one actual MAF restoration
case. All HTTP and MAF cases and both row-import cases passed. The four package
cases incorrectly expected one physical artifact: the durable writer also owns
a persistent coordination lock. The corrected fixture requires the exact
payload/lock pair and separately proves payload cleanup or retention and valid
committed provenance. Fresh build-backed discovery and execution then passed all
four rows, none skipped, in 36 seconds (`p02bc-package-fixed`). The original
62-case failed result remains recorded. This set does not rerun or
replace the separate 89-case transfer baseline.

The offline command repair then discovered and passed exactly two LiveProcess
cases, none skipped, in 49 seconds (`p02bc-offline-repaired`). These are real
process/SQL/file bootstrap, resume, rollback and reactivation checks with an
already occupied Web endpoint. No matching activation child or nonadministrative
database session remained afterward; the shared fixture is retained for the next
lanes. The complete post-namespace-import portability scan covered 8,343 files
and 36,257 findings; no-write enforcement passed with 15,439 reviewed allowances.
The later package test correction requires regeneration before final closure.

All 18 affected production projects now have direct standard Release build
evidence. Eleven remaining owners passed under `p02bc-direct-*`; the final
Workbench brace-only correction passed its direct build in 21.35 seconds under
`p02bc-style-workbench-build`. Existing package advisory warnings remain.

The canonical-byte-length reuse candidate discovered and passed all 25 codec
cases, none skipped, in 81 ms (`p02bc-canonical-codec`), including exact escaped
and multibyte byte limits and unchanged canonical hashes. The 55-case HTTP set
above includes retained replay after a limit reduction and refusal of a new
oversized operation. The matched performance selection discovered and passed
both cases, none skipped, in 1m33s (`p02bc-canonical-performance`) with no other
test or database workload. The comparison report assesses allocation and timing;
these results do not establish retention-worker capacity.

Native page/editor discovery found the expected 62 cases. The first execution
passed 24 and failed 38, none skipped, in 3m41s (`p02bc-native`). All 38 failures
had the same unsupported `data-testid` parameter on the pending list's
`FormSection`. The repair moves that attribute onto the existing shared `Stack`
wrapper and updates its rendering review hash. Direct Projects.UI and Projects
builds passed in 2.07s and 7.42s, and refreshed discovery again found exactly 62.
The repaired execution passed 61 and failed one, none skipped, in 4m29s
(`p02bc-native-fixed`). The remaining card test exposed a source-established
readiness race: the pending inventory loads independently of the editor, but
the modal enabled save before that inventory was ready. The modal now accepts
the host's explicit readiness and guards both buttons and direct submission.
Four existing save tests await that affordance and event completion. Direct
Projects.UI and Projects builds passed again (2.29s / 8.16s). The extended
lightweight capture guard discovered and passed one case in 557ms; the exact
native repair selection discovered and passed five cases in 28s, none skipped
(`p02bc-readiness-light`, `p02bc-readiness-native`). The original failures remain
recorded; unchanged successes are not represented as a new 62-case all-pass run.

After that source repair, complete portability enforcement passed without a
baseline write (`p02bc-list-repair-portability`, 36,266 findings, 15,439 reviewed
executable-source allowances unchanged). Rendering review passed 1,195 inputs;
documentation validation passed all 424 maintained Markdown files. Later source
changes would invalidate these final-candidate checks.

The final browser host build passed directly through Web in 32.60s, with existing
advisory warnings. Browser discovery/execution is a separate 15-case lane:
13 store tests, one actual native restart recovery and one two-context conflict.
One new store case checks strict operation-key termination in the actual browser;
inspection confirmed the existing JavaScript patterns were already correct, so
the module was not changed for that case.

## Current repair checkpoint

The combined repaired Unit filter discovered and passed exactly 38 cases, none skipped,
in one second (`p02bc-repairs-unit`): proposal codec 12, governed binding 10, LLM Chat
composition four, transfer architecture three and browser pending-store adapter nine.
This closes the earlier governed read-revocation fixture failure and saved Process-step
identity codec failures. It does not replace the remaining actual governed MAF/HTTP tests.

Final transfer production edits built directly through Infrastructure, Workspace and Web
in standard Release (`p02bc-final-transfer-build-*`). Target session gates hand off to
transaction gates before effects. Acknowledged target results survive later cleanup
failure with an explicit warning; service preflight resources dispose before dispatch.
Existing package-advisory warnings remain. Integration discovery then found an incorrect
EF transaction interceptor signature in a new test; no test execution is claimed for
that failed discovery. The revised transfer source selection expects 89 cases.

Canonical `has-pending-model-changes` passed with no changes (`p02bc-model-parity`).
The generated predecessor-to-current idempotent SQL (`p02bc-lifecycle-idempotent.sql`)
was reviewed: four new tables, their checks/primary keys, one partial unique open-epoch
index and a migration-history insert, all guarded by migration identity and one outer
transaction. It contains no existing-table rewrite or destructive DDL. This command
generated SQL only; it did not apply it to an ordinary profile. EF tooling 10.0.3 reports
the existing runtime 10.0.4 version warning.

The complete portability scan covered 8,332 files and 36,236 findings. Enforcement found
one stale allowance from the removed broad offline-command runtime composition and no
added findings. The reviewed stale entry was removed; final enforcement without
`--write-baseline` passed with 15,436 accepted findings. Subsequent import and HTTP
source changes require a fresh complete scan and enforcement before closure.
Scanner support tests passed 6+4+7 cases, and rendering review passed 1,195 current inputs
after review of the shared scoped submission/reference store registration in Web.

Corrected build-backed boundary discovery found exactly 16 cases. Execution passed
15 and failed one, none skipped, in 1m21s (`p02bc-boundary-fixed`). All repaired HTTP,
Process, governed tool/metadata and fractional EXPLAIN parser cases passed. The actual
MAF case proved its acceptance and denied source read, then attempted SDK restoration
with a different read-only toolset. Compatibility correctly refused it. The fixture now
asserts that refusal and restores its original full policy before historical replay;
this same one-case correction remains to be rerun. Production checkpoint checks were
not relaxed. The frozen transfer binary discovered and passed exactly 89 cases,
none skipped, in 5m19s (`p02bc-transfer`). Package-import repairs discovered during
review are a later candidate and are not included in that binary.

The repeated offline executable lane discovered exactly two cases and failed both
before activation preparation (`p02bc-offline-cli`, 17 seconds). Static review found
seven Infrastructure storage registrations whose Security/Workbench dependencies are
absent from the deliberately narrow offline graph. Their explicit omission is being
implemented without disabling service validation or loading the full application.
The failed child processes and all their database leases were cleaned up.

The complete pre-optimization performance lane discovered and passed exactly two
cases, none skipped, in 1m27s (`p02bc-protected-performance`). It ran alone on the
isolated fixture and includes concurrent replacement samples that the earlier parser
failure prevented. Capacity and allocation findings are reviewed separately; a passing
harness alone does not establish the retention worker's operational capacity.

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
