# Agent policy historical receipt checkpoint

The backend portion of [the recovery design](../plan/P07-agent-policy-recovery.md)
passes **40/40 focused cases: 16 Unit and 24 Integration, no skips**. Policy and its
historical receipt now commit in the same atomic catalog replacement. Lookup uses the
complete original submission and never invokes PUT or a provider. Retained native
browser submissions remain the next increment; this checkpoint does not claim native
restart recovery or full R5/E2E closure.

## Behavior and limits

Models owns the immutable receipt/log and strict bounded codecs. The log retains at
most 1,024 records for 30 days, with 4 KiB per receipt. It never evicts live evidence.
Dedicated policy publication prunes expired records; ordinary catalog, full-document,
package-shaped and deletion writers preserve authoritative receipts. Legacy omitted
logs stay omitted. Explicit null, malformed, duplicate, unknown and out-of-bound records
are errors, not missing evidence. Current policy equality never proves an old save.

Core checks the current context before and after reading. A newly active generation
of the same profile/scope may read historical acceptance; it cannot dispatch the old
command. Different profile/scope, altered original intent, missing/expired evidence and
failed reads have separate outcomes. The HTTP POST lookup explicitly requires Agent
read permission, bounds the original submission to 8 KiB, preserves exact revision
strings and disables caching. PUT remains a single CAS attempt.

The extracted `AgentPolicyPublication` holds preparation/acknowledgement state only.
The store retains real lock, witness and file-write ownership. Its internal constructor
accepts the existing durable writer for fault injection; no new infrastructure friend
assembly or project reference was added. A real committed-stage fault proves restart
lookup after lost acknowledgement. Later edits and deletion cannot change the recovered
snapshot. Pure tests exercise the publication protocol without constructing the store.

Historical receipts do not change Agent execution configuration. They are excluded
from the shared-configuration lock calculation, while the changed Agent key and catalog
witness remain mandatory. This avoids waiting on every unrelated Agent for one policy
save. Whole-catalog serialization, maximum-size overhead and sustained capacity remain
separate qualification work. Deploy updated catalog writers together under maintenance
exclusion; mixed versions and dataset restore qualification are not proven here.

## Validation and repairs

The exact filters and expected discovery are in the design's backend entry gate.
Both discoveries matched, and all affected production projects plus Unit/Integration
built in standard Release with local sibling libraries. Existing compiler/NuGet warnings
remain. HTTP tests exercise the actual owner as well as strict input, permission,
wrong-owner receipt and sanitized failure paths. Storage tests include two existing
package/provisioning races, capacity refusal without a write, expiry and corruption.

The first frozen candidate passed 35/40. It exposed two real defects: receipt changes
expanded locks to all Agents, breaking both unrelated-key races, and POST lookup had
inherited write permission. Both were repaired in their owning boundaries. Two new
collection assertions selected immutable-array identity equality; they now compare
the exact receipt sequences. The successor rebuilt all seven production/test targets
and reran the same 40 cases. No race deadline or production guard was weakened.

Portability rescanned the final source including new files and enforced **15,615
reviewed findings unchanged**, without writing the baseline. Rendering, documentation
and diff checks passed. No UI behavior changes in this checkpoint.

CodeAnalytics compares `snap-20261019144656-d75641cd` to
`snap-20261019152249-d75641cd`: four projects, 2,375 → 2,388 types, 19,419 → 19,466 members,
62 DI registrations and one entity unchanged. The same six pre-existing cycles remain.
The sole new finding is informational: the explicit receipt wire record has 11 members.
All 215 diagnostics are identical: 24 DI0001, two DI0002 and 189 WS0002. Its blocking
flag was already set by workspace-load diagnostics; the affected builds pass. This is
a scoped architectural delta review, not a clean dependency-workspace claim.

## Proof identity and resources

First candidate `c4-agent-policy-receipts` retained its five failures and verified
unchanged source/runtime. Proof SHA-256:
`61EE2E0AB5BBD23CD002B82591F3A7B51F64C708D7CECC18D0E35B3C74665519`.

The passing `c4-agent-policy-receipts-ready` inventories remained unchanged after tests:

- 9,401 source inputs: `0E0289C0426C6A6867D21758D2B2121FDEA9AD6B5B5190F7D99950AF8CBA5584`.
- 4,340 runtime inputs: `EBAADEDD0627CC91572A6E9F63C8A12933B075BA45F95681BFB0219A97B55D66`.
- Retained proof: `0235B28B09539AA81E890B6668C97FDA913BDA00E94F77B77F935DD08FB3D074`.

Both reuse the existing PostgreSQL 18 fixture and have ownership records from before
directory creation. Observers covered each entire test command. Both reconciled with
zero observed processes and zero new database delta; six/eight unattributed TEMP paths
were preserved respectively. Both freezes are released. Later documentation/commit
metadata are outside the executed identities. Older blocked cleanup remains recorded
in [resources](../resources.md) and was not retried or bypassed.

## C# architecture and progression gate

Status: **Pass for backend receipt lookup; native recovery remains open**.
No new project dependency or production partial was introduced. The existing large
store loses the extracted publication state; new types have receipt, codec or protocol
responsibilities and independent tests. The test-only partial shares the existing
owned fixture and adds no runtime capability. Module/Core forward reads without cache
invalidation. The API contract is covered by the Web build and real HTTP checks outside
the four-project analytics scope.

Proceed to bounded native pending submissions, navigation/restart and two-tab proof.
Process/Workflow activation, complete writer coverage, retention/restore, sustained
capacity and final E2E closure remain open. SPA stays on `codex/concurrent-mutations`;
no SPA main merge, push or colleague communication occurred.
