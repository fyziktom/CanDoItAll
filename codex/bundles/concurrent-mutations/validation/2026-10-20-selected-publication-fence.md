# Selected Process source publication fence

Status: implemented and validated on `concurrent-mutations`, after `ad8793500`.
This checkpoint does not activate inherited Workbench effects or Process/Workflow producers.
SPA remains clean on `codex/concurrent-mutations`; no merge or push was performed.

## Change and boundary review

The [design](../plan/P07-selected-publication-fence.md) now has a narrow Application query
port and an explicit selected-source entry on the existing publication fence. The port
resolves the same scoped persistence query. Workbench gains no Persistence or Process
product-module reference.

The selected fence requires the effect owner's PostgreSQL ReadCommitted transaction after
all Project gates. It takes the Process root lock, validates the child/generation/allocation,
then share-locks the source runtime and assignment. The existing legacy guard uses the
same extracted row-lock helper without changing its admission rules. Assignment repair
does not take a root advisory lock, so these row locks must last through effect commit.

The fence revalidates canonical material and registered contracts, compares the complete
original source except its observation time/current flag, and checks claim time after
awaited reads. A copied fingerprint cannot cover changed permission, scope, capability,
executor or claim fields. Actual saved source authority is mandatory. Dispatch requires
the original live claim; manager review requires the existing waiting phase, an open
retained child and no active/live claim. Review does not revive dispatch or settle work.

Current Agent/operator rights, original Project/profile/namespace and finite-operation
protections remain effect-owner responsibilities. The selected query is Agent-specific;
it does not enable Workflow dispatch. No schema, ProjectReference, cache or lock registry
was added. The extra bounded reads/locks still need measurement in the actual publisher;
this correctness lane does not qualify sustained capacity. Root semaphore retention and
the historical coarse catalog-lease responsiveness failure remain open.

Architecture snapshots `snap-20261020050826-de5e7b25` and
`snap-20261020051805-de5e7b25` cover six projects. Types change 2,579 to 2,581 and members
21,833 to 21,839. Findings remain 822, with no new findings. One existing composition-file
size finding changes by one line. Project cycles remain zero; the existing one module
cycle and six type cycles are unchanged. The analyzer's 208 existing diagnostics and
blocking-errors flag are unchanged; all affected direct builds pass. The new scoped query
alias is verified by composed execution, although the analyzer registration count remains 145.

## Exact validation and repair bridge

Standard Release builds passed for Process Application, Persistence, ModuleProcesses and
the Integration project. No custom configuration or output directory was introduced.

Initial filter, confirmed discovery **126**:

```text
FullyQualifiedName~ProcessDecisionSelectedPublicationTests|FullyQualifiedName~ProcessDecisionDispatchAuthorityTests|FullyQualifiedName~ProcessDecisionPublicationFenceTests|FullyQualifiedName~ProcessExecutionAuthorityPersistenceTests
```

All 126 executed: **124 passed, 2 failed, 0 unexecuted**. Both failures were the new
independent-root fixture inheriting the singleton coordinator's ambient transaction.
The actual source substitution, canonical material/contract, source absence, manager
review, source-row repair and expiry cases passed. Both failed results remain retained.

The repair suppresses execution-context flow only when starting the independent concurrent
request. Actual SQL lock-wait observation and commit/rollback assertions remain. The exact
repair filter `FullyQualifiedName~ProcessDecisionSelectedPublicationTests` discovered and
passed **23/23** cases. This repeats 21 initial passes and corrects both failures, yielding
**126 distinct passing cases** across the bridge. Only that test source and Integration
DLL/PDB changed; every production source and runtime input is unchanged. A failed initial
repair-build wrapper invocation (PowerShell flattened a one-entry array) is retained;
the corrected explicit Integration build passed before discovery or execution.

The full portability scan ran for both candidates, including new untracked source files.
Final enforcement without baseline writing passed with **15,642 unchanged reviewed
executable-source findings**, zero added/stale findings and no baseline edit. Baseline and
secret-scanner tests, rendering tests/register, documentation and diff checks passed.
The broad Stable suite was not rerun; its historical failures/unexecuted cases remain open.

## Frozen identities and observation

Both roots are under `.artifacts/concurrent-mutations/validation/` and are Released.

| Candidate | Source inputs | Runtime files | Test seconds | Observer seconds |
| --- | ---: | ---: | ---: | ---: |
| `c4-selected-publication-fence` | 9,587 | 2,504 | 491.8624606 | 511.0892193 |
| `c4-selected-publication-fence-final` | 9,587 | 2,504 | 99.2846756 | 119.8416078 |

Initial source manifest SHA-256:
`CA85059410C6A2A4DE8CF4A09B586E5F94A3EDF3F9811674F567E8E1FF533D00`.
Initial runtime manifest: `DBF22A7F896845A3819989CAEE9BEE7D053E4B129B4E158C9022E8BCE298ADBF`.
Repair source manifest: `82D25AF64F23BAD4CE64A2136520BA031814652F08EC0588AAA8134763DDF9C1`.
Repair runtime manifest: `8626823FA1429C8A984E4C36EC09E6A574FE90E904817E68889162CBB36DB3CC`.

The exact observer PID/start identity was confirmed before each execution. Observers cover
the entire test commands. Complete source/runtime identities verified before release.
Both runs have zero remaining observed processes and zero database-name delta. Seven
initial and four repair unattributed TEMP entries are preserved. The existing isolated
PostgreSQL 18 fixture on loopback 53619 remains required; ordinary 5032 and installed tools
are preserved. Historical cleanup denials remain open; this lane is not global cleanup closure.

Retained UTF-8 LF proof (working-tree and Git byte form match):

- [Initial proof](../proof/2026-10-19-planning-authority/c4-selected-publication-fence.json),
  SHA-256 `5E4F6AFC201E51A663BAB23CA967F5B18AD51334031B55025225683B5A07F259`.
- [Repair proof](../proof/2026-10-19-planning-authority/c4-selected-publication-fence-final.json),
  SHA-256 `204B57B2F21E1318AF2D019C19A69A8C6EEB3F45604B83919BCA22E28E6D503C`.

## Next integration

Add the explicit enlisted publisher hook after Project gates, then the inherited target,
plain shared basis, current held-source authorization and participant. Core/MAF need a
separate parent-authorized saved-effect continuation and real inherited context while
generic/interactive APIs stay closed. Preserve unique provider/receipt ownership and
uncertain-result recovery. Wire actual producer admission and atomic parent outcomes,
then manager rights, Workflow, settings and end-to-end/capacity closure. R5/R6 remain open.
