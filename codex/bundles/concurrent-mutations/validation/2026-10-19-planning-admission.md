# Planning policy admission

The initial preparation `c4-planning-admission-validation` was superseded before freeze
or test execution after test review corrected its new chat identity and refusal type.
The subsequent `c4-planning-admission-reviewed` candidate is rejected: 25 Unit passed,
but Integration deadlocked during fixture service construction before completing any
test. This record is not a product activation or bundle closure.

The Workbench participant prepares admission from the persisted Workspace v2 owner,
its actual dataset namespace and head revision, and a held selected-Agent catalog lease.
It requires the original current-schema Project Structure source, exact project lifetime,
profile generation/fingerprint and current source write authority. Missing or malformed
policy is explicit failure. Policy resolution preserves inheritance and the module's
budget/current-publication limits.

The prepared value binds the actual run/chat/authority supplied by the existing journal
without reacquiring a catalog lease or doing SQL inside that synchronous publication.
Task-basis capture and proposal preparation/restoration also require the policy's original
dataset namespace. No new project reference is needed: the module uses its existing
Workspace dependency; Core owns only the generic prepared value and journal binding.

The first implementation incorrectly depended on the Workspace UI owner. Managed stacks
showed its provider-administration factory returning through MAF runtime/tool composition
to planning admission. PostgreSQL was idle. The project graph was acyclic, but the
factory-composed service graph was not. The reviewed repair uses the existing
`WorkspaceSettingsWriter.ReadV2Async` persistence owner directly, retaining its profile
and namespace checks. A new registered-composition regression refuses construction of
the UI owner, so this dependency cannot silently return.

The rejected candidate's Release builds, discovery, rendering/docs/diff checks and full
portability enforcement passed (15,610 unchanged findings). Source/runtime identities
remained unchanged. Its process tree was stopped after 378.725 seconds; the observer
covered both commands and found no remaining observed process. The exact abandoned
database `cditall_primary_cdf76c3af0574dbd9ce299eb90002a76` and two fixture directories
are identified in the [retained proof](../proof/2026-10-19-planning-authority/c4-planning-admission-reviewed.json).
Automatic approval review rejected their cleanup with `blocked by policy`; the cleanup
command did not execute. They remain owned cleanup blockers, separate from the previously
blocked profiler directory. No replacement container is created. The freeze is released
for the narrow dependency repair; the same isolated PostgreSQL server is retained.

Fourteen new integration cases cover registered composition isolation, empty and legacy policy absence, actual provenance
and Agent override inheritance/ceiling, stale catalog preparation, revoked source writes,
unsupported publication policy, mismatched source kind/identity, changed profile, deleted
source lifetime, invalid persisted policy, and dataset mismatch before provider work or
proposal preparation. The candidate also selects the thirteen registered publication and
eight historical receipt regressions, plus twenty-five pure operation/codec cases.

Product execution-entry routing, provider-contract pinning, exact concurrent step-start
admission and native controls remain the next implementation boundary. Process/workflow
policies, remaining writer coverage, sustained capacity, broad stable validation and final
resource cleanup remain open as listed in the bundle closure ledger.

## Repaired owner boundary

`c4-planning-admission-owner-repair` passes all 60 selected cases: 25 Unit and
35 Integration, including the new composition regression. The real Workspace policy
and Agent override are captured and resolved; no UI/provider-administration dependency
is constructed. The thirteen publication and eight historical receipt cases still pass.

Direct Core, MAF and Workbench Release builds and both test builds passed. Discovery
confirmed 25/35. Full portability enforcement passed with 15,610 unchanged findings;
rendering, documentation and diff checks passed. Scoped architecture snapshot
`snap-20261019080309-881eb549` confirms the new direct persistence dependency and no
new cycle involving admission. The previous broader graph remains reference evidence;
actual factory composition is established by the runtime tests, not inferred from it.

Source identity `71C88BC88408FEE28A7A0603664E5AE99A005FABF55F22A1E048D295C620B9AB`
(9,223 files) and runtime identity
`3BC0579263DCBA5E3D2521317FBC7AC5590A01DE233BF29D57AAB997B37B61C1`
(6,436 files) remained unchanged. The observer covered the complete 3.655-second Unit
and 260.661-second Integration commands. Reconciliation found zero observed process
and zero database-name delta; ten unowned TEMP entries were preserved. The prior
blocked abandoned fixture is explicitly retained in this candidate's baseline, so this
does not claim full bundle cleanup. The freeze is released for the next implementation.
The [passing proof](../proof/2026-10-19-planning-authority/c4-planning-admission-owner-repair.json)
retains every selected result, command, identity and resource/dependency record.
