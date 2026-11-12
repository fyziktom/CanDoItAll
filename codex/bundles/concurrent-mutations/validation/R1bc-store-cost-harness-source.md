# R1bc store-cost instrumentation source checkpoint

Status: source authored and independently reviewed; **unbuilt, undiscovered and
unexecuted**. This checkpoint contains no production optimization. The parent's
R1c proof and source inventory remain separate.

The exact inventory is
[`R1bc-store-cost-harness-amended-source-inventory.json`](../reviews/R1bc-store-cost-harness-amended-source-inventory.json).
It identifies the current managed source hashes and the existing main-file bases
for the four new Integration files, the one existing fixture seam, the measurement
card and this note. The parked `FileSandboxWorkspaceJsonStore.cs` optimization is
not an input. Retain baseline source and loaded binary stamps before transferring
that candidate.

| Theory method on `AgentReceiptStorePerformanceTests` | Source rows |
| --- | ---: |
| `Serial_owner_slice_detail_and_native_refresh` | 12 |
| `Independent_maximum_workspaces` | 8 |
| `Four_distinct_runs_share_actual_workspace_coordination` | 2 |
| Total | **22** |

All rows use explicit enum InlineData and class `Category=LongRunning`; there are
no Fact, MemberData, ClassData, conditional-skip or inherited test rows. Stable's
canonical exclusion therefore omits this class. Parent-owned discovery must verify
22 aggregate rows and then exactly one per fresh-testhost execution. No source row
count is inferred from a runner listing.

The existing fixture's sole behavioral extension is an optional owned parent for
`CreateAsync`. Omitting it preserves all prior calls. The harness uses its small
template, then seeds real stores sequentially under pre-recorded child ownership.
It does not retain large original journals or seed-store caches. Independent
workers use different roots and deterministic profiles; the queue control uses
one root/profile with distinct deterministic run/chat/session/proposal identities.

The independent source review checked setup graph lifetime, real owner/mapper/native
and API serializer seams, awaited lease and service cleanup, process-wide allocation
attribution, actual worker overlap, headroom and last-GC labels, failure evidence,
explicit cumulative revision/cursor histories and ownership before allocation.
Review corrections removed retained setup graphs, counted actual detail/native
authority observations, separated setup and operation windows, preserved known
checkpoint results before cleanup, and added a separately recorded idle-overhead
window. The final narrow delta made seed identities/timestamps deterministic and
retained worker/phase association on existing store activities. This is source
review, not a measured passing result.

Each row records one cold owner, two warmups and seven measured cohorts. It stops
after a failed sample without automatic retry. Seven samples support median/max,
not a reliable tail percentile. Fresh owner instances do not imply cold OS caches.
The one-second idle window is recorded without subtraction. Phase process-allocation
deltas exist only for serial rows; one precise process delta measures each full
concurrent cohort. Nested phase/activity costs are not additive wall time.

The accepted provisional profile is 8 GiB application / 2 GiB idle reserve /
at most 3 GiB incremental observed private bytes for four independent maximum
runs. Actual physical, GC and Windows current-process commit headroom is required. Both pre-setup
whole-row and operation-entry increments remain visible; setup can invalidate the
reference profile without proving an application-memory defect. No forced GC,
OS memory cap or global serialization is introduced. Sampled maxima and last-GC
heap/LOH state cannot establish actual transient peaks; the planned diagnostic pass
and phase attribution remain mandatory for qualification.

Parent review corrected the two Windows commit field labels before any execution.
The existing `MEMORYSTATUSEX` observations describe the effective system-or-process
limit and the current process's available commit, which may be smaller than system-wide
commit headroom. See [the Microsoft field definitions](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex).
No sampled value, interop layout, threshold or workload changed. The original inventory
`285656A5354829E921A4784F05FC5F07DE010B5532BD1D9060F875A8CFBB2328`
remains retained; the amended inventory identifies this label-only delta.

Read/write payload counts are logical observations, not total or physical device
I/O. Compact initial run/journal hashes establish matched seeds; owner-written
subsequent times and dispatch lease IDs remain real. Core receipt reads use a
deterministic NotObserved provider with actual claim checks; the service/native
reads use real current-authority resolution. Provider execution is forbidden.
These 22 rows do not qualify actual interactive/Process authority performance,
browser rendering, provider work, runtime-policy activation or production capacity.

No builds, tests, scanners, DB operations, runtime processes or diagnostic tools
were executed by the author. The parent owns final compilation, portability-static
review/enforcement, exact discovery, fresh-host execution/deadline, retained
baseline/candidate comparison, cleanup reconciliation and interpretation.
