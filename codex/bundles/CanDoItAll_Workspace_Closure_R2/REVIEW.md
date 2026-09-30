# Connected-source review

Review date: 2026-09-30. Repository: fyziktom/CanDoItAll, components-decoupling.
Reviewed HEAD: `15eadc18932e77a20ae5be2d7f2b207700a15d25`. The compare from the preceding
review contains six commits, including archived instructions, production changes,
observation/test corrections and the final documentation record. [R01, R02]

## Disposition

Workspace rendering is structurally complete. Keep its leaves and intentional route/DI/
authorization hosts. The current owner code closes the exact-profile precheck/write gap;
Simple Chats now owns actual async dependency scopes for admitted operations rather than
merely retaining a cancellation source. MainLayout now has explicit route/layout/profile
fences. These are real code changes, not documentation-only claims. [R05–R09]

Do not reopen those designs without a new reproducer. Their reported passing tests are
positive but distinct from tests executed by this reviewer. Components' repair was described
as locally tested, but its recorded SHA could not be fetched remotely during this review.
No claim of independent review of those unavailable repaired source bytes is made. [R13, R14]

Two remaining P2 failures are not cosmetic:

| Item | Evidence strength | Decision |
| --- | --- | --- |
| WCL-DEL1 | Maintained report describes two completed turns, UI failure and manual reproduction, retained exact Agent on reload. Current deletion code includes coordinated multi-file/journal work. Underlying failing stage remains unobserved in the supplied public record. | Reproduce with safe exact-stage diagnostics, then bounded repair; no assumed root cause. |
| WCL-NAV1 | Maintained full-host log report identifies RemoteNavigationManager and circuit cancellation; causal mapping to the preceding usage test remains unproven. Current test waits for URL/charts, not navigation acknowledgement. | Correlate and hold acknowledgement; distinguish product versus fixture/framework lifetime. |
| Dependency delivery | Connected Components branch inventory still shows older main/development; recorded repair SHA returns not found. | Verify local pair; publish only under separate permission; do not claim remote equivalence. |
| Current module summary drift | Current modules.md says only two feature renderers moved and Workspace leaves remain deferred. This conflicts with actual projects and closure record. | Small maintained-doc correction in this run; historical sealed files remain unchanged. |

Sources: DEL1 [R03, R10, R11], NAV1 [R04, R09, R12], delivery [R02, R13, R14], map [R16, R17].

## An important refinement to deletion investigation

`DeleteAgentWorkspaceDataAsync` does planning/validation before creating the durable pending
journal. However `LoadIndexForAgentDeletionAsync` can reconcile and write SessionCount before
journal admission. Therefore the absence of `pending-agent-deletion.json` does not prove
there were no persistent preparatory effects. Do not infer rollback, success or a failing
stage solely from that absence. [R10, R11]

The actual guard checks indexed/run active state and unresolved effects; a retained active
dispatch lease ID in a Completed run is not by itself proof of the failure's cause. All
counter, payload, revision and ownership validations remain useful invariants. [R11]

## Scope and limitations

Source reads covered the changed owner/lifetime implementations, relevant new tests,
maintained closure and finding records, module projects and representative remaining
renderers. Large module inventories are planning-level, not a line-by-line certification.
Default-branch search was used only for path discovery; substantive code reads were pinned.
The source register records slices, full reads and failed retrieval separately.

No product build, C# test, browser execution, private snapshot restore or runtime failure
reproduction was performed in this review. No .NET SDK is available in the review container.
Original private TRX/log/media were not supplied. Reported counts are implementer records,
not independently re-executed observations. Package validators establish local bookkeeping
and integrity only. No product patch is included; the files are implementation instructions.
