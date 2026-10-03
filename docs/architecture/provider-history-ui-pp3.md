# Provider Request History PP3

The complete UI family has moved. The first native campaign exposed two bounded defects, repaired
with failing-first regressions and source/publish browser proof. Rebuilt native images now have
passing attempts for the backend vector and all nine native browser journeys, including canonical
content, denial and credential rotation. The one frozen Stable checkpoint remains in progress.
This record does not claim application release readiness or remote dependency delivery.

## Boundary and behavior

Global `/agents?tab=request-history` and saved-provider History retain their existing production
host. It resolves the original `IProviderRequestHistory`, supplies the scope and clock, subscribes
to authentication/profile changes, and retires the old activation before replacing it. The full
workspace, filter form, search state, result actions and metadata/content dialogs live in the
existing `src/UI/CanDoItAll.AgentFramework.UI/History` family. No new production project or copied
History contract was introduced. Canonical sources, authorization, persistence, cursor protection,
retention, shared relay and PP1/PP2 implementations remain their existing owners.

The filter draft is separate from its applied query. Search validates the real EditContext and
freezes the UTC interval before awaiting. Paging uses only that applied query and server cursor;
the previous-page trail remains bounded to 32. Advanced inputs remain mounted while collapsed,
including invalid raw page-size values. Exact model and external-reference identities are retained.
A fixed provider cannot be widened by a raw provider filter.

`HistoryViewOrigin` is a local presentation identity, not authorization. Result callbacks capture
it when rendered, so an old Cancel, Clear, page or Details action cannot target another read even
if the same entry ID returns. Detail and content callbacks also retain their owning activation,
metadata and exact canonical reference. Every read still uses the native authorized port. Metadata
does not prefetch content. Denied/stale reads clear the disclosed data; close, scope replacement,
context retirement and disposal clear owned references and callbacks. Pending cancellation sources
remain usable until their asynchronous reads unwind. History cancellation does not cancel canonical
agent, chat or workflow operations.

## Tooltip prerequisite

The bounded correction belongs to `CanDoItAll.Components`, signed commit
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`, verified with fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`. Tooltip cancellation is tolerated during retirement;
unexpected active failures remain observable. Delayed imports and queued operations retire once,
and a departed target cannot close its successor's tooltip. The earlier Dialog repair is preserved.

The consuming Web assembly reports `0.3.0+b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`.
The commit is local and unpushed. CI resolves its matching Components branch independently; this
record does not describe the local correction as remotely available.

## Independent proof and integration

The existing AgentFramework sandbox renders the exact full workspace. Its per-workspace fixture
records page, metadata and content reads separately and offers 17 explicit scenarios, including
negative and delayed reads, canonical owner choices and two independent workspaces. It registers
no production module, History implementation, EF, vault or runtime service.

The new light test project references that sandbox and rehomes the existing search-state/seam tests
without duplicate executions. It is included in Components/Stable solutions and all three component
CI selections. Production-host notification tests remain in `CanDoItAll.Tests.Components`. The
source/publish browser helper is shared with PP1, whose two browser cases remain selected.

The evaluated project sets remain unchanged: sandbox 16 and Web 168. The sandbox watch inventory
grew from 647 to 657 files, adding the complete History family and its scenarios; Web changed from
4,656 to 4,658. These are structural measurements, not a measured Web speedup.

The parity sandbox used SDK 10.0.303, Debug and polling watch at 1920×1080/scale 1. Initial build
took 15.66 seconds, readiness was observed at 38.751 seconds from launch, and browser navigation
through interactive hydration took 573 ms. Three consecutive Razor edits became visible in
2,554/1,007/1,027 ms; C# in 985/496/378 ms; scoped CSS in 1,940/1,328/1,291 ms. These include browser
observation overhead. Runtime PID 61268 and watch iteration 1 remained constant. Razor/C# advanced
the metadata-update generation; CSS used stylesheet refresh. No restart was required. All three
probe files were restored byte-for-byte and the owned watcher/process tree was stopped.

Retained setup failures include a wrong initial working directory and an incorrect initial browser
route. One C# observation expired before the orchestration tool delivered its edit; the corrected
consecutive three-sample series above followed it. No pre-extraction warm-edit timing was measured,
so these numbers do not claim a before/after speedup. This slice owns no JavaScript edit.

## Native findings and bounded repairs

The first signed application checkpoint is `5e53e5aaea2f3411cdc1f59eaccc52924f68f382`.
Its fresh three-instance backend vector passed 19/19 and its native default/import setup passed 1/1.
The consumer union passed media/vision but failed four History assertions: Agent/files and Simple
Chat could leave visible Details actions stale; both Workflow variants returned `Unavailable` after
an authorized content request. These results are retained as failures, not credited to the repaired image.

The DataGrid renders cells through registered column templates. Capturing the History origin outside
that deferred template could retain the preceding render's identity. Each row now captures its origin
when its cell is rendered. Old callbacks still retain their original identity and are rejected; the
visible row can open details after a prior dialog closes. Real source and published reopen regressions
first failed 0/2, then passed 2/2. No shared DataGrid or Components change was required.

The existing owner contract permits linked Workflow `PrimaryEvidence` content. The final persistence
recheck incorrectly required `ContentOwner` for every source. It now uses `CanReadContent` and requires
the exact same role to remain stored. This preserves the authorization policy and refuses lineage,
non-Workflow primary evidence and a changed role. Five native SQL regression cases first passed 4/5,
isolating Workflow primary evidence. There is no schema, HTTP or permission-policy change.

The frozen Stable Components assembly found 12 policy-panel test setup failures because the neutral
History fixture no longer registers the production profile notification service. The policy test host
now registers that service explicitly. The 13 policy and 26 context cases passed together, and again
after the production corrections. The broad run remains the original frozen attempt; it is not restarted.

Direct repaired UI, persistence, Web and sandbox builds passed without warnings. The independent
History family passed 84/84 after the fixes. The exact native authorization/source/recheck selection
discovered and passed 50 cases, including all five owner-role regressions.

The final application image was rebuilt from signed application
`17452fc2c892de1edb62cbdbb284b5a44b4536b2` and Components
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`. Its first backend attempt failed the existing Responses
first-data-to-completion interval check; multiple frames and the terminal event were present. The
harness retained only the Boolean result, so the numeric latency and cause are unproven. Its supported
resume verified unchanged frozen inputs and images and passed all 19 scenarios. Neither timeouts nor
the oracle changed. The first attempt remains a failure.

The rebuilt image's first five-consumer run passed Agent/files, accepted Workflow and media/vision;
Simple Chat and incomplete Workflow timed out opening metadata after projection polling. The helper
could accept the previous table and enabled Search before the replacement query reached the browser.
A controlled native check queued Search and an old row together: all three old actions were correctly
refused, while settled actions and three explicit-clear controls opened. The ordinary warm sequence
also passed three times, so the evidence does not claim every unsynchronized sequence fails. The
test now observes Clear completion before polling again and observes metadata closure. All four
canonical consumer cases then passed on the unchanged production image.

The final three-case UI run passed PP2 two-circuit conflict/merge and source disable/retire/reimport.
History paging exposed the same test assumption: the previous caller's one-row page already satisfied
the next caller's page-count assertion. A native control confirmed both captions were identical and
that the old queued action was refused while the requested caller opened normally. Two assertions now
wait for the replacement caller row. Search replacement and cursor-reset assertions remain intact;
the one affected History paging/denial/rotation case then passed separately. These synchronization edits
change test code only; they do not invalidate production images or unrelated passing journeys.

## Current attempts

Ignored working evidence is under `artifacts/provider-history-pp3/20261003`. Private database
credentials and native fixture state remain under `.artifacts`, outside tracked documentation.
All browser evidence uses synthetic content at 1920×1080 and scale 1.

| Selection | Result | Qualification |
| --- | --- | --- |
| Tooltip failing first | 10 passed / 7 failed of 17 | Reproduced against unchanged production |
| Tooltip corrected | 17/17 | Expanded lifecycle family later has 21 cases |
| Bounded Components consumers | 57/57 | Initial 56/57 exposed three reviewed source fingerprint deltas; refreshed and reran without refresh |
| Native pre-extraction visual smoke | 1/1 | Both History entries and nested dialogs; seeded visual support, not producer proof |
| First independent History run | 56/57 | Test attempted inspection through disposed bUnit wrapper |
| Expanded independent History run | 83/84 | Disposal assertion needed repository's awaited disposal helper |
| Focused disposal follow-up | 1/1 | Correctly awaited real component disposal |
| Confirmed independent History | 84/84 | 26 search-state, 17 seam, 14 workspace, 18 scenario and 9 filter cases |
| Production context host | 26/26 | Includes nine profile/auth/scope × pending/displayed evidence cases |
| Initial source/published browser union | 4/4 | Two complete History scenarios cases and two retained PP1 editor cases |
| Final History source/publish | 2/2 | Rebuilt after the two-workspace action whitespace correction |
| First native backend/defaults | 19/19 and 1/1 | Signed initial image pair; separate selections |
| First native consumer union | 1/5 | Media/vision passed; four History assertions failed as described above |
| Native owner-role reproduction | 4/5 | Existing Workflow readable role failed the final SQL recheck |
| Source/publish reopen reproduction | 0/2 | Real deferred-template callback defect |
| Repaired independent History | 84/84 | Same stale-intent and retirement controls retained |
| Repaired production policy/context hosts | 39/39 | Includes all 12 failed policy setups and their native Settings host case |
| Repaired source/publish History | 2/2 | Includes repeated row reopen and all 17 scenarios |
| Repaired native authorization/source/recheck | 50/50 | Existing authorization and deletion controls plus five exact readable-role cases |
| Final image backend, first attempt | 14 passed / 1 failed / 4 pending | Existing Responses timing threshold; numeric cause not recorded |
| Final image backend, same-image resume | 19/19 | Native runner verified all frozen image/source reuse; original failure retained |
| Final image native defaults | 1/1 | Exact default/non-default imported identities and routing |
| Final image initial consumers | 3/5 | Agent/files, accepted Workflow, media/vision passed; two metadata polling timeouts |
| Final image synchronized canonical consumers | 4/4 | Agent/files, Simple Chat default/non-default, accepted and incomplete Workflow |
| Final image initial final UI | 2/3 | PP2 concurrency/source lifecycle passed; History caller-page handoff timed out |
| Final image synchronized paging/denial/rotation | 1/1 | Exact caller rows, pagination, limited denial, revoked-key 401 and replacement native turn |
| Anonymous native History | Passed | Actual startup confirmation, lazy mount, explicit denial, zero rows/details/content |

The full browser selection discovers 11 cases: two History sandbox and nine native three-instance
cases. Every distinct native case has a passing final-image attempt: defaults 1, media 1, canonical
consumers 4, PP2 concurrency/source lifecycle 2 and History paging 1. This is a reviewed union of
attempts, not a claimed single 9/9 run; overlapping attempts are not added together. Managed
credentials remain refused Agent/Workflow content; their authorized reads use the native local
operator circuit. Production UI, module, Web and sandbox builds passed. Components, CodeAnalytics and dotnetwatch
MCPs are unavailable in this session; evaluated MSBuild, inspected source, CLI discovery and real
browser evidence provide the available proof.

## Architecture and desktop review

The implementation boundary gate passes: the existing neutral port is the only read capability;
the production module retains context wiring, while query state and rendering are independently
tested in the light 16-project graph. There are no new production references or cycles, service
locators, copied owner contracts, persistence or authorization implementations. Razor code-behind
partials follow the existing component convention. The additional native test partial groups one
shared cross-consumer History assertion; it is not a production architecture boundary.

Normal, two-workspace and long-content screenshots were inspected in source and published modes.
Search remains the primary action and More filters is supporting content. Result rows own the
document's vertical scrolling; the wide detail/content dialogs bound their own body, with the
content footer visible. Long read-only text scrolls inside its text area. The two-workspace desktop
case exposed Details wrapping onto two lines; scoped button whitespace now keeps the action intact
(38 px high). Source/publish browser checks passed again after that bounded CSS correction.

Five final native canonical content screenshots were inspected: Agent/files, both Simple Chat
model choices and both Workflow outcomes. Their metadata and content labels retain the distinction
between exact request identity and bounded conversation/run context. All content footers are visible.
Six further native screenshots cover paging, limited denial, rotation, PP2 conflict, source refusal
and disabled source state. All contexts are 1920×1080 at scale 1; taller screenshots are full-page
captures of that viewport, not another responsive target.

## Final source and native runtime

The final executable application source is `17452fc2c892de1edb62cbdbb284b5a44b4536b2`, Components is
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`, and unchanged FileTools is
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. The source fingerprint is
`d7f2f5e983c3829dbcfe992af5695ab04040febbabd070398674fb4874ba72d9`.
The immutable app image is
`sha256:1467d0b4e03b3c1cf5cf1f62438d6e1d1837381feef6d55bb78f41f53c8ad40c`;
scripted external upstream is
`sha256:c1ab43e00468a481fd4c4fc27121776e2577628113989bb0d28148e8c78275f7`.
The fixture uses host SDK 10.0.303, Docker SDK 10.0.302/runtime 10.0.10 and configuration
`HistoryPp3Native`; browser follow-ups use `HistoryPp3Followup`. Later changes are confined to two
browser-test files and this report. Their source diffs and exact rebuilt test DLL hashes
are recorded separately; the unchanged production image is not relabelled with a later commit.

`native-final-binaries/loaded-binaries.json` records the BaseLib, UI and persistence hashes copied
from the exact running central container. Docker assembly versions omit Git suffixes, so provenance
comes from the frozen build inputs, immutable image and observed binary hashes. The local Web build's
BaseLib version contains the signed Components revision. No remote publication, push or merge occurred.

## Native log qualifications

The final seven-container capture has no CircuitHost, RemoteRenderer or unhandled-exception signal.
It is not an error-free log claim. Protocol refusals, incomplete Responses and explicit History
denials match exercised negative cases. Two fail-closed token-validation cancellations at
15:17:00 UTC match the limited credential immediately after its denied browser context closed;
the source uses request cancellation. Context shutdown is an inference: those records contain no
circuit ID. Browser/request oracles remain empty and explicit revocation/rotation assertions pass.

Client-a also emitted 30 retryable AgentConversation maintenance warnings and 24 canceled
ReadJournal/acknowledgement stage warnings. This existing bounded maintenance limitation was also
observed on the first image and is not repaired or suppressed here. Partial coverage remains visible;
the native consumers eventually verify the exact expected identities and content. Existing EF
mapped-then-ignored PromptArtifact/PromptVersion warnings and unordered row-limit warnings remain;
the latter have no recorded query/circuit attribution. DevelopmentOnly vault warnings qualify the
owned development fixture. PostgreSQL's six initial migration-history probes and twelve deliberate
cross-database permission denials are classified against fresh startup and the isolation checks.
Exact counts, times, container/image IDs and private log hashes are in `native-final-log-review.json`
and `native-final-server-log-receipts.json`. No unrelated server log was treated as current proof.

## Remaining closure

Final native work is complete on rebuilt central/client-a/client-b images from the source pair above.
Historical PP2 native and qualified Stable results remain historical, including 16,144/16,159.
All seven exact owned native containers are stopped; volumes, images, networks and private evidence
are retained. The three owned public-denial relays were removed and the measured watcher was stopped.
Ordinary retained instances were untouched. The separate owned Stable database is still needed.

One final frozen Stable checkpoint is selected because the shared BaseLib lifecycle change affects
consumer teardown and this slice changes test aggregate/CI ownership. It is not repeated at each
stage. The owned PostgreSQL 18.6 test fixture uses disk-backed storage with measured headroom;
the previous one-GiB tmpfs fixture is not reused. Portability delta review, no-write enforcement,
documentation/evidence checks, safe export, verified signed application checkpoints and exact
owned-resource cleanup remain required before final closure.

The ongoing frozen run has also found one pre-entry Workflow OpenAPI documentation gap: descriptions
are missing for `WorkflowProviderOption.modelCatalog` and `isSourceManaged`. Those fields were added
in `ddba548adf` before PP3 entry. Git blob comparison confirms their model, route, documentation
transformers and coverage fixture are unchanged. This actual failure remains an independent
Workflow documentation follow-up; no coverage assertion or owner source is changed to make it pass.
It does not invalidate the History UI proof and does preclude describing the broad run as all green.
