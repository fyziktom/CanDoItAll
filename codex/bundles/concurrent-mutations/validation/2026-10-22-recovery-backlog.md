# Native recovery backlog progression

Status: **Pass, Behavioral.** Both new Integration rows pass on unchanged production
`91ce8b113`. One and two independent service providers share one owned PostgreSQL profile
inside the test process. This is not a concurrent multi-OS-process scheduling test.

Each row creates 32 live and eight historical roots. Real owner seeding selects bounded
pages of 16, 16, 8 and 0 roots, retaining its completed cursor. One live root's deliberately
corrupt position is made due first while its valid original intent remains saved. The
actual queue defers it as InvalidEvidence, retains both wire values and releases its lease.
Every other root obtains durable progress, including all eight historical roots.

The actual registered worker, coordinator and stage dispatcher run with one operation
per slice. A test-only queue decorator records calls and delegates every operation to
EfProcessCancellationRecoveryQueue. Both initial live/live/live/legacy cycles occur for
each worker. The single worker makes 42 claim attempts; the two workers make 30 and 13.
These counts demonstrate finite-backlog progress and poison-root isolation, not a global
ordering promise, capacity limit or latency SLA. Completion of every artificial historical
shape is outside this test's claim.

Exact discovery is two and both pass. Integration builds twice in standard Release;
the second build includes cleanup that awaits every worker and attempts every provider
disposal. There are no failed builds or tests in this checkpoint. All 48 source pins and
56 owning/copied outputs remain unchanged during execution. Only the Integration DLL/PDB
differ from the signed worker checkpoint; the other 54 outputs and every pinned production
source are identical. Prior worker, queue and OS restart proofs remain trusted.

Architecture gate: **Pass with existing production follow-ups.** No production type,
member, partial owner or reference changes. The preceding module snapshots and their
explicit NuGet audit limitation still apply. Full portability passes without a baseline
edit: **15675 reviewed findings unchanged**. Tooling, rendering, documentation and
whitespace checks pass. Broad Stable stays at the final integrated freeze.

The native command is fully observed and reconciles with zero new process, database-name
or temporary-path residue. Two bulk scans were removed after exact size/hash/containment
checks and verified absent. Durable proof scanning passes with zero findings. The raw
local scan has three reviewed helper-variable matches and no private sentinel; raw
evidence and hashes remain retained. Ordinary 5032 and denied historical cleanup targets
are untouched.

[Compact proof](../proof/2026-10-22-recovery-backlog.json): 51581 bytes, SHA256
`C3992980ADDBEE4CA60C6E117428F54EB5EA69C19A7C00634CAF913DB2F4D6E6`.
Local proof is in `.artifacts/concurrent-mutations/validation/c4-recovery-backlog`.

Next investigate the retained whole-journal cost regression and contention through
controlled measurements. Enabled remains false. HTTP/status cutover, snapshot refresh
and settings, remaining writers and final E2E are open. SPA remains on its own branch.
