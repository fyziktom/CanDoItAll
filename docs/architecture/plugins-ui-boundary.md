# Plugins UI boundary

## Scope and entry receipt

The complete Plugins workspace now uses an independent rendering library and the
same host-owned presentation policy in production and a standalone sandbox. This
record closes the [Plugins package](../../codex/bundles/CanDoItAll_Plugins_UI_Decoupling/prompt.md),
including its shared foundation, source review, validation matrix and development
loop obligations. It does not start another module.

Execution began at `177a8a91e9ce2723276fcd2e52deea77783727c6` on
`components-decoupling`, with a clean checkout. The reviewed source provenance was
`dd050d5a1489537207e073cac0838f40cde4340f`; all 22 repository-file blob references in
the source register matched that revision. The complete 34-file package was read.
Its 22 shared-foundation files matched the preceding TestLab foundation. Package
validation passed on a task-owned LF-normalized copy: five JSON files, 64 links,
33 manifest entries and 29 source entries. The committed input package was not
rewritten. This compatible external bundle uses its own input/requirement/source
documents and this durable execution receipt; no second canonical bundle tree was
invented. Evidence depth is behavioral, with raw local artifacts kept ignored.

S0 accepted TestLab without a product edit. Current discovery and execution passed
16 `TestLabSandboxReviewTests`, four `TestLabNotificationTests` and the one
`Global_saved_party_resolves_through_the_owner_without_project_admission` case in
`TestLabProof`. They cover independent committed read-back, exact saved-party
resolution and the Unknown notification/replay lock. The existing browser receipt
was reusable: only the Plugins bundle intervened after the accepted correction.
This does not claim a fresh execution of the historical 77-case correction or
115-case extraction. Shell fix `7446c940e`, TestLab extraction `3c579fd1a` and
correction `dd050d5a1` remain intact.

Environment: Windows `10.0.26200`, win-x64, .NET SDK `10.0.303`, MSBuild
`18.6.14+e730f1db7`, isolated configuration `PluginsUiProof`. Source replacement
from `Directory.Build.targets` remained enabled. Read-only sibling revisions were
Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85` and SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`. Code Analytics and Components MCP were
unavailable; local implementation/component contracts, search, current test
discovery and evaluated build metadata supplied the evidence. No MCP result is
claimed.

## Ownership and dependencies

```text
Plugins module -> Plugins.Presentation -> Plugins.UI -> Plugins.Contracts
Plugins.UiSandbox -> Plugins.Presentation
Plugins.Contracts -> Plugins.Abstractions -> descriptive framework abstractions
```

The module keeps `/plugins`, a transient per-page `PluginWorkspaceSession`, EF,
stores, grant evaluation, plugin runtime/loading, vault, OAuth protocol, package
installation and restart. The session calls existing owners in process and supplies
the callback URI and browser effect. The routed page only binds, renders and
retires the workspace. Production DI uses this path; no HTTP adapter was inserted.

The additional small presentation project is justified by sharing the actual
request, draft, reconciliation and effect policy across two hosts. It composes
`PluginWorkspaceReads`, `PluginDraftRegistry` and `PluginWorkspaceOperations`;
`IPluginWorkspaceOwner` is the real production/scenario substitution boundary.
There is no service locator, generic event bus, controller inheritance or partial
class split. The renderer receives a typed workspace/view, including typed
sections and action targets. All six sections, tree, header, helpers and package
dialog moved together; descendants inject no production owners.

Contracts move stable catalog, settings, connection, grant, log, OAuth and pure
package models while retaining their `CanDoItAll.Modules.Plugins` namespaces,
wire shapes, enum values and defaults. Thirty type forwarders preserve resolution
through the old implementation assembly. SDK identifiers/descriptors remain in
Plugins.Abstractions. Runtime registrar, package path options/assets and token
envelope remain with the implementation. Existing XML comments moved with their
models for generated OpenAPI; no new XML documentation was introduced.

Evaluated NuGet restore graphs include source-mode replacement and every root:

| Host | Projects including root | Transitive packages | Native assets | Unresolved / cycles |
| --- | ---: | ---: | ---: | --- |
| Original Web | 137 | 140 | 30 | 0 / 0 |
| Extracted Web | 140 | 140 | 30 | 0 / 0 |
| Plugins.UI | 11 | 19 | 0 | 0 / 0 |
| Plugins sandbox | 13 | 1 | 0 | 0 / 0 |

The sandbox's allowed project closure is Sandbox, Presentation, UI, Contracts,
Plugins.Abstractions, AgentFramework.Models, AgentFramework.Capabilities.Abstractions,
Memory.Abstractions, Infrastructure.Abstractions, AgentFramework.ProviderHistory.Abstractions,
SharedKernel and Components.BaseLib/Common. These are descriptive abstractions,
not runtime, persistence or provider implementations. The RCL's 19 packages are
Microsoft ASP.NET Core/Extensions/JSInterop framework dependencies; the Web SDK
supplies those framework assemblies to the sandbox, whose one package is
`Microsoft.AspNetCore.App.Internal.Assets`. The assembly/public-type guards include
negative forbidden-transitive and unresolved-edge fixtures. No forbidden owner,
EF, vault, concrete plugin, workflow runtime or Web/composition edge is present.

Product solution membership adds the three reusable production projects. The
sandbox is directly buildable; lightweight tests belong in Components and Stable
test solutions and the actual component CI project lists. Browser fixtures belong
to the Playwright graph, not the product solution.

## State, admission and effects

| Responsibility | Final behavior |
| --- | --- |
| Connection draft | Host-owned plugin/key, descriptor semantics, persisted identity and generation; raw input and schema validation survive unmount, selection, refresh and unrelated actions. All nine schema controls capture input before blur, including incomplete numbers/JSON. |
| Save | Captures the whole request independently of mutable fields. Accepts the exact returned ID/token before read-back, merges only unchanged submitted fields and preserves newer edits as dirty. One operation per editor; independent or explicitly retired successor editors can progress. |
| Reference change | Removed/replaced descriptors or missing persisted targets retain a visible draft with writes blocked. Explicit reset/target selection retires it; no key/name/position-based adoption. Dirty and unresolved drafts are retained within a 64-draft bound; a full retained set gives a refusal notice. |
| Reads | Seven independent lanes for catalog, selected settings, OAuth, both log streams, packages and restart. Request generations fence success, error and completion. Selection and log-scope changes invalidate the relevant lanes, including A → B → A. Logs retain `Take: 50`; catalog reload reads only selected references. |
| Known/unknown outcome | Saved plus failed follow-up remains saved with warning and known identity. Refresh performs reads only. Unknown keeps the submitted operation and blocks replay until explicit operator review; it cannot be resolved by a coincidental display-name match. Operation storage is bounded at 256. |
| Grants/lifecycle | One typed grant target includes plugin, capability, recipe, scope kind and trimmed scope key. Grant/Deny/Revoke share visible busy state and handler admission. Enable/disable share the plugin target; save/OAuth share the editor target. Other targets remain independent. |
| Disposal | Detaches handlers, retires drafts, invalidates reads, cancels owned upload and suppresses queued UI effects. Each cancellation source is disposed by its own unwound continuation. Already admitted writes can still finish with their original owner. |

Package extraction, install persistence, restart metadata and logging have separate
commit boundaries. Narrow typed committed/stage exceptions preserve receipts when
a secondary step fails. Package progress identifies replacement, extraction,
installation and recorded restart; known restart state is accepted before another
read can fail. A catalog-only failure during package refresh also produces a
saved warning. The installer retains existing archive/path/byte-limit/cleanup
rules. No general transaction coordinator, schema or new concurrency protocol was
added. Existing connection/grant tokens are not claimed as enforced optimistic
concurrency.

The actual `InputFile` passes an `IBrowserFile` through the UI-local seam. The host
opens and disposes a bounded stream using `MaxPackageBytes`; the installer enforces
its own limits and generates staging paths. During consumption, the input stays
mounted, a second selection is rejected and both dialog close paths/Escape leave
it open. Navigation/disposal cancels reading. The workspace never buffers the
whole archive. Known partial installation blocks another upload in that workspace;
the receipt instructs the operator to refresh/read stored state and reload the
page after review before a new archive. Unknown outcomes have explicit review
controls. Neither recovery path automatically replays installation.

OAuth requires the chosen saved, clean, valid, enabled connection and current
permission/status facts. URLs remain transient browser effects using `_blank` and
`noopener,noreferrer`; a late or retired origin cannot open one. Session creation,
popup failure and Connected are distinct. The observed owner entry point preserves
connection/session commit stages without changing the existing `StartAsync` API,
callback/return paths, state expiry, PKCE or vault ownership. General notices and
diagnostics do not expose arbitrary exception text, settings, tokens or full URLs.

Restart remains explicit in `PluginRuntimeRestartService`. Duplicate requests are
serialized, committed metadata survives a logging failure, and a scheduled stop
does not depend on successful logging. Owning tests await `ApplicationStopping`
with a bound instead of the former arbitrary 1500 ms sleep. Only disposable host
lifetimes were restarted.

## Real renderer, scenario host and visual review

The sandbox registers only Blazor and BaseLib. It reuses the full production
surface and presentation policy, with separate fake connection/grant/OAuth/package
storage. Fake writes persist before subsequent reads; controlled waits expose
pending and post-commit states. Scenario transitions retire the workspace and
release old waits without redirecting admitted writes into the successor store.
Scenarios cover normal/empty/100-entry catalogs, missing references/descriptors,
all field kinds, invalid/dirty drafts, partial/unavailable/stale/held reads,
held save/read-back/grant/OAuth/upload, refusal, unknown, warning, permissions,
scoped/recipe grants, OAuth statuses and package/restart stages. Harmless log rows
distinguish plugin/all scope and the two streams. No database, real runtime/plugin,
vault, OAuth transport or installer is needed to render them.

The desktop composition retains a 24rem tree pane and one detail surface with six
tabs. Settings remain a form; target, status and reset controls form one compact
row. At 1600 × 1000 the normal representative two-field form includes its Save
action in the first viewport. Warning/recovery content and the deliberately long
all-field fixture extend vertically in the existing detail/page scroll area. The
tree owns overflow for long catalogs; no horizontal page overflow was observed.
No mobile variants, bespoke layout CSS or replacement component library were added.

Inspected screenshots include current production settings with later typing,
production warning/grants/dialog/restart, sandbox busy grants, invalid settings,
unknown recovery, held upload and published dialog. The earlier vertically stacked
target controls consumed too much first-viewport space; the compact row corrected
that. Busy choices identify the actual grant row. Warning and unknown messages
retain both identity and recovery controls. The package dialog is a wide, centered
two-column overlay with its own bounded content region; published geometry was
1152 × 363.25 at (224, 318.375), within the viewport. The held-upload overlay also
remained bounded, and accepted save/read-back retained input focus.

Parity CSS links the authoritative generated Web `wwwroot/css/output.css` as a
static asset without a Web project reference; missing CSS fails the sandbox build
with the existing Tailwind rebuild command. BaseLib supplies component CSS,
Material Symbols Rounded fonts, scripts and dialog behavior. Both the local SVG
and controlled package-icon route loaded. Source browser proof and published
Production-host proof verified ten stylesheet/script/font/image responses (200,
plus the intended package-icon redirect), correct computed font, no overflow and
no failed assets/page errors. The published host ran without database configuration.
There is no feature JavaScript or scoped CSS to migrate.

## Behavioral validation receipt

All commands ran from the repository root. Each listed test filter was derived
from current source, discovered with a build, then run unchanged with
`--no-build --no-restore`. The owning paths below omit only the repeated project
filename, which is the directory name plus `.csproj`.

```powershell
dotnet test $project --configuration PluginsUiProof --list-tests --filter $filter /m:1
dotnet test $project --configuration PluginsUiProof --no-build --no-restore --filter $filter --logger "trx;LogFileName=$name.trx" --results-directory artifacts/plugins-ui /m:1
```

| Owning project below `tests/` | Exact filter | Expected / discovered / passed |
| --- | --- | --- |
| `Components/CanDoItAll.TestLab.UI.Tests` (TestLabProof) | `FullyQualifiedName~TestLabSandboxReviewTests` | 16 / 16 / 16 |
| `Components/CanDoItAll.Tests.Components` (TestLabProof) | `FullyQualifiedName~TestLabNotificationTests` | 4 / 4 / 4 |
| `Unit/CanDoItAll.TestLab.Tests` (TestLabProof) | `FullyQualifiedName~Global_saved_party_resolves_through_the_owner_without_project_admission` | 1 / 1 / 1 |
| `Components/CanDoItAll.Plugins.UI.Tests` | `FullyQualifiedName~CanDoItAll.Tests.Components.Plugins` | 47 / 47 / 47 |
| `Components/CanDoItAll.Tests.Components` | `FullyQualifiedName~PluginsPageTests\|FullyQualifiedName~PluginsPageDraftRegressionTests` | 9 / 9 / 9 |
| `Integration/CanDoItAll.Tests.Integration` | `FullyQualifiedName~PluginsUiOwnerReceiptTests` | 10 / 10 / 10 |
| `Integration/CanDoItAll.Tests.Integration` | `(FullyQualifiedName~PluginCatalogIntegrationTests&FullyQualifiedName!~Docker_qdrant_plugin_workflow_live_proof)\|FullyQualifiedName~PluginsOwnerPersistenceTests` | 31 discovery entries / 31 / 36 expanded cases |
| `Unit/CanDoItAll.Tests.Unit` | `FullyQualifiedName~PluginManifestTests\|FullyQualifiedName~PluginWorkflowExecutorBoundaryTests\|FullyQualifiedName~PluginWaveArchitectureGuardrailTests\|FullyQualifiedName~BundledPluginWorkflowExecutorTests` | 33 / 33 / 33 |
| `Integration/CanDoItAll.Tests.Integration` | `FullyQualifiedName~ApiDocumentationCoverageTests` | 23 / 23 / 23 |
| `Playwright/CanDoItAll.Tests.Playwright` | `FullyQualifiedName~PluginsBrowserTests` | 2 / 2 / 2 |

The existing simulation theory carries six complex MemberData descriptors that
VSTest lists as one theory; source-derived execution is 36. An initial expectation
of 36 discovery lines was rejected before execution, then corrected to 31 with
this explicit explanation. No zero discovery or skipped cases count as proof.
The table represents 160 distinct Plugins/consumer cases plus 21 S0 cases.

The original seven real-owner page journeys passed before extraction. Failing-first
regressions demonstrated missing unblurred raw input and refresh erasing a dirty
name; their corrected real page cases are included in the nine. Final review added
a failing-first catalog-only package-refresh case (expected warning, observed
plain saved), then included its passing correction in the 47. The two existing
restart/runtime cases were rerun after the narrow restart change with filter
`FullyQualifiedName~Plugin_runtime_restart_request_stops_host_lifetime|FullyQualifiedName~Docker_runtime_package_install_activates_settings_and_workflow_executor_after_restart`:
2 discovered, 2 passed. These are already included in the 36, not extra distinct
coverage.

| Matrix obligation | Concrete proof |
| --- | --- |
| TL-1–5 | Fresh S0 selections above, corrected source/browser receipt inspection and separate historical counts. |
| BD-1–7 | Evaluated restore graph, three lightweight boundary tests, real six-section tests, HTTP defaults/ID/forwarder assertions, generated API coverage, source/publish assets and solution/CI membership. |
| ST-1–7 | `PluginsWorkspaceTests`, real form `PluginsSurfaceTests` and two page regressions: immutable whole input, actual accepted ID before held read-back, later typing, refusal/unknown, direct duplicate admission, independent successor and changed/missing reference recovery. |
| ST-8–12 | Controlled out-of-order A/B/A reads, independent bounded log streams, partial failures, noncooperative disposal, 100-entry read budgets and bounded draft storage. No polling sleeps substitute for operation completion. |
| GR-1–6 | Real busy grant buttons, full scoped/recipe target identity, conflicting versus independent writes, lifecycle conflicts and actual owner permission/persistence tests. |
| EF-1–4 | Clean/dirty/invalid/denied UI admission, delayed-origin popup suppression, failed browser effect, preserved session/connection progress and retained real callback/PKCE/vault/disconnect tests. |
| EF-5–8 | Actual production InputFile ZIP install; real invalid/oversized/read-failed archive cleanup; extraction/install/restart metadata/logger fault receipts; held stream close/reselection/disposal; partial receipt survives failed read-back. |
| EF-9–10 | Observable owned restart, duplicate request protection, harmless sandbox restart and retired scenario's admitted write readable exactly once in its original storage. |

Owner tests use actual PostgreSQL, stores, package validation/extraction and typed
faults at the relevant secondary boundary. The production browser uses the real
Web route/shell and owners. A test-only EF interceptor holds/fails the next
connection SELECT after the real commit; it does not replace the write. The
journey reads actual stored IDs/state, verifies one connection, grants and lifecycle,
uploads a valid test-owned archive and observes the owned restart signal. Only the
browser popup boundary is intercepted for OAuth; no live user/provider login is
performed. Expected injected EF read failures are accounted for specifically;
unexpected page, unobserved-task, disposal and asset failures are rejected.

Direct builds use `dotnet build $project --configuration PluginsUiProof /m:1` for
Contracts, UI, Presentation, Plugins, sandbox, Gmail, Office365, Docker, Composition
and Web. All ten passed on restored measurement sources. Browser child hosts use
`CANDOITALL_TEST_CONFIGURATION=PluginsUiProof`; external browser base URL is unset.
API documentation's 23 cases and the actual HTTP wire test cover the public
contract relocation beyond renderer names.

The owner/page/browser endpoint was a task-owned PostgreSQL 18.6 container
(`server_version_num=180006`) on `127.0.0.1:58941`, with per-fixture leased databases and package directories below
`CanDoItAllTestEnvironment`. The normal application/database was not used.
Fixtures dispose their databases, streams, browsers and paths.

## Development-loop measurements

Measurements use one restored/warm-cache owned watch host at a time, 1600 × 1000,
and the representative Office365 two-field settings/three-executor shape. The
user's background workload was not controlled or stopped for timing. Startup is
process launch through actual interactive settings rendering and required assets,
not merely a listening port. Three edits change a visible Razor label, an executed
log helper expression and computed application CSS; sources restore byte for byte.
The helper probe triggers section rendering for the C# change. No required watch
inputs/build targets were disabled, and no stale package binaries were substituted.

| Host / measurement | Samples in milliseconds | Median |
| --- | --- | ---: |
| Original Web startup | 61158.4 | one sample |
| Original Web Razor | 3940.0, 1916.6, 1891.8 | 1916.6 |
| Original Web C# | 1338.8, 1451.4, 1075.5 | 1338.8 |
| Original Web CSS | 203.9, 110.6, 143.9 | 143.9 |
| Extracted Web startup | 50074.9 | one sample |
| Extracted Web Razor | 2920.6, 1906.9, 2919.0 | 2919.0 |
| Extracted Web C# | 1390.9, 1473.0, 1052.0 | 1390.9 |
| Extracted Web CSS | 254.9, 109.3, 141.4 | 141.4 |
| Sandbox startup | 9437.4 | one sample |
| Sandbox Razor | 3933.0, 886.0, 878.2 | 886.0 |
| Sandbox C# | 330.0, 377.1, 360.0 | 360.0 |
| Sandbox CSS | 193.9, 111.5, 207.6 | 193.9 |

This is local edit-loop evidence, not a statistical benchmark. The full Web graph
stays broad and its final Razor samples are noisier/slower than the original;
there is no claim that extraction speeds up full-Web hot reload. The small sandbox
is independently useful. Razor/C# changes applied through hot reload and CSS
through asset refresh; no unsupported edit required a process restart. JavaScript
is not applicable. The extracted Web samples precede only the catalog-failure
boolean aggregation correction; graph, rendered probes and successful-path work
are unchanged. Sandbox samples include it.

An earlier sandbox repetition stopped on a transient Windows memory-mapped CSS
write lock after two CSS samples. It is retained as failed measurement evidence.
The complete run above uses a bounded retry in the ignored probe, with its 100 ms
retry included in the final CSS sample. Earlier probe/fixture mistakes and failed
test attempts remain in ignored evidence and are not counted as green results.

Actual inventories are captured with `dotnet watch --list --project $project
--configuration PluginsUiProof`: 4406 entries for original Web, 4424 for extracted
Web and 480 for the sandbox. The
restore graph and runtime updates corroborate the inventory; linked application
CSS is served/watched by static assets even when not a literal watch-list line.
Published parity was checked using `dotnet publish
src/Sandboxes/CanDoItAll.Plugins.UiSandbox/CanDoItAll.Plugins.UiSandbox.csproj
--configuration PluginsUiProof --output artifacts/plugins-ui/published-sandbox
/m:1` followed by a real Production-environment Chromium host without a database.

## Closure gates and retained limits

Raw TRX/discovery/build/graph/watch/publish logs and measurement JSON are under
ignored `artifacts/plugins-ui`; inspected browser images are under ignored
`output/playwright/plugins-ui`. Durable architecture and behavior claims are
recorded here rather than committing runtime logs or credentials.

Portability tooling self-tests passed (six baseline tests, four secret-scanner
tests). The full proposed-tree scan included new untracked source before staging;
a final full tracked scan followed staging and whitespace normalization. All 27
added and 25 stale fingerprint entries were reviewed. The added entries comprise
19 intentional case-policy fingerprints (tag/name ordering, expansion identity,
schema keys matching existing ConfigurationState semantics) and eight PowerShell
README-fence matches. They introduce no filesystem case assumption, platform-only
launch or elevation. Stale entries remove the moved renderers/obsolete page state.
The reviewed baseline diff was inspected; pattern definitions and policy stayed
unchanged. Final enforcement without `--write-baseline` passed with 15124 allowances.

```powershell
python tools/Validation/Portability/test_enforce_portability_baseline.py
python tools/Validation/Portability/test_scan_artifacts_for_secrets.py
python tools/Validation/Portability/scan_portability.py --repo-root . --output artifacts/plugins-ui/portability-staged-scan.json --tracked-only
python tools/Validation/Portability/enforce_portability_baseline.py --scan artifacts/plugins-ui/portability-staged-scan.json --baseline tools/Validation/Portability/portability-risk-baseline.json
pwsh -NoProfile -File tools/Validation/Test-DocumentationEvidence.ps1
pwsh -NoProfile -File tools/Validation/Test-Documentation.ps1
git diff --cached --check
```

Documentation evidence passed nine cases, documentation validation passed all 260
maintained Markdown files, and staged whitespace validation passed. These are
current local gates, not promises to leave validation to CI.

Cleanup verified zero remaining fixture databases, then stopped only container
`cda-plugins-ui-72e4fdca` after matching its captured ID/name and
`candoitall.task=plugins-ui` label. Its automatic removal was confirmed. No owned
watch, browser fixture, sandbox or asset-probe host remained. Measurement edits
were restored; sibling revisions and clean worktrees were unchanged. Every process
termination used an owned process handle/tree. The task never addressed or sent a
restart/stop to port 5032. At the final read-only audit it had no listener, three
entry PIDs were absent and two were still present; no claim is made that unrelated
process lifetimes stayed fixed during the run.

Bundle validation: Pass. Raw-input closure is Solved for S0 (accepted TestLab),
P1 (baseline/seam), P2 (complete dependency cut), P3 (state/admission/outcomes),
P4 (real effects) and P5 (sandbox/production proof). The source review and every
validation-matrix group map to the evidence above. There are no pending work units,
missing required proofs or downstream module work authorized by this receipt.

The broad Stable/platform suites were not run. The solution and CI changes only
add the bounded project's membership; they change no root build policy, shared
test infrastructure, global DI, persistence schema or migration. The actual public
contract consumers were rebuilt and selected through manifest/workflow, API,
runtime, owner and browser evidence. This local slice is not a release/merge or
broad frozen checkpoint. An SDK/serialization, shared lifecycle, build-policy or
cross-module owner change would reopen that decision. Linux/macOS and live
provider/Docker host behavior are not claimed; the opt-in live Docker test is
explicitly excluded, while trusted package runtime activation passed.

Remaining product limits are deliberate: no durable idempotency protocol, new
optimistic-concurrency enforcement, new URL/query protocol, plugin SDK redesign,
live OAuth account proof or generic effect framework. Unknown outcomes require
operator review. Known partial uploads require read-only review and a page reload
before another archive. The complete six-section seam and representative sandbox
are implemented; none of these limits defers a required renderer or owner path.

## C# architecture gate result

Status: Pass.

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Resolved | A heavy module owned rendering and mutable editor/request state together. | Thin routed page, independently built Contracts/UI/Presentation and real sandbox closure. | None. |
| Resolved | Post-commit failures and concurrent/outdated UI results could lose identity or misreport outcome. | Failing-first regressions, typed owner receipts, independent lanes and real persistence/browser proof. | None. |
| Resolved | Final package catalog refresh failure was omitted from the operation warning. | New failing-first case followed by 47 passing lightweight cases. | None. |

Dependency direction passes evaluated and runtime/public-type guards. Construction
keeps service registration and concrete owners in the module. No partial-class
partition or hidden implementation edge is introduced. Pure behavior runs without
a database and includes negative/race/recovery cases; real production behavior is
proved separately. Future Plugins sections use the workspace seam and renderer;
production does not bypass it. The architecture and bundle may close with the
static/documentation/cleanup receipt above; another module requires a new request.

## Bounded review closure in the Scheduler assignment

At starting HEAD `0e176a3b99270cdc9a86a57d6276d05979d7352e`, fresh failing-first proof
reproduced PL-R1 (two remaining-catalog controls absent), PL-R2 (actual replacement plus
cleanup double fault incorrectly Refused), and removed/repopulated A allowing an old OAuth
popup. The catalog shell now remains selectable independently of its selected detail, and
membership disappearance retires selection effects while retaining the draft origin.
The installer preserves its primary exception, exact `ReplacementStarted` identity and a
secondary cleanup exception. The real session reports Unknown and keeps replay locked.
No archive, path, size, installation or permission policy was relaxed.

Fresh selected evidence in `artifacts/scheduler-ui`: light 50/50, owner 11/11, page 9/9,
browser 2/2 (72 total). The browser covers A missing, explicit B selection, empty/repopulated
catalog and retained unknown A with one write and no popup. A demonstrated bUnit stale-event
lookup was repaired by locating and dispatching on the renderer, without changing its product
assertions. Earlier 160 Plugins/consumer and 21 TestLab counts above remain historical.
No TestLab source was changed. Scheduler final shared gates are recorded in the
[Scheduler receipt](scheduler-ui-boundary.md).
