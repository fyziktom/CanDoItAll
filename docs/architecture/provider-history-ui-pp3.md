# Provider Request History PP3

Execution is in progress. The complete UI family has moved and its focused component checks pass;
source/publish browser and development-loop checks passed. Native three-instance and final closure
gates are still being collected. This record does not claim application release readiness or remote dependency delivery.

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

The final browser selection discovers 11 cases: two History sandbox and nine native three-instance
cases. The native consumer cases now assert canonical owner references, exact global/provider attempt
identity and explicit content. Managed credentials remain refused Agent/Workflow content; their
authorized reads use the native local operator circuit. Production UI, module, Web and sandbox
builds passed. Components, CodeAnalytics and dotnetwatch
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

## Remaining closure

Final native work must use rebuilt central/client-a/client-b images from the final source pair and
cover exact default/non-default routing, History identities and content, public/limited denial,
credential rotation, canonical Agent/files, Simple Chat and Workflow/TestLab evidence. Historical
PP2 native and qualified Stable results remain historical, including the 16,144/16,159 checkpoint.

One final frozen Stable checkpoint is selected because the shared BaseLib lifecycle change affects
consumer teardown and this slice changes test aggregate/CI ownership. It is not repeated at each
stage. The owned PostgreSQL 18.6 test fixture uses disk-backed storage with measured headroom;
the previous one-GiB tmpfs fixture is not reused. Portability delta review, no-write enforcement,
documentation/evidence checks, safe export, verified signed application checkpoints and exact
owned-resource cleanup remain required before final closure.
