# Selected inherited Agent stop: implementation checkpoint

Historical status at this checkpoint: implementation retained; behavioral qualification
was blocked. [Subsequent qualification](2026-10-22-selected-read-connections.md) resolves the receipt deadline
and completes the remaining regressions. All original failures below remain evidence;
producer activation and end-to-end closure are still gated.

## Implemented behavior

Core owns selected inherited execution stopping through the existing narrow execution
reader, atomic writer, dispatch lease and cancellation registry. Its dedicated capability
validates the immutable original parent/session/profile/source and journal on every read.
Workbench supplies the task-plan adapter through Application's typed child-stop contract;
Process persistence reads the stopped root through a selected EXISTS query. No new project
reference, schema or partial class was introduced. Composition roots register these owners.

Live execution receives an exact-ID cancellation signal. The 250 ms bound covers dispatch
lease acquisition only; synchronous cancellation callbacks and the complete operation have
no claimed hard wall-time bound. An idle approval with closed measured work can cancel under
the lease and a final parent/profile recheck. Already closed outcomes replay without a new
revision or charge. Unknown effects, open work, unavailable journals and incomplete committed
publication remain explicitly pending. Accepted effects and original work are preserved.

The legacy Process observer uses indexed candidate selection and excludes inherited journals
on both reads, so it cannot overwrite their richer state. The extracted background execution
fingerprint retains the original serialized field order and golden hash; ordinary dispatch
still requires the complete current profile generation. Parent stopping permits a historical
original generation while fencing the current profile throughout waits and publication.

## Validation and blocker

All **58 new cases pass**, within 75 selected-stop/prerequisite cases. Another 13 existing
outcome-reader cases pass. One existing lost-acknowledgement recovery case fails at the final
read-authority check inside the unchanged **two-second receipt checkpoint budget**. Its
three isolated repeats on identical source/binaries also fail. The first failure cancels a
catalog read; isolated failures include PostgreSQL connection opening. This establishes a
repeatable deadline failure on this host, not its complete cause. No deadline was increased,
authority check removed, cached authorization introduced or failure hidden by a retry.

The next three selected lanes, **98 cases**, did not execute after that failed gate.

| Selection | Discovered | Passed | Failed | Not executed |
| --- | ---: | ---: | ---: | ---: |
| Unit-AgentInheritedParentStopTests | 20 | 20 | 0 | 0 |
| Unit-AgentBackgroundExecutionIdentityTests | 15 | 15 | 0 | 0 |
| Unit-AgentExecutionCancellationRegistryTests | 3 | 3 | 0 | 0 |
| Unit-AgentFrameworkProcessRuntimeCancellationObserverTests | 6 | 6 | 0 | 0 |
| Unit-AgentPendingApprovalCancellationTests | 13 | 13 | 0 | 0 |
| Integration-AgentInheritedParentStopTests | 8 | 8 | 0 | 0 |
| Integration-ProjectTaskPlanProcessChildStopperTests | 10 | 10 | 0 | 0 |
| Integration-ProjectTaskPlanProcessOutcomeReaderTests | 14 | 13 | 1 | 0 |
| Integration-FileSandboxWorkspaceProcessQueryIntegrationTests | 12 | 0 | 0 | 12 |
| Integration-AgentToolBackgroundJournalIntegrationTests | 35 | 0 | 0 | 35 |
| Integration-AgentInheritedPlanningSavedEffectTests | 51 | 0 | 0 | 51 |

The SQL adapter's ten passing cases prove both snapshot modes, original work/budget replay,
accepted publication preservation, stopped-root/profile checks and foreign/corrupt bindings.
They intercept four root EXISTS observations without loading steps/claims in those probes.
The eight real file cases prove lease contention, caller cancellation, late revocation,
lost acknowledgement and open-work preservation. Pure tests exercise narrow Core ports.

Earlier attempts retain three test-only assertion/lease-arrangement failures and two fixture
generation-initialization failures. Their repairs each changed one test file and only the
Integration DLL/PDB. All production inputs/binaries remain unchanged across those attempts.
The initial partial-class discovery omission was corrected from 15 to all 35 journal cases.

Nine affected standard Release builds pass; Integration was rebuilt after each test repair.
The final 9,930 source inputs and 8,078 runtime files across eleven standard Release roots
remain unchanged through the selected run and all isolated repeats:

- Source manifest SHA-256: `AA9922D7AFAE2E4EAE0B4E975F270A7C387E0A65E8759AAE756068AE87CEBB18`.
- Runtime manifest SHA-256: `F9780570DDCA0BBE821AF11BFEF914683A4A225E02B1073D88693B71391A78A5`.

Full portability scan and enforcement pass with **15,665 unchanged reviewed findings** and
no baseline rewrite. Rendering-register validation passes all 1,212 inputs; its one reviewed
change concerns registration only. Static-tool tests and documentation validation pass.

## Architecture review and progression

Five scoped before/after analyses retain all existing cycles and project references. Core
and Application have no diagnostics. Persistence retains ten WS0002 package-advisory workspace
diagnostics reported as errors; Workbench and Process retain their existing package/DI
diagnostics. Scoped facts are not a claim of globally clean analysis. Actual Release builds
and passing DI adapter tests are separate evidence. The sole new informational member-count
finding is the legacy cancellation observer's explicit inherited-journal exclusion predicate.
No fake extraction was added to hide that count. Full comparison is retained in the proof.

Qualification remains blocked by the repeated receipt deadline failure and unexecuted lanes.
Next measure connection opening separately from SQL execution and split Agent detail versus
current-authority read cost. Then repair the demonstrated bottleneck and rerun affected proof.
Root quiescence and fair durable recovery have a prepared design but have not been implemented.
Snapshot refresh, producer activation, Workflow/settings/SPA, remaining writers, finite read
latency, sustained capacity and final integrated Stable remain open. SPA stays on its own
`codex/concurrent-mutations` branch; no merge into its colleague-owned main is authorized.

## Retained evidence and resources

Both current runs have zero observed processes, database-name changes or temporary-path
residue. All eight selected-run commands and three diagnostic commands are covered by their
resource observers. Earlier repaired attempts also have zero new residue. The existing
isolated PostgreSQL server is reused; ordinary 5032 and historical denied cleanup are preserved.

Secret-reviewed proof: `proof/2026-10-19-planning-authority/c4-selected-parent-stop-checkpoint.json`,
1,330,947 bytes, SHA-256 `7909CF6CEE9B0CABF49454E39789250E9D1F6A5CC122D0947FE1DB7B13DC14E6`. It retains exact failures, unexecuted selections,
build/test/static commands, architecture, immutable input identities and resource/load evidence.
Documentation-only edits receive a separate exact source bridge before the signed checkpoint.
