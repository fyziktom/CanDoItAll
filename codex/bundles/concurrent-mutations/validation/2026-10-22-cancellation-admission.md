# Finite root cancellation admission

All 61 selected cases pass: 48 Unit and 13 native PostgreSQL/HTTP/accounting cases.
Eight affected standard Release projects build successfully. Candidate verification
preserves 464 source pins and 54 owning/copied outputs through execution. The resource
observer covers all nine test commands and reconciliation finds no remaining observed
process, database-name delta or temporary fixture path. Full portability passes after
review of two HTTP-route heuristic matches.

## Admission contract

`ProcessRootCancellationAdmissionService` owns only selected-root admission. It reads
current progress, establishes one trusted actor and command context, and commits the
existing runtime barrier and recovery row atomically. At most three optimistic-conflict
attempts share that context. The five-second cooperative deadline includes reads, current
authorization and commit; cancellation awaits the actual owner and never detaches work.
An acknowledged commit returns immediately without observer, hierarchy, projection or
provider work. Repeated pending/completed requests observe the original barrier.

The Processes module derives availability from both validated recovery configuration and
worker registration for the host lane. Disabled recovery and suppressed MCP lanes refuse
before any runtime mutation. Application does not depend on module options or Web. The
existing runtime timestamp rule now also covers initial cancellation: a retry keeps its
original event timestamp while saved runtime time advances at PostgreSQL precision.

`POST /api/processes/runs/{runId}/cancellation` accepts an empty body and requires current
Process read and execute permissions. The actor comes from the authenticated subject;
explicit authorization-disabled local operation uses a distinct system actor. The host
rechecks the same subject, issuer, credential stamp and grants before each commit attempt.
These are request-scoped checks, not an atomic credential-revocation/SQL transaction.
No credential-file lock is acquired under a Process SQL lock. Accepted cleanup continues
independently of later caller access.

The response is `202 Accepted` for pending cleanup and `200 OK` only for verified
completion. `Location` identifies the existing authorized GET, and `Cache-Control` is
`no-store`. Missing, invalid, child, terminal, reconciliation, contention and unavailable
outcomes are explicit. A deadline or lost acknowledgement cannot prove rollback; the
client must observe the original selected root. No exception retries an uncertain write.
The root-only endpoint refuses children without resolving their parent or cancelling a
sibling. The existing `/cancel` API and native adapter retain their current behavior
until the jointly tested client cutover; no error-triggered fallback was introduced.

## Evidence

The 15 admission Unit cases exercise availability, child identity, existing progress,
terminal/missing runs, three-attempt exhaustion, original command/actor/time preservation,
authority loss, lost acknowledgement, cooperative deadlines, caller cancellation and a
known commit acknowledged alongside cancellation. Eleven registration cases verify the
existing options/lane contract plus all four availability combinations. The 22 existing
barrier/confirmation cases regress the shared runtime transition.

Two PostgreSQL cases force simultaneous original reads through independent contexts and
lose a real commit acknowledgement. Both requests converge on one barrier and exactly
one new receipt. An earlier caller clock does not move runtime time backwards. Recovery
after acknowledgement loss leaves the original state, queue and accounting bytes intact.

Four real HTTP cases require both grants, reject anonymous/partial grants and request
bodies, record the authenticated actor, recover the same barrier on replay, refuse both
disabled and suppressed-worker configurations, and preserve the selected child, root and
sibling. Three existing observation HTTP cases regress privacy and read authorization.

Two real HTTP/worker cases hold an Agent child's selected publication lock in StepSnapshot
and RunSnapshot modes. Admission returns before release, observation remains pending,
and the child journal is unchanged. After release, the registered worker reaches verified
completion, charges the original child work exactly once, releases reserved work and
does not repeat the provider call or produce a Project effect. Repeated completion adds
no receipt. Two existing completion cases regress lost acknowledgement during accounting.

## Review and retained diagnostics

Runtime, Application, Processes module and Web API snapshots add no dependency cycle or
project reference. The sole new informational finding is the eleven-value typed admission
outcome enum. Runtime adds one shared timestamp helper; Application adds the admission
owner/contracts; module registration supplies availability; Web supplies caller checks
and protocol mapping. Existing design-time module/Web diagnostics remain 123/180; direct
builds and actual host execution pass.

The first Integration build found two test import/name-resolution mistakes; the corrected
build passed before execution. The first discovery helper accidentally included a nested
fixture `Dispose` method; the corrected selection confirms all 61 current-source cases.
No native test failed. Original build/discovery records remain in the compact proof.

The full static scan includes untracked files: 10,024 files, 47,058 raw findings. Its two
added protected findings are the explicit POST route and Location URL matching `/proc`.
The inspected baseline delta is limited to those fingerprints, the count and timestamp.
Final enforcement without `--write-baseline` passes at 15,679 reviewed findings. The
6/4/7 portability/secret/rendering tool tests and rendering register pass.

## Remaining integration

This qualifies admission and its registered-worker continuation, not default activation.
Native/legacy client cutover, sustained/separate-process capacity, remaining owner and
policy waves, and final Stable/E2E remain open. SPA stays on its own branch for colleague
review. Earlier cleanup denials remain recorded and were not retried.

The [compact proof](../proof/2026-10-22-cancellation-admission.json) preserves command/log
hashes, exact source/output identities, test results, architecture, static review and
resource reconciliation. The existing isolated PostgreSQL 18 server was reused; ordinary
5032, unrelated resources and installed tools were preserved.

The local artifact scan has three reviewed PowerShell source-expression matches and zero
credential-sentinel matches, with no oversized or unreadable text exclusions. Automatic
approval review rejected removal of the new 31,972,751-byte static JSON with only
`blocked by policy`; the related 18,296,794-byte CSV was not attempted. Both remain in the
owned run directory with retained hashes and summaries. No alternate deletion was tried,
and whole-bundle cleanup is not claimed.

The exact compact delivery proof scans with zero findings; its SHA-256 is
`A3FED0E523DCB8F5D7B7DE120A1FA1886D62A6D03CF4DFF5168D76C6E6178F20`.
Documentation and whitespace checks pass. The only post-test pinned-source change is the
reviewed portability baseline refresh: an explicit bridge preserves all 463 other source
pins and 54 outputs, and retains the original tested manifest without repinning it.
