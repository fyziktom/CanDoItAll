# Validation matrix — TestLab closure and Plugins extraction

These are behavioral obligations, not a required number of tests/classes. Combine related cases coherently. Current `docs/testing.md` and CI own command syntax, traits and gate expansion. The reviewer has not executed these product tests.

## Entry and evidence rules

Read current sources and select tests before editing. Use Code Analytics MCP when available, otherwise local references plus evaluated build metadata. List actual changed assemblies and consumers, including public DTO serialization/API and plugin integration consumers. A test does not become irrelevant just because its name lacks Plugins.

For each new or changed filter, record the source-derived expected case count (including theory rows), run `--list-tests`, compare actual discovery, then execute that same selection on rebuilt current assemblies. Zero discovery, unexpected skips and mismatched counts are not green. `--no-build --no-restore` is valid only after the owning assembly/dependencies have been refreshed for the current source/configuration.

Use the test owning projects rather than launching an unfiltered whole-suite loop. Example syntax after deriving the actual expected count:

```powershell
$project = './tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'
$filter = 'FullyQualifiedName~PluginsPageTests'
$configuration = 'PluginsUiProof'
dotnet test $project --configuration $configuration --list-tests --filter $filter /m:1
# Compare discovery against the case count derived from the current source.
dotnet test $project --configuration $configuration --no-build --no-restore --filter $filter --logger 'trx;LogFileName=plugins-page.trx' --results-directory artifacts/plugins-ui /m:1
```

The reviewed source contained seven Plugins page Fact journeys before its helper section. That observation is not discovery and must not be used to force an incorrect current expectation. Use current repository support to match test configuration and child browser outputs; do not reuse a stale fixture executable from another configuration.

## S0 — preceding slice acceptance

| ID | Behavior to retain | Suitable evidence |
| --- | --- | --- |
| TL-1 | A party change during committed delayed read-back settles exactly that save, retains newer input/context/IDs and does not replay | Existing real-form `TestLabSandboxReviewTests` regression, with actual async completion |
| TL-2 | Known global/omitted-option party resolves exactly; missing/unknown/failure states are not fabricated as success | Existing real selector and fallback tests, plus positive production-session fallback |
| TL-3 | Unknown notification title is accurate and replay lock survives refresh/project changes | `TestLabNotificationTests`, observing real notification service |
| TL-4 | Existing accepted browser journey still has a target-bound Pending → Saved observation, one fake write and real production receipts separately | Inspect current `TestLabBrowserTests`; execute when no verifiable current result exists or drift invalidates it |
| TL-5 | No inherited claim that S0 shell/115 extraction cases just reran | Source/command/result ledger distinguishes current S0 checks from historical receipts |

Reference current receipt: light 38, session 29, host notification/reconciliation 7, browser 3 = 77 cases. This is implementer-reported at the review head, not independently rerun. Select narrowly for S0, expand if a regression or drift requires it, and then continue to Plugins. No TestLab product change is required when the correction is intact.

## Dependency, contract and real-renderer proof

| ID | Required outcome | Proof layer |
| --- | --- | --- |
| BD-1 | RCL and sandbox evaluated reference/package/public-type closure contains no concrete module owner, EF, Security/vault implementation, workflow runtime, concrete plugin or Web composition | Evaluated MSBuild/NuGet graph plus traversal guard; negative forbidden and unresolved-edge fixtures |
| BD-2 | Justified Plugins/AgentFramework/Infrastructure abstractions are distinguished from implementations | Document allowed closure and actual dependencies, not a blanket name-prefix ban |
| BD-3 | All six sections, tree/header and package dialog render through extracted real components, including deferred children | Light component tests and browser; no stub child or host-supplied heavy RenderFragment replacing real content |
| BD-4 | Moved public contracts keep IDs, namespaces where retained, JSON/defaults, enum values and API meaning | Focused serializer/API tests using production options; rebuild actual consumers |
| BD-5 | Sandbox starts without database, real plugin loading, OAuth network, secret vault or production service registration | Owned startup/browser process with backend configuration absent; inspect actual registrations/assets |
| BD-6 | Styles, fonts, icon URLs, scripts, overlays and package/static images are present in source and supported publish modes | Successful HTTP responses plus computed-style/geometry/dialog observations; checked fixture icon assets |
| BD-7 | New projects are reachable in the proper product/test/CI graphs without root-policy hacks | Project/solution/CI inspection and current discovery; no tests added to product solution |

## Deterministic session, editor and read tests

| ID | Required cases | Assertions |
| --- | --- | --- |
| ST-1 | Change tabs, select B then return A, manual refresh and unrelated grant/package action with unsaved settings | Intended drafts/validation survive; explicit reset alone retires the selected editor; no global reconstruction |
| ST-2 | Real text/JSON/number/URL/multiline input before blur, then tab unmount or late refresh | Raw unfinished value retained, schema errors retained/recomputed for correct field, no coercion of invalid input to a valid value |
| ST-3 | Save dispatch, then newer name/enabled/configuration edits while owner/read-back waits | Submission remains immutable; accepted unchanged values merge; newer values stay dirty; no whole-editor lock used as workaround |
| ST-4 | First save succeeds with actual new ConnectionId; secondary settings/catalog/log read fails | Same ID retained before read, saved-with-warning state, refresh retry has zero writes, later explicit save updates the same connection |
| ST-5 | Known refusal, genuinely unknown outcome, and older accepted receipt followed by newer refusal/unknown | Refusal remains editable as appropriate; unknown blocks blind replay; old refresh cannot falsely resolve later uncertainty |
| ST-6 | Same target double click/direct method dispatch; different independent target during pending save | One admitted write for the first operation; independent target may progress; stale completion never clears successor busy state |
| ST-7 | Recreated/removed descriptor or newly selected persisted connection with same key | Explicit lifetime transition; no ID or accepted values patched onto another identity by key/name/position |
| ST-8 | Catalog/manual refresh A and newer selection B; delayed success/error/cancellation including A → B → A | Latest user intent owns selection and section; stale result/status/finally suppressed |
| ST-9 | Installation/runtime log requests for A, B, All with out-of-order completion and one failed stream | Data and state remain scoped; one stream failure does not erase the other; bounded query count and Take retained |
| ST-10 | Partial settings/OAuth/package/restart failure | Distinct loading/empty/unavailable/stale states; unrelated information/actions remain available when valid |
| ST-11 | Dispose or reset with noncooperative pending read/write and queued UI callback | Observed completion; no callback into disposed host, no disposed-token access, no post-retirement pop-up; admitted owner result is not undone |
| ST-12 | Many plugins and repeated renders/typing/section changes | Bounded read counts, no per-input database calls, no full-catalog N-way reload after every local mutation, no unbounded session cache |

Use controlled completion gates or test clocks, never arbitrary sleeps as synchronization. Tests asserting lack of a second effect must await the event/operation itself, not merely enqueue a synchronous bUnit Click. Follow current `cut.InvokeAsync`, disposal and async test helper guidance.

## Typed grant and lifecycle behavior

| ID | Required cases | Assertions |
| --- | --- | --- |
| GR-1 | Start Grant/Deny/Revoke through actual buttons with held owner operation | Actual rendered row is busy; handler and renderer use the same typed identity |
| GR-2 | Repeated/conflicting decisions for one grant | One active mutation or documented serialized intent; no independent contradictory writes because action strings differ |
| GR-3 | Same capability/recipe with different scope kind/key, or another plugin | Independent row is not falsely blocked/updated; request retains full original identity |
| GR-4 | Missing/denied/revoked capability, missing required recipe grant, disabled/uninstalled plugin | Existing real backend decision is preserved; UI hints do not grant authority |
| GR-5 | Enable/disable or save/OAuth actions sharing actual identity | Conflict policy explicitly tested; unrelated editors stay editable |
| GR-6 | Grant result succeeds then refresh fails | Actual decision/receipt retained, read-only refresh and no unintended replay |

Do not assert new database concurrency enforcement for existing tokens. If a bounded owner change actually adds/changes enforcement, record its compatibility impact and widen owner/API tests rather than smuggling it in as a renderer move.

## OAuth, upload and restart — effect-specific proof

| ID | Required cases | Evidence |
| --- | --- | --- |
| EF-1 | Clean saved valid connection + allowed backend grant starts OAuth; dirty/invalid/missing/denied states do not dispatch | Session + real settings controls; positive/negative real owner tests |
| EF-2 | Delayed Start, then change selection/connection, dispose or reopen UI | Correct admitted server result retained separately; no stale popup/focus/reopen or false Connected status |
| EF-3 | Start created session/connection but browser opening fails, and start refusal after a possible connection creation | Truthful stage/outcome with explicit recovery; passive refresh never creates another session; no claim that every Failure means no effects |
| EF-4 | OAuth callback/return path, declared scopes, PKCE/state/vault behavior, disconnect | Retain existing actual owner/HTTP tests, fake only external provider transport; no live user credentials |
| EF-5 | Real small valid ZIP selection using production InputFile and configured owner limit | Bounded stream reaches actual installer; owner-generated staging and original manifest/archive validation retained |
| EF-6 | Invalid/oversized ZIP, malicious-looking filename/path entries and read failure | Existing owner safeguards tested; no arbitrary filesystem write, whole-file UI buffer or secret-bearing exception notice |
| EF-7 | Second file selection, close/reopen, navigation/dispose while browser stream is still read | Documented protected input lifetime or bounded staging/cancellation; stream/temp cleanup; predecessor cannot overwrite successor status |
| EF-8 | Package already staged/extracted/installed, then metadata/log/refresh failure | Real known stage/identity preserved; not automatically reinstalled; truthful pending-restart/uncertain state |
| EF-9 | Explicit restart request and duplicate request | Correct real service invoked on an owned fixture only; bounded await of its stop signal; sandbox never stops a real process |
| EF-10 | Every scenario transition with pending fake operations | Retired reads cancel; admitted fake write updates fake storage exactly once; later read sees result; successor unaffected |

For EF-5 through EF-8 inspect the real owner implementation first. The review saw distinct extraction/persistence/restart/log stages and did not inspect the entire archive validator or token protocol. Keep the existing protections and test them; do not invent proof based on a fake installer that merely returns success.

## Existing owner and production composition regression

Retain all seven inspected `PluginsPageTests` journeys, adapted only to the new public seam/selectors: descriptor executors, tag grouping, empty executor state, real connection settings persistence, owner-produced OAuth URL/new-tab attributes, catalog package install/restart and installation/runtime logs.

Discover current focused integration tests for PluginCatalog/Settings/Connection/Grant, package manifest/extraction/restart, OAuth/callback/vault, log redaction, Plugins HTTP endpoints and workflow executor adapters. Do not use guessed filenames as an excuse for zero tests. If moving contract assemblies, inspect reflection/serialization/endpoint schema/plugin-loader consumers and package/source-mode behavior.

Each real database lane uses explicitly isolated PostgreSQL 18. Record sanitized server version/endpoint and leased database ownership. Keep package catalog/installed/runtime directories under the test environment. Avoid loading external untrusted plugin code: use the repository's trusted fixture package and exact existing runtime activation tests where affected. Never use the ordinary development database, port 5032 host or a user's installed package directory.

## Browser acceptance

Run Chromium at the supported large-desktop viewport (currently 1600 × 1000; follow current repository guidance if changed).

**Production route:** render `/plugins` in the real Web host with the real shell and relevant production registrations. Navigate all six sections. Save settings, reopen and read back actual owner values/ID. Exercise grant and enabled state through real controls, hold a read-back while typing, force a secondary read failure and verify one durable connection. Observe correct selected/all logs. Use the actual package dialog/InputFile with harmless owned fixtures, verify installed/restart-required receipts, and test restart only on a dedicated disposable host. Exercise OAuth start through a controlled external boundary without visiting a real user's account/provider. Preserve unrelated dirty editors while another action completes.

**Sandbox:** same real components, all six sections and meaningful scenarios. Verify busy grant state, dirty/invalid settings, out-of-order requests, exact connection identities after fake commit/refresh, package dialog close/reopen, bounded upload scenario and harmless OAuth/restart effects. A browser observing a fake store is sandbox proof, not database proof.

Wait for the correct target and operation generation/receipt. A button's existence, disabled state, stale generic Ready label or preexisting success notice does not prove a new operation completed. After the awaited interaction, read the owner outside renderer-dispatcher assertions. Capture and inspect screenshots for settings, grants, dialog/upload/restart and warning/recovery. Check browser/server errors, failed assets, focus, overflow and overlay geometry; expected injected failures must be specifically accounted for rather than ignored wholesale.

## Build, static gates, measurement and cleanup

Build each changed production project directly: contracts, RCL, module, sandbox, and affected composition/Web/consumer projects. Build/test SDK adapters only if their source or public dependencies changed. Reevaluate broad Stable/platform gates according to current invalidation triggers; no automatic unfiltered full suite after every local change, and no dismissal of a mandatory lane as somebody else's CI problem.

Run current portability-static tooling self-tests/scan/enforcement, review added/stale findings and repair actual defects. Baseline updates must be intentional and inspected; final enforcement runs without `--write-baseline`. Run maintained documentation/evidence validators and whitespace checks. Never weaken rules or suppress relevant tests to produce a pass.

Complete [DEV_LOOP.md](DEV_LOOP.md). Inspect owned process/container/path identities before cleanup, release browser contexts, sessions/streams, fixture databases, temporary package paths and owned watch processes. Restore measurement-only source/asset edits exactly. No broad process kills.

Final report separates passed, failed, blocked, not run and not applicable; includes exact source/configuration, commands, expected/discovered/executed cases, sanitized evidence paths, actual graph/asset findings, sample counts, S0 result and any residual debt. A missing environment gate permits an honest checkpoint, not a completed validation claim.
