# Memory UI boundary and validation

The `/memory` route now uses the same seven-tab renderer and presentation policy as
the standalone Memory sandbox. The module retains production services, persistence,
driver dispatch, profile authority and executable capability enforcement. This is an
in-process extraction; it does not add a backend or enable unsupported provider features.

## Execution context and scope

The Memory handoff, its Scheduler review, source register, validation matrix and complete
shared foundation were read against current repository instructions. Entry was a clean
`components-decoupling` checkout at `059f4dafcdfc4ae65bbb13c1533ce8a49d988652`. The reviewed
`14f07bffa30ddb124011869e38bc5b264b8bce42` implementation had only bundle archival changes
after it. No checkout, reset or branch replacement was required.

SC-R1 and SC-R2 were reproduced and repaired first in signed commit
`51648a77e0f0b60c1acae84e2ccc9880e354a062`. The
[Scheduler record](scheduler-ui-boundary.md#sc-r1--sc-r2-closure-memory-handoff-prerequisite)
records failing-first evidence, request/receipt fencing, dependent validation retirement,
59 light tests, 15 real-page tests and two browser journeys. The shipped shell fix and
Plugins repairs remain unchanged. No third module was started.

Environment: Windows, .NET SDK 10.0.303, source dependencies, parity assets,
`MemoryUiProof` configuration. Sibling revisions were Components
`f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85` and SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`. No sibling source was changed.
CodeAnalytics, Components and dotnetwatch MCP were unavailable. Source inspection,
evaluated restore graphs, CLI watch and real Playwright observations supplied the proof;
there are no invented MCP results. Proof tier is Behavioral. The handoff remains sealed
and unchanged; this maintained record is the implementation receipt.

## Responsibilities and consumers

| Owner | Responsibility and dependencies |
| --- | --- |
| `Modules.Memory.Contracts` | Data-only profile/editor/result values, capability policy and owner port; references only existing `Memory.Abstractions` |
| `Memory.UI` | `MemoryWorkspaceSurface`, typed `IMemoryWorkspace`, all meaningful child renderers, scoped CSS, explicit registered-component and safe-URL projection; Contracts and BaseLib/Common |
| `Modules.Memory.Presentation` | Shared `MemoryProvidersPageController`: draft lifetime, snapshot publication, handler admission, submitted inputs and observed outcomes; references UI |
| `Modules.Memory` | Thin routed adapter; concrete facade, profile mapper/validation, request factory, profile-origin guard, snapshots, executable guard and action services; existing runtime/application/store owners |
| `Memory.UiSandbox` | Same controller and surface with a deterministic owner-port store and controlled waits; Presentation only, authoritative theme linked as content |

The original 29 files under module `Components` moved together, including list/detail,
summary, profile, capability, HTTP and MCP editors; query/result/context pack/feedback
form; operations/events/feedback rows and panels; ingestion; formatting, availability,
Provider UI host and built-in mock panel. Page-scoped CSS moved to the renderer's
`.memory-workspace` root with descendant scope across child boundaries. `_Imports`
explicitly imports the real component contracts. No feature-specific JavaScript exists.

The seven tabs remain Providers, Operations, Events, Feedback, Query, Ingestion and
Provider UI. Query provenance includes source module, record and citation. Client-rendered
tabs retain ordinary editor controls; external Provider UI mounts only while active and
is keyed by draft/profile revision. Registered components receive the exact approved
Provider and Surface parameters. No manifest assembly name is loaded dynamically.

Existing namespaces are retained for moved components, DTOs and controller. Their
assembly ownership changes. Actual source consumers were updated to the owner mapper;
the editor's implementation-dependent static mapper was removed. No external binary
consumer requiring a type forwarder was found, so binary compatibility with an old
precompiled module assembly is not claimed. The pure defaults match the actual HTTP/MCP
constants. Display status mapping is exhaustive and preserves all 21 numeric values;
unknown future values fail explicitly. Runtime/protocol enums were not globally moved.

Affected consumers include the route and DI registration, current page/editor/round-trip/
validation/surface tests, Memory handlers and HTTP/MCP/NativeRemote drivers, protocol and
feedback-handle consumers, API profile configuration, PostgreSQL owners and Agent/MAF
result disclosure. Product `CanDoItAll.slnx` includes Contracts/UI/Presentation only.
The new light test project belongs to Components, Stable and all three actual component
CI project lists. Playwright has a non-assembly reference to build the sandbox.

## State, effects and supported behavior

One live draft contains distinct profile, query, feedback and ingestion inputs. Automatic
refresh and tab changes retain raw values, unfinished tags, validation and incomplete
numeric text. HTTP timeout/retry fields keep text until authoritative integer validation;
they do not silently clamp or revert while typing. Explicit selection or reset retires
the old draft. There is no cross-circuit cache or collection of hidden retained editors.

Provider IDs remain ordinal. Editing InstanceId upserts the captured destination and
does not rename/delete the original. Initial selection may choose the first provider;
an explicitly missing selection remains missing and leaves the remaining list usable.
An already initialized empty draft remains unselected after catalog repopulation until
the operator selects a provider.

Every command captures complete inputs before the first guard/validation await, including
nested transports, collections and cloned JSON values. A same-destination pending or
unknown command blocks direct duplicate handlers. Independent destinations may progress.
Demo creation conflicts with its fixed provider IDs and blocks other dispatch while active.
Receipts are bounded at 32; only completed/non-blocking receipts can be retired for space.
Pending and unknown effects are never evicted to permit another write.

Reads fence success, errors and completion by request, draft and database-profile origin.
A snapshot revision also prevents a result for an earlier same-ID profile from becoming
current. Selection/disposal cancels owned reads, not already dispatched writes. A late
result retains its original receipt and facts without changing the current editor/tab.
The real origin checks canonical database-profile ID and generation at the owner seam.

Returned saved identities, operation IDs, accepted operations, feedback handles, ingestion
job/snapshot identities and dispatch-attempt facts survive follow-up read failures. Page
Refresh reads snapshots only. Explicit operation status may call the supported provider
port and update its ledger; no refresh replays a query or other action. Blank query input
is a known pre-dispatch refusal, allowing correction without an unknown-outcome lock.

Unknown outcomes remain distinct from refusal and returned results. Explicit serialized
review reads the original exact identity; it never replays the command. Seeing a saved
profile or operation is an observation of current persistence, not proof that the earlier
command completed. A query without a known operation ID stays unresolved. Partial demo
creation retains confirmed IDs; catalog review permits an explicit subsequent attempt
which skips existing fixed IDs. It does not claim a two-write transaction or idempotency.

Ledger reads fail independently. Retained rows are visibly stale and reused only for the
same selected provider. Operations use the existing store limit of 50 and pending events
100; feedback display takes 100 after the existing unbounded store read. This extraction
does not introduce database paging or virtualize the provider catalog.

| Shipped driver | Executable UI actions |
| --- | --- |
| Mock | Synchronous query and approved UI surfaces |
| HTTP / NativeRemote | Synchronous query and approved UI surfaces |
| MCP | Sync/async query and operation status with required configured tools; approved UI surfaces |
| All drivers | Cancellation, manual ingestion, feedback execution and provider push acknowledgement remain refused |

Stored enabled/health/capability/driver policy remains authoritative. Imported unsupported
claims do not grant execution. Missing tools, disabled providers and unavailable runtime
drivers remain denied. Workers and drivers remain disabled by default. The browser fixture
explicitly enables only shipped Mock/HTTP in its owned test host; application defaults do
not change. Accepted unsupported-feature fixtures are labelled synthetic display data.

Environment credential references preserve HTTP/MCP meaning without reading secret values.
Recognized legacy raw credentials are removed from UI snapshots and new persistence while
their key names remain available for migration notices. Unknown safe vendor JSON, limits,
capability/UI metadata and distinct interaction metadata round-trip. Unsafe Provider UI
URLs never reach hidden editor state or rendered attributes: only HTTPS or loopback HTTP,
without userinfo/query/fragment, is allowed. Iframe sandbox/referrer and external-link
protections remain intact. Generic notices/logs omit provider exception messages; logger
state records action/region, provider identity where relevant, and exception type.

## Sandbox, assets and desktop proof

The scenario owner supports initial loading, empty, populated, 103-provider catalog,
unavailable, partial, accepted query, unknown query and Provider UI variants. Read/save/
query/status holds are actual owner-port waits. Reset releases those waits and retains the
last four retired stores for inspection; an accepted old write changes its original store.
Fixtures include exact operation/context provenance, unmatched feedback and pending events.

Desktop composition retains PageScaffold/PageHeader, compact summary badges and a split
provider-list/detail editor. The first 1600 x 1000 viewport exposes provider choices,
selected identity and the start of the editor. The page owns vertical scroll; a large list
does not introduce a second scroll owner. No new dialog, mobile layout or component-library
redesign was introduced. Relevant open state is the action-outcome details/review control.

Inspected images include the original provider/Provider UI baseline,
`output/playwright/memory-ui/sandbox-providers.png`, `sandbox-provider-variants.png`,
`production-providers.png`, and `artifacts/memory-ui/published-providers.png`.
The expanded final browser run's `sandbox-unknown-review.png` was also inspected: the
open outcome details show the original Unknown query, disabled/busy review, blocked
duplicate query and intact provenance editor, with no overlay or clipping defect.
Normal views retain the same list/editor hierarchy and usable first viewport; provider UI
shows the actual built-in RCL, owned iframe/external link and distinct blocked states.
The published host computes 68px minimum provider rows and a two-column grid
(575.625px / 959.375px at 1600px), has no horizontal overflow, and loads Material Symbols
Rounded. Source and publish use canonical theme/font/scoped assets; there is no Web
ProjectReference. The standalone Production-environment published DLL needs no database.
`published-proof.json` records all asset/fixture HTTP responses as 200 and zero browser
errors. Source journeys also assert no browser/HTTP errors. The README records theme rebuild.

## Evaluated graph and development loop

Measurements used owned hosts and the same source-reference/parity/configuration mode
before and after. The Memory baseline was taken after S0 and before moving renderers.
Restore graph traversal rejects unresolved edges/cycles and inspects package/runtime/native
assets, including source replacements. Runtime assembly/public-type tests also include
negative forbidden-transitive and unresolved-edge cases.

| Host | Projects including root | Packages | Native asset entries | Watch entries |
| --- | ---: | ---: | ---: | ---: |
| Original Web | 143 | 140 | 30 | 4,453 |
| Extracted Web | 146 | 140 | 30 | 4,467 |
| Memory sandbox | 7 | 1 | 0 | 318 |

The sandbox's seven projects are itself, Presentation, UI, Contracts, Memory.Abstractions,
BaseLib and Common. Its only package is ASP.NET internal assets. No Application, transport,
EF, Web, composition or native runtime appears in that closure. Optional registered
third-party UI dependencies remain the composing host's responsibility and are not included
in this base-feature claim. The built-in panel and owned iframe fixture need none.

| Host | Startup to interactive (ms) | Razor visible, three samples (ms) | C# visible, three samples (ms) | Scoped CSS visible, three samples (ms) |
| --- | ---: | --- | --- | --- |
| Original Web | 57,838 | 2,890 / 1,892 / 1,902 | 860 / 1,792 / 1,968 | 7,153 / 7,187 / 4,817 |
| Extracted Web | 48,960 | 2,911 / 1,878 / 1,894 | 1,155 / 2,092 / 1,795 | 8,515 / 15,981 / 4,455 |
| Sandbox | 8,996 | 1,900 / 373 / 361 | 593 / 573 / 564 | 543 / 592 / 355 |

These are executed edit-to-visible samples, not a forecast or full-Web speedup claim.
Razor changed the actual summary label; C# changed an executed profile display projection
and triggered a read; CSS changed computed provider-row height. All source bytes were
restored in finally and owned watch process trees stopped. Full-Web CSS varied and was
slower in this after sample. Feature-specific JavaScript measurement is not applicable.
The baseline host used default disabled drivers, so its query screen is not successful
provider-execution proof; the separate production browser journey proves the enabled real
Mock/HTTP handler paths. The later blank-query refusal does not alter graph/watch/probed paths.

Local artifacts: `artifacts/memory-ui/{original-web,extracted-web,sandbox}-transitive-graph.json`,
`*-loop.json`, original `original-web-watch-list.txt` and after `*-watch-files.txt`.
The loop helper and restore-graph capture scripts are retained in that ignored directory.
Exact watch command: `dotnet watch --list --project <host.csproj> --configuration MemoryUiProof`.

## Current test selection and commands

Each selection derives its expected count from current source, builds during discovery,
then executes the identical filter against that assembly. Commands from repository root:

```powershell
dotnet test <owning-project.csproj> -c MemoryUiProof --list-tests --filter <filter> /m:1
dotnet test <owning-project.csproj> -c MemoryUiProof --no-build --no-restore --filter <filter> --logger "trx;LogFileName=<name>.trx" --results-directory artifacts/memory-ui /m:1
```

`artifacts/memory-ui/Invoke-Proof.ps1` enforces expected discovery before execution and
records `<name>-discovery.log`, `<name>-run.log` and `<name>.trx`. Test configuration is
`MemoryUiProof`; browser base URL is unset so fixtures own their hosts. Database selections
use `CANDOITALL_TESTS_POSTGRES_CONNECTION` for the task-owned loopback PostgreSQL 18.6 and
`CANDOITALL_TESTS_POSTGRES_CREATE_STRATEGY=WAL_LOG`, never the ordinary application database.

| Key / owning project under `tests` | Exact filter selection | Expected / discovered / executed | Current receipt |
| --- | --- | --- | --- |
| L: Components/CanDoItAll.Memory.UI.Tests | `FullyQualifiedName~CanDoItAll.Tests.Components.Memory` | 27 / 27 / 27 | `memory-light-verified`, pass |
| P: Components/CanDoItAll.Tests.Components | OR of `FullyQualifiedName~` each of `MemoryProvidersPageTests`, `MemoryProviderOperationsPageTests`, `MemoryProviderUiSurfacePageTests`, `MemoryProviderProfileEditorComponentTests`, `MemoryProviderProfileEditorRoundTripTests`, `MemoryProviderProfileEditorValidationTests`, `MemoryUiRefactoringCheckpointTests`, `MemoryContractCompatibilityTests` | 39 / 39 / 39 | `memory-pages-final`, pass |
| O: Integration/CanDoItAll.Tests.Integration | OR of `FullyQualifiedName~` each of `MemoryWorkspaceOwnerTests`, `MemoryProvidersApiIntegrationTests`, `MemoryOwnerPersistenceTests`, `MafMemoryResultDisclosureIntegrationTests` | 32 / 32 / 32 | `memory-integration-verified`, pass |
| R: Memory/CanDoItAll.Memory.Tests | OR of `FullyQualifiedName~` each of `MemoryOperationHandlerTests`, `MemoryMcpDriverTests`, `MemoryHttpDriverTests`, `NativeRemoteMemoryProviderDriverTests`, `MemoryProtocolContractsTests`, `MemoryFeedbackHandleSecurityTests`, `MemoryOperationResultExtensionTests`, `AgentMemoryProviderEndToEndTests`, `HttpMemoryProviderResponseLimitTests`, `McpMemoryProviderResponseLimitTests` | 70 / 70 / 78 | `memory-runtime-verified`, pass |
| C: Memory/CanDoItAll.Memory.Tests | `FullyQualifiedName~HostCompositionDependencyRemovalTests` | 6 / 6 / 6 | `memory-composition-verified`, pass |
| B: Playwright/CanDoItAll.Tests.Playwright | `FullyQualifiedName~MemoryBrowserTests` | 2 / 2 / 2 | `memory-browser-verified`, expanded cases pass |

R has 55 facts, 13 inline cases and two non-serializable MemberData rows at discovery;
each MemberData expands to five execution cases, giving 78. The API's existing InMemory
serialization tests remain API compatibility proof, not PostgreSQL proof. O's production
workspace tests and owner-persistence tests use actual PostgreSQL stores; B uses the real
route/host/PG and real HTTP driver with only the loopback provider response held. MCP
async/status tests use controlled transports, not a live external provider account.

Recorded failures remain failures: Scheduler failing-first was 10 failed/4 passed. Memory
baseline P (before four added compatibility cases) passed 35/35. Initial broad namespace
discovery found 54 instead of 35 and execution was aborted in favor of the explicit union.
Initial L failed one invalid fixture extension namespace, then passed 19, expanded to 26
and finally 27. Initial P failed unsafe-URL retained state and old placement expectations;
both were repaired and 35 passed. Added compatibility proof then exposed a test-only cast
from an interface-backed collection to an array; the fixture now owns a mutable array.
Initial O had a test-only internal mapper access compile error, then a missing explicit
Mock registration; after correcting those fixtures, its initial 10 cases passed. Initial
R expected 80 incorrectly; discovery 70 aborted execution, then corrected selection passed
78 expanded cases. Browser iterations corrected hidden details text, waiting for observed
save before read-back, and an unsupported ingestion button correctly absent by policy.
The third browser run passed both; expanded final proof adds initial loading, incomplete
numeric input, retired-store persistence and actual unknown-review busy state.

All final selections passed: **184 Memory cases**, plus **76 Scheduler prerequisite cases**,
zero skips. Seven direct builds also passed with zero warnings/errors: Contracts, UI,
Presentation, module, sandbox, Composition and Web. Command:
`dotnet build <project.csproj> -c MemoryUiProof --no-restore --no-dependencies /m:1 -v minimal`.
Dependencies were rebuilt by the current discovery commands first. Transcripts are
`artifacts/memory-ui/final-build-<project-name>.log`. No full-suite result is implied.

No unfiltered Stable or platform/live suite was run. This is bounded Memory UI/owner wiring,
not a release/merge, schema change, global test fixture rewrite or cross-platform runtime
change. Graph, composition, API, PostgreSQL, transport and Agent consumer selections address
the actual invalidation. The shared browser fixture option preserves its previous defaults;
only Memory opts in. Changes after a proof invalidate its affected selection, not every
unrelated assembly. No skipped/quarantined result is counted as closure.

## Validation matrix trace

Keys below refer to the exact commands, counts and artifacts above; one test can prove
several obligations. Source/publish/static receipts remain distinct from runtime proof.

| Obligation | Concrete observation and proof boundary |
| --- | --- |
| V-S0-01, V-S0-02, V-S0-03 | Scheduler review regression cases: held overlapping reads, successor Pending/Unknown, mismatch/failure/disposal and non-save receipt; S0 59/15/2 receipt |
| V-S0-04, V-S0-05 | Optional/required dependent validation, unrelated/raw issues, current light/page/calendar/control regressions; S0 receipt |
| V-S0-06 | Prior Plugins paths reviewed unchanged; no relevant implementation dependency changed |
| V-BD-01 | Source/consumer inventory above, original graph/watch/screenshots and 35 baseline page cases |
| V-BD-02 | L MemoryBoundaryTests, evaluated 7-project closure and negative forbidden/unresolved traversal |
| V-BD-03 | L Seven_tabs_use_real_children; P real route; B complete production/sandbox tabs |
| V-BD-04 | P MemoryContractCompatibilityTests, O exhaustive numeric status and API round-trip, R protocol/handle consumers |
| V-BD-05 | P profile round-trip/validation and compatibility tests for safe JSON, defaults, credential references, legacy removal and interaction metadata |
| V-BD-06 | L MemoryExtensionLifetimeTests and public-type guards; P registered surface; B owned UI fixtures |
| V-BD-07 | Product/Components/Stable/CI inventory and L current build-backed discovery |
| V-ST-01 | L Refresh_and_tabs / Incomplete_numeric_text; B focused unblurred typing, nested transport/tag retention |
| V-ST-02 | L captures all query fields and profile transports; O Real_query_captures_input_before_guard_await through real handler |
| V-ST-03 | L Query_captures_all_fields, Late_read_success_or_error, Same_ID_profile_replacement; B held HTTP result after selecting B |
| V-ST-04 | L Missing_exact_selection and Empty_refresh; B missing/empty/repopulated scenarios |
| V-ST-05 | L Duplicate_save_query_and_status, independent destination and original receipt locks |
| V-ST-06 | L Save_captures_destination / Page_refresh; O PostgreSql_saved_destination with committed write and failed read-back |
| V-ST-07 | L Unknown_save / Unknown_query; B actual open unknown review, disabled pending control and no replay |
| V-ST-08 | L Disposed_view original-store write; B reset while save is held, retired-store inspection |
| V-ST-09 | O PostgreSql_saved_destination asserts original and new exact IDs; L destination capture |
| V-ST-10 | O Demo_second_write_fault: first profile durable, read-only review, retry skips it |
| V-ST-11 | L Page_refresh counters versus explicit status; O real query ledger remains one operation |
| V-ST-12 | O Profile_generation_change and Failed_feedback_read; L Changed_database_origin; R handle/provenance and O MAF disclosure |
| V-CP-01 | C zero-provider registration; P empty page; L/B explicit-only demos |
| V-CP-02 | O real healthy Mock handler/ledger and B real production synchronous query |
| V-CP-03 | R MemoryMcpDriverTests / MemoryOperationHandlerTests accepted/status contracts; L/B synthetic accepted rendering separately labelled |
| V-CP-04 | O Imported_unsupported_claims across four drivers refuses ingestion/feedback/ack/cancel before ledger/dispatch; P/B unavailable controls |
| V-CP-05 | P validation and R policy/tool/driver tests; C explicit registration/defaults |
| V-CP-06 | L registered exact parameters and A-B-A/disposal; P/B built-in panel |
| V-CP-07 | L five unsafe URL cases, P projector/page states, B actual protected iframe/external link |
| V-CP-08 | O exhaustive status projection; R accepted/feedback handle security; L exact accepted result and unmatched sandbox ledger |
| V-CP-09 | P legacy credential snapshots and generic exception notices, L unsafe URLs, O safe partial failures; artifact secret scan |
| V-BR-01, V-BR-03 | B seven-tab controls, actual focus/raw input, held query/status/save and no late tab activation |
| V-BR-02 | B real owner/PG save, Mock query, held real HTTP transport and exact persistence read-back |
| V-BR-04 | B UI variants, refusal, missing/partial/unavailable/reset and zero browser errors |
| V-BR-05 | Published standalone DLL, source/publish network checks, computed geometry/font and inspected images |
| V-BR-06 | Executed three-sample Razor/C#/CSS loop, graph/watch inventories; JS not applicable |
| V-CL-01 | Direct root builds and current selection/discovery/execution receipts |
| V-CL-02 | Full proposed-tree scan, reviewed added/stale baseline delta, final no-write enforcement |
| V-CL-03 | Documentation/evidence/package/secret gates and exact owned-resource cleanup |
| V-CL-04 | Explicit impact/Stable/platform/live widening decision above |

## C# Architecture Gate Result

Status: **Pass**.

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | No blocking architecture finding remains | L/P/O/R/C/B receipts and source-mode graph above | None |

Responsibilities are cohesive: renderer owns DOM; presentation owns lifetime/admission;
production owns mapping, authority and effects; the scenario store substitutes at the same
owner port.

### Dependency direction

Contracts reference Abstractions only. UI references Contracts/components; Presentation
references UI; concrete module and sandbox compose the same controller. Graph/public-type
checks exclude backend requirements and reject cycles/unresolved edges. Registered
extensions remain explicit composition choices.

### Partial-class policy

No production partial-class split, service locator, generic event bus or second controller
was added. Registration uses normal DI at the production composition boundary.

### Testability proof

27 independent light cases exercise the actual shipped seam. Real PostgreSQL/route/driver
and browser proof separately establishes production behavior; synthetic fixtures do not
stand in for owner authority or unsupported provider execution.

### Closure decision

The complete Memory slice meets its architectural boundary and behavior obligations.
Future implementation of a currently refused provider feature needs separate runtime/owner
authorization and proof. No such feature or third module is part of this extraction.

## Repository gates and cleanup

The full proposed-tree portability scan included new untracked files: 6,966 files,
32,162 total findings, not truncated. Every one of the 54 added and 46 stale protected
findings was reviewed against current/original excerpts. The deltas are moved MCP
contracts/editor markup, pure defaults/captures, three synthetic MCP fields, relocated
case-insensitive URI scheme comparisons, one README capability sentence and two scanner
matches on the sandbox README's PowerShell code fence. No OS assumption, shell invocation,
machine path or secret resolution was added. IDs remain ordinal; URI scheme matching
remains correctly case-insensitive. Stale allowances belong to the removed original paths.

The baseline diff exactly matches those reviewed fingerprints/counts, with unchanged
patterns, scope and policy. Final enforcement **without `--write-baseline` passes 15,139
reviewed findings**. Tooling self-tests passed 6 baseline and 4 secret-scanner cases.
Receipts are `portability-final-{scan,delta,enforce}.log`, `portability-final.json`,
`portability-final-reviewed-excerpts.txt` and `portability-baseline.diff` under
`artifacts/memory-ui`. Commands are the canonical [portability procedure](../testing.md#portability-static-gate),
with `--tracked-only` omitted so new files were included. The one reviewed baseline refresh
was followed by diff inspection and no-write enforcement.
After the final whitespace cleanup and staging every new source file, a fresh full tracked
scan covered 6,967 files and 32,168 findings, without truncation. Its
`portability-closure-{scan,enforce}.log` again records no-write enforcement passing all
15,139 allowances; no second baseline refresh was needed.

Both handoff integrity validators passed; their utility suites passed 11 and 14 cases.
These are package integrity checks, not product tests. Documentation evidence checks
passed 9 cases and the canonical documentation validator passed all 272 maintained Markdown
files. Its initial failure identified the new light test project's missing README; that
README was added and the validator rerun successfully. `git diff --cached --check` is clean.
The final artifact secret scan covered 165 text files, including published text with a
50 MiB file limit, with no findings, oversized-text skips or unreadable-text skips;
browser artifacts also pass. Reports are `secrets-closure.json` and `browser-secrets.json`.
Source fixtures test raw credential, provider exception and unsafe URL negatives explicitly.

Owned PostgreSQL 18.6 container
`3bc63dfea630df91c76632474fea45836ffed1dd35a5ff0a9329c8219d6169a8`, label
`candoitall.task=memory-ui-059f4d`, loopback port 50628, had no remaining fixture databases
before its exact identity/label were checked and it was stopped/auto-removed. Recorded
watch/publish PIDs 7988, 22896, 33376 and 5892 and their listeners are gone. Browser fixtures
dispose their own hosts. Sibling HEADs and clean source states were rechecked unchanged.
The ordinary app/database on port 5032 was never used or stopped. No push, merge, deployment,
ordinary app restart or unrelated Docker cleanup occurred.

Local artifacts remain ignored; no runtime log, binary, screenshot or credential enters
the source commit. Source handoff files remain unchanged. The signed Memory commit and
clean final status are reported on delivery, with the local receipt in
`artifacts/memory-ui/closure.json` after commit.

## Bundle closure

| Work unit / raw requirement | Closure | Evidence |
| --- | --- | --- |
| S0: reproduce/repair SC-R1 and SC-R2 first | Solved | Signed Scheduler commit and current 59/15/2 receipt |
| M1: complete inventory and original baseline | Solved | Source/consumer map, 35 baseline cases, original graph/watch and rendered observations |
| M2: light production boundary and compatibility | Solved | Shared real renderer/controller, 7-project sandbox, negative guards, production builds and consumer tests |
| M3: state/requests/effects and shipped refusals | Solved | Light plus actual owner/PG/transport/Agent proof, bounded receipts, known/unknown distinctions, unchanged executable limits |
| M4: complete backend-free sandbox and assets | Solved | Seven-tab controlled scenarios, browser focus/lifetime/recovery, published host and inspected desktop images |
| M5: validation, measurements, maintained documentation and cleanup | Solved | Current selected tests, builds, graph/watch/edit samples, portability, documentation/evidence/secret checks and owned-resource cleanup |

Final bundle validation: **Pass**, with no blocked or partially solved work unit. Live
external provider accounts and new refused driver features were not run or implemented;
they are outside this assignment, not missing proof for a claimed capability. The remaining
bounded-history/paging and assembly compatibility limits are stated above. No downstream
module was started.
