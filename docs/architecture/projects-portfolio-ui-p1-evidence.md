# Projects P1 evidence index

This index accompanies [the architecture record](projects-portfolio-ui-p1.md). It identifies
local evidence without committing runtime logs, credentials, protected snapshots or raw
browser traces. The owned run directory is
`artifacts/projects-ui-p1/20261001-038a2696`. Failed attempts remain there. Reruns are not
added together as unique coverage, and a narrow success does not relabel a mixed run.

## Source and environment

| Input | Recorded source |
|---|---|
| Application starting commit | `038a2696f31291cfda5b34b88db75f28405472cd` |
| Application starting tree | `cdfe12f2b90b47146cfef30bcddb71b1bf3bc394` |
| Signed S0 test commit | `b589e44eceb9afe52fcf9b01fc46cdedd6a0e99f` |
| Components local/development commit | `4a858412d2c2a3f6123bf23d8c4584f05b47627d` |
| Components tree | `1e318a37f187c120e74d88357715ba22ae5cec31` |
| FileTools local commit | `3a080ecd31068a77c1e1bd639f7a78e21c93db85` |
| FileTools tree, also matching the CI pin | `6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d` |
| Toolchain | .NET SDK 10.0.303; runtime 10.0.12; source references |
| Database | Owned PostgreSQL 18.6; `WAL_LOG` test cloning |
| Focused configuration | `ProjectsUiProof` |
| Integration checkpoint | `ProjectsUiFinal` |
| New UI validation | 1920×1080 only |
| Paid/live inference | Not authorized; zero new requests |

`entry.json` records the pair after the first three S0 test edits. Initial application and
sibling checkouts were clean. `evaluated-graphs-before.json`, `evaluated-graphs-after.json`
and `graph-comparison.json` distinguish source availability from actual evaluated resolution.
All 37 existing protected UI/sandbox closures and their package references are unchanged.
The new UI/sandbox closures contain five/six projects. The Web closure remains large.

`stable-to-final-source.json` records portable-PDB source checksums for 206 unique binaries and
6,048 available source documents. Its differences from the final source are explicitly
listed: four contract EOF cleanups, renderer/boundary-test formatting, sandbox edit revision
tracking, and the final Projects host/readiness controls. The later Files alignment repair is
also outside that checkpoint's compiled source. `focused-with-files-binary-source.json` records
173 unique binaries and 4,717 source documents with no current-source mismatch. Generated or
unavailable PDB documents are excluded from that comparison. Static assets and published
sandbox behavior have their separate publish/browser evidence.

## Tests and attempts

Discovery preceded execution with the same filter/configuration. The recorded focused
discovery counts equal execution, with no skips in the successful rows below.

| Proof | Discovery / execution | Result | Local evidence prefix |
|---|---|---|---|
| Exact cold ordered shared-provider acceptance | 1 / 1 | Pass; cause remains unresolved | `s0-cold-01`, `s0-browser-discovery` |
| S0 provider ordering and retained session/surface controls | 29 / 29 | Pass | `s0-components` |
| Original starter isolation and unblurred input controls | 2 / 2 | Both demonstrate the original defects | `p1-original-controls-03` |
| Leaf form, raw validation and dependency boundaries | 11 / 11 | Pass | `p1-leaf-final` |
| Final Projects page and native mutation controls | 37 / 37 | Pass | `p1-owner-final-03` |
| Files-owner cases after the alignment repair | 11 / 11 | Pass; subset of the page suite, not additional unique coverage | `p1-files-owners-final-02` |
| Existing context provider and CRM portfolio consumer | 6 / 6 | Pass | `p1-context-consumers-final` |
| Final-review read ownership controls before repair | 3 / 3 | Three demonstrated failures | `p1-final-review-controls-before` |
| File Agent positive UI/approval/write/attach/read-back | 1 / 1 | Pass | `p1-ui-agent-03` |
| First consumer batch | 7 / 7 | Mixed: four pass, three fail | `p1-ui-consumers-01` |
| Two exact refusal follow-ups | 2 / 2 | Pass, original batch stays mixed | `p1-refusal-followup-01` |
| Workflow asset/TestLab and early Files journey | 2 / 2 | Workflow passes; early Files journey fails | `p1-ui-handoffs-02` |
| First completed Files and neighboring shell journey | 1 / 1 | Pass under original DOM assertions; visibility gap below | `p1-ui-handoffs-final` |
| Files viewer geometry before repair | 1 / 1 | Demonstrates two-pixel viewer width | `p1-files-visibility-before` |
| Final visible Files and neighboring shell journey | 1 / 1 | Pass, including geometry and viewport assertions | `p1-files-visible-final-02` |
| Full frozen Stable checkpoint | 15,851 discovered / 15,906 executed | Mixed: 15,905 pass, one artifact-scanner failure, zero skips | `stable-discovery-03`, `stable-final`, `stable-reconciled.json` |
| Complete owning Unit checkpoint after artifact repair | 9,366 discovered / 9,400 executed | Pass; unchanged frozen binaries/filter, 34 runtime-expanded theory rows | `stable-unit-artifact-repair`, `stable-unit-reconciled.json` |

`test-attempt-ledger.json` retains individual TRX counters, times, file hashes and failures;
`*-result.json` retains exact project, filter, expected discovery and configuration. Earlier
compile and harness failures are retained in their corresponding discovery/log files.

The first consumer batch failed two refusal cases at runtime continuation deadlines and a
Workflow case because the test selected Runs before the exact TestLab plan was ready. The
plan test now waits for the actual plan identity. Both refusal cases passed once on fresh
owned hosts, with unchanged product code, deadlines and grants; no causal repair is claimed
for those two deadline observations. The separate Workflow follow-up verifies immutable
version/input, native file bytes, persisted TestLab result and reopened plan.

The Files journey initially raced SSR hydration, then exposed test locator mistakes. Its
final run waits for interactive readiness and preserves server navigation acknowledgement
and clean-host assertions. The temporary `RendererInfo` marker failed 27 of 34 bUnit cases;
the final first-render marker passed the owning suite and browser. These failed attempts
remain distinct from current proof.

Screenshot review found that the original Files dialog could collapse its contained viewer
to two pixels even while the expected bytes were present in the DOM. The stronger browser
control reproduced that defect. The original owner now stretches the existing Stack's
children; no Files extraction or shared-library change was required. The final screenshot
shows readable native Markdown and the same disabled edit controls. The initial repair's
missing enum import was a separate build/discovery failure, preserved as
`p1-files-visible-final-discovery.log`.

The full Stable run took 5 h 6 m 32 s and remains mixed. Its source scanner correctly found
synthetic provider-key arguments from its own negative-control theory in `stable-cases.txt`.
`discovery-artifact-repair.json` binds the unchanged raw list, retained at
`.artifacts/projects-ui-p1/20261001-038a2696/stable-cases.txt`, to the normalized derivative
in the run directory. Only three known synthetic arguments changed; discovered identities,
counts, failed TRX and logs remain intact. No scanner policy, test, source or binary changed.
The complete owning Unit assembly was rediscovered and rerun: 9,400 passes, zero failures
or skips, with the original per-method execution counts. The other 19 original assemblies
passed 6,506 cases, including all 3,273 Integration cases. These are separate checkpoints;
they are not added into a new all-green full Stable claim.

The original 55 runtime-expanded cases (16 Memory, five Integration and 34 Unit) are recorded by method
in `stable-reconciled.json`; no assembly or discovered method is missing or unexpected.
Current CI uses separate Unit/Memory/core jobs and sharded Components/Integration jobs
(90-minute Linux and 180-minute host shard budgets). This sequential, concurrently loaded
workstation run does not predict those shard durations or certify CI timeout headroom.

## Reproduction commands

The owned `Invoke-Proof.ps1` configures PostgreSQL through private files, disables live-model
gates, performs build-backed discovery, checks the expected count and runs the identical
filter with `--no-build --no-restore --logger trx`. Its underlying command shape is:

```powershell
dotnet test $project --configuration ProjectsUiProof --list-tests --filter $filter /m:1
dotnet test $project --configuration ProjectsUiProof --no-build --no-restore --filter $filter /m:1 --logger trx --results-directory $ownedResults
```

The final owner filter is
`FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectsPageTests|FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectsEditorMutationTests`.
The six-consumer filter is
`FullyQualifiedName~ProjectsAgentChatContextProviderTests|FullyQualifiedName~ProjectsCrmHrIntegrationTests`.
The final browser filter is `FullyQualifiedName~ProjectsPortfolioBrowserTests`.
The Files-owner follow-up filter is `FullyQualifiedName~ProjectsPageTests.Project_file`;
its first discovery found 11 cases against an expected eight, so no tests ran until the
expectation was corrected. That discovery failure is retained separately.

The Stable trigger is the moved public Projects types and native acknowledgement/admitted
seed seam. The owned `Stable.ProjectsUiFinal.slnx` has the same project paths as the canonical
Stable solution, plus an explicit isolated configuration declaration. This avoids the
canonical solution rejecting an undeclared custom configuration. The filter remains:

```text
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

The build-backed `--list-tests` run and subsequent `--no-build --no-restore` execution use
that solution, configuration, `/m:1`, source pair and owned PostgreSQL. Initial configuration
and test-compilation failures remain separate from successful discovery. The artifact repair
used `Invoke-UnitCheckpoint.ps1` against
`tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`, with the same Stable filter,
`ProjectsUiFinal`, `--no-build --no-restore`, `/m:1` and owned PostgreSQL. Both discovery
and execution use the unchanged frozen assembly. `stable-unit-reconciled.json` compares
its per-method executed counts to the original Unit TRX.

The retained owning test sources include the following. Their final aggregate results belong
to the Stable reconciliation, separately from the focused page/renderer evidence.

| Owner behavior | Existing source |
|---|---|
| Bounded Cards/Files projection and read generations | [ProjectFileFilterProjectionTests](../../tests/Unit/CanDoItAll.Tests.Unit/ProjectFileFilterProjectionTests.cs), [ProjectsPageLoadGenerationTests](../../tests/Unit/CanDoItAll.Tests.Unit/ProjectsPageLoadGenerationTests.cs) |
| Native project write/admission and reservation | [ProjectsServiceIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/ProjectsServiceIntegrationTests.cs), [ProjectWriteAdmissionIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/ProjectWriteAdmissionIntegrationTests.cs), [ProjectCreationReservationIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/ProjectCreationReservationIntegrationTests.cs) |
| Deletion, recovery and shared storage | [ProjectDeletionIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/ProjectDeletionIntegrationTests.cs), [ProjectDeletionSharedStorageConcurrencyIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/ProjectDeletionSharedStorageConcurrencyIntegrationTests.cs) |
| Real package records/media round-trip and target locking | [DatabaseRuntimeSwitchingIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs) |
| Package format and retained owner histories | [ProjectPackageHardeningTests](../../tests/Unit/CanDoItAll.Tests.Unit/ProjectPackageHardeningTests.cs), [ProjectTransferHistoryPersistenceTests](../../tests/Integration/CanDoItAll.Tests.Integration/ProjectTransferHistoryPersistenceTests.cs) |
| Agent authority and project lifetime | [AgentNativeProjectAdmissionIntegrationTests](../../tests/Integration/CanDoItAll.Tests.Integration/AgentNativeProjectAdmissionIntegrationTests.cs), [AgentProjectLifetimePersistenceTests](../../tests/Integration/CanDoItAll.Tests.Integration/AgentProjectLifetimePersistenceTests.cs) |

## Assets, loops and gates

`production-build-final.json` records direct serial builds of Projects.Contracts, Projects.UI,
the Projects and Workbench modules, the sandbox and Web. All six passed in `ProjectsUiProof`
on the final production source. These builds use separate outputs from the running Stable
checkpoint; no Stable binary or PDB changed after its recorded capture.

`sandbox-publish-final-result.json` records an independent publish, with the Production
host receipt and `sandbox-published-final.png`. Source/published desktop checks used the real
renderers, four loaded stylesheets, material fonts and all five mounted steps. The sandbox
is a declared state simulation; actual file authority is proven by the production journeys.

`development-loop-summary.json` contains three observed samples for each supported edit
scenario. Individual loop JSON files include restored SHA-256 identities. The architecture
record gives timings, hydration, watch counts and limitations; no universal speedup is
claimed. Original archive/path-length failures and the polling watcher stall remain recorded.

`portability-final-with-files.json` scanned 7,718 candidate files and recorded 33,185 raw findings.
`portability-review.json` explains each of 15 added and 13 stale baseline entries.
`portability-final-with-files-enforcement.log` passes without the write flag for 15,216 reviewed
executable-source findings. The scanner's ten self-tests and documentation evidence's nine
cases passed. The sealed package validator passes 43 files, 16 groups, 55 local links and
23 source entries; that result is metadata/seal consistency only.

`evidence.json` is the working copy of the sealed package's sixteen evidence groups.
`safe-artifact-index.json` records SHA-256 identities for curated receipts, source/test
metadata and two visually reviewed desktop screenshots. `consumer-proof.json` binds the
UI-created project/lifetime/phase/option/starter identities to the actual Agent and Workflow
asset hashes and the persisted TestLab run. `sandbox-scenario-review.json` labels manual
observations explicitly and does not invent an automated case count.

The main text-artifact scan reported 22 findings, all reviewed source/fixture false positives;
`secret-review-disposition.json` records their individual fingerprints. Five private sentinel
files produced zero matches. The scoped successful Agent and Workflow text-evidence scans
each passed with zero findings. Raw traces, API-issuance screenshots, protected snapshots and
private credentials remain outside the curated index. A safe index is not a blanket statement
that all retained raw artifacts are publishable.

All seven exact owned containers and the published sandbox are stopped. `owned-cleanup.json`
records their verified identities and exit states. Owned volumes, private files and raw proof
remain retained for diagnosis; the user host on port 5032 and prior artifacts were untouched.
Final documentation checks cover nine evidence-validator cases and 327 maintained Markdown
files. `stable-reconciled.json` accounts for every canonical assembly, discovered method and
runtime theory expansion; frozen binary/PDB hashes still match their captured values.

## Remaining scope

S0 is `REVALIDATED_WITHOUT_ROOT_CAUSE`. Workspace rendering boundaries and historical R2
counts remain intact. Files P2 and other module extractions were not started. Scheduler
delivery was not rerun: no scheduler/executor/capability contract, job policy or existing
ID-based seed caller changed. Corresponding owning Stable suites remain part of the gate.

The exhausted 40-request journal is unchanged. Paid live-model and generated-application
prerequisites remain outside this task; they are not passes. Local dependency resolution
and runtime proof do not claim a successful future consumer CI run or application release
readiness. Components publication is available on development; main and the unmatched
feature branch retain the integration constraints in the Workspace record.
