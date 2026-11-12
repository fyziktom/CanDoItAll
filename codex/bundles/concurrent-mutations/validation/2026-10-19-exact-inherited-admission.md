# Exact inherited Agent execution admission

Status: 73 distinct selected behaviors pass across an exact test-only repair bridge.
This closes the admission/recovery prerequisite, not Process/Workflow activation or R5/R6.

## Implemented behavior

`AgentInheritedPlanningRunAdmission` acquires the existing per-execution dispatch lease
and resolves the deterministic child execution ID before invoking preparation. A retained
child keeps its original input, material, grant, runtime contract and unknown work. New
publication reuses FileSandboxWorkspaceStore's existing generic initial commit journal.
No lineage scan, shadow reservation index or second filesystem transaction protocol was added.

The required business callback validates the original canonical parent on both paths.
Core additionally validates schema 7, original source/profile/run identity, current
source read access and explicit inherited runtime metadata v3. The inherited path cannot
substitute interactive authority; existing interactive metadata v2 is preserved. Read
recovery does not require renewed dispatch permission and does not grant dispatch rights.
The live owner independently requires current authority and intact work accounting.

Failures propagate. A primary failure and a lease-release failure are retained together.
A successful durable save followed by failed lease release resolves the same child on retry.

## Builds and exact discovery

Affected Core and Integration projects built in standard Release with local sibling
libraries. No custom output configuration was created. The initial frozen candidate
discovered and executed exactly:

| Filter | Cases | Initial result |
| --- | ---: | --- |
| AgentInheritedPlanningRunAdmissionTests | 28 | 26 passed, 2 test assertion failures |
| AgentInheritedPlanningOwnerTests | 18 | Passed |
| ProjectTaskPlanExecutionEntryTests | 11 | Passed |
| MafPlanningStepRuntimeTests plus MafInheritedPlanningRuntimeTests | 16 | Passed |
| Total | 73 | 71 passed, 2 failed, 0 skipped |

The two failures compared immutable-array record identities after deserialization.
The first test-only repair compared serialized decisions and passed 27/28; its absent
new-run branch tried to serialize a null decision, which the strict converter correctly
rejects. The final repair uses an explicit absence assertion and passes all 28/28.

Both bridges compare complete source inventories with the initial candidate. Only
`AgentInheritedPlanningRunAdmissionTests.cs` changed; every production and fixture input
is byte-identical. The initial 45 regression passes remain applicable. This is not a
claim that the initial 73-case invocation passed, nor that all 73 were rerun together.

Tests exercise independent-store same-ID contention, progress by another child, all six
initial commit interruption boundaries, source and parent denial, cancellation while
waiting for the lease, original unknown work, unrelated execution rejection, runtime
metadata substitution and primary/cleanup failure preservation. Healthy exact lookup
reads one execution record with either 4 or 96 unrelated records and reads no unrelated
execution payload. This is bounded-lookup proof, not sustained workload qualification.

## Static and architecture gates

The final complete portability scan includes untracked production source. Final no-write
enforcement passes with all 15,639 reviewed findings unchanged. Baseline-tool tests,
secret-scanner tests, rendering-tool tests, rendering register, documentation and diff
checks pass. No baseline change was needed.

CodeAnalytics changed from `snap-20261020000039-926561d8` to
`snap-20261020002927-926561d8`: four projects, 2,085 types, 15,767 members,
636 findings and 189 existing diagnostics. There are zero project cycles; the exact
five pre-existing type cycles are unchanged. No new finding was added. The existing
metadata informational finding changes from 9 to 12 source members (the earlier compact
review text's “10” was a transcription error; the retained snapshot finding is authoritative).

No project references or runtime partial files were added. The new owner has one
responsibility: exact inherited initial admission. Parent semantics stay with the
required module callback, while file transaction/recovery and live dispatch remain with
their existing owners.

## Frozen identities and retained proof

| Candidate | Source SHA256 | Runtime SHA256 |
| --- | --- | --- |
| Initial | `3524FC683DF7517E18E4C31594FCA785472C3707D57F2ACC2D36501A6D9A5014` | `87A7DEAE93063FE2923238307250AE8B6DADB5A67D2346C131ECD2A22F8B7C10` |
| First test repair | `28024B2F2380FBF9E7A5DEA0C353145E201441D48F474C834784FBFE8DF011A0` | `6F1DAFBDE39B4197DEC514CD009F3031E55B590140F9BDE4F17A9C20F1202CE6` |
| Final | `50FEF22D701BC398F614872FDFF3979F8F32F5992D59138F3B116CA288FC492B` | `14F7856D892AA119ACE01FE7D788F3C9D5303226FEBE80411BB875731348D576` |

Each inventory contains 9,525 source and 3,889 runtime files. All were verified unchanged
after their test executions, before releasing the freeze for the next repair or documentation.

- [Initial proof](../proof/2026-10-19-planning-authority/c4-exact-inherited-admission.json):
  `139100E1FBAFB358457E155075DEFC85C31EE737F79B2836563A64E17542D9E4`.
- [First repair proof](../proof/2026-10-19-planning-authority/c4-exact-inherited-admission-repair.json):
  `159961B7B01394AB926F23BB2641B16F967FA82DF21AB2BDA464B8A12DA906C7`.
- [Final proof](../proof/2026-10-19-planning-authority/c4-exact-inherited-admission-final.json):
  `40E7DB658308CE3F447107501AF734D3EA37E7787E352A546C0B52685B3F5755`.

## Resources and remaining work

Owned candidate directories are under `.artifacts/concurrent-mutations/validation`.
They reuse the isolated PostgreSQL 18 fixture on loopback 53619. Observers cover every
test command in full for 230.41, 58.06 and 103.61 seconds respectively. No observed
process or database-name delta remains. Respectively 12, 4 and 4 unattributed TEMP
entries were preserved; they are not deletion authority. Earlier cleanup denials remain
unresolved and the shared fixture is retained for subsequent integration.

Actual Process/Workflow producers, inherited Workbench publication, current manager
continuation, settings/capability activation, grant fairness, remaining writers and
capacity/release checks remain open. The historical broad Stable result is still
16,182 passed, 17 failed and 2,444 unexecuted. SPA remains clean on
`codex/concurrent-mutations`; no SPA main merge or push was performed.
