# Workspace API Access UI boundary

Status: complete. This record
executes the sealed [assignment](../../codex/bundles/CanDoItAll_Workspace_API_Access_UI_Decoupling/prompt.md).
The package remains historical input. Only WSC-R1 and API Access are in scope. Data Sources
and Storage retain their production hosts; no other Workspace slice was started.

## Entry and source ownership

Entry: `aad66738bbd0c10b1746d78806d714bed625e598`, `components-decoupling`, clean. The sole
change after reviewed Core `186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf` was the sealed bundle.
Read-only siblings: Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SharedInfo
`83e21e23bcf43d92b061a6d367ac385241d13cd3`. All were clean at entry.
Windows, .NET SDK 10.0.303, configuration `ApiUiProof`, sibling source replacement and
Parity theme were used. Raw proof is ignored under `artifacts/workspace-api-access-ui`;
safe screenshots are under `output/playwright/workspace-api-access-ui`.

The complete extracted package, current repository/CI/testing/UI-seam instructions and
applicable SharedInfo guidance were read. This is a compatible external Behavioral bundle:
its prompt, reviews, source register and matrix provide the semantic phase/closure roles.
It does not need a schema migration. CodeAnalytics, Components MCP and dotnetwatch MCP
were unavailable in enabled tools. Local source, evaluated restore graphs, reflection,
CLI watch and actual Playwright MCP supplied the documented alternatives. The repeatable
browser journeys also use the repository's C# Playwright fixtures.

The inventory traced SettingsPage's active API slot, WorkspaceApiAccessHost and all four
children: issuance, token metadata/confirmation, scope picker and ordinary account editor.
Production owners remain `ApiAccessService`, `ApiTokenAdministrationService`,
`ApiUserAdministrationService`, the canonical `ApiScopeCatalog`/identity rules, real Web
administration-access adapter, issuer and private user/token stores. HTTP endpoint filters,
registered-session checks and the settings route remain their existing owners. Private
records in mixed runtime/value files were projected, not moved into the UI dependency graph.
`ApiTokenIssueResult` consumers in LLM Chats/Workflow HTTP tests use the unchanged bearer
and type fields; their business-family handlers and transport were not changed.

## WSC-R1 prerequisite

Two failing-first real Files form cases reproduced `.sample` being kept as the Delete
target after Save to `.next` and later executable-path input. `WorkspaceFilesState`
now increments a separate destination revision only when extension text changes.
`WorkspaceFilesController` adopts a known saved destination when that revision and
editor identity still match, while its whole-draft revision continues protecting field
reconciliation and Delete. A target edit away and back is a new revision. New/select/
dispose cannot adopt old identity. The real Files owner and storage protocol are unchanged.

The corrected two cases pass. Fourteen identity cases include new/existing drafts,
warning/read-back failure, target changes, retirement, unknown locks and exact next Delete;
the original extension remains present. The Core light selection passes 52 cases, the real
private Files owner 21, Resources readiness 10 and relevant production hosts 9. The final
production Settings browser also exercises Defaults, Secrets, Files, history, Data Sources,
Storage and replacement Providers navigation. Resources RS-R1 and Core sensitive-state
semantics remain intact.

## Implemented boundary

```text
Workspace production module -> Workspace.ApiAccess.UI -> Workspace.ApiAccess.Contracts
                                           |
                                           +-> Components.BaseLib -> Components.Common
Workspace.ApiAccess.UiSandbox --------------+
```

Three projects were added: dedicated API contracts, a Razor UI library and the independent
sandbox. Cohesive presentation controllers live in the UI library; another presentation
assembly would add a project without a new dependency boundary. The module references the
leaf and registers three narrow production owner adapters and a circuit-scoped safe ledger.
Its thin host creates/retires sessions and projects status back to the unchanged shell.
No generic Core project acquired an API reference. Infrastructure and shared foundation
acquired no product-contract edge. Data Sources/Storage and the eight Settings entries
remain unchanged; Providers still replaces navigation with `/agents?tab=providers`.

- `ApiAccessSession` separates configuration availability from management availability,
  fences activation and read-only receipt observations, and retires sensitive children on
  caller/view/access changes. Only the active API host loads its status/accounts; token
  metadata remains lazy.
- `ApiTokenIssueController` captures the complete immutable request before any incomplete
  await, validates raw lifetime text without coercion, and keeps a stable form/draft.
  Its private one-time value is distinct from safe outcomes/metadata, is never recovered
  from observations and is released on dismissal or retirement.
- `ApiTokenListController` owns its page and exact-ID/kind/action confirmations. Machine
  remains the only management category. `ApiPageController` separates desired text from
  accepted query/page, fences success/error/finally, identifies retained stale rows and
  permits one bounded page correction without replacing a newer query.
- `ApiAccountController` acquires an exact record before opening an editor. Captured
  requests include expected version; known identity/version is adopted before read-back
  while later fields survive. A successor draft cannot inherit old identity. Password
  intents use private one-use fields and are disposed; metadata/receipts contain no
  password, hash, bearer or credential binding.
- `ApiOperationLedger` bounds safe receipts at 32, never evicts pending/unknown effects,
  and blocks conflicting handler admission while allowing independent targets/reads.
  Observations neither establish causality nor resolve unknown writes. Late observation
  results, failures and access refusals cannot replace a newer outcome or retire it.

The real BaseLib forms, immediate inputs, grids, dialogs, clipboard and scope picker are
used in both hosts. Canonical available scope definitions are projected from the production
catalog; the UI's parser only edits selections and does not grant authority. Ordinary users
cannot become administrators, empty user grants remain valid and machine empty grants are
refused by the existing owner. No key/options tree, production DI, service locator, durable
command queue, automatic retry, JWT runtime or password runtime enters the light graph.

## Real owner changes and compatibility

The real token administration service snapshots mutable collections and all request fields
before its permission await. It checks access on reads and writes, pins exact machine IDs
at the presentation adapter, and checks cancellation before admitting a durable operation.
An admitted writer runs to its original instance-local registry/store, independently of
subsequent view cancellation.

`ApiDurableAcknowledgementException` carries only safe candidate identity and failure type.
The issuer preserves its acknowledged registration as internal metadata; a writer failure
before/after durable commit remains Unknown without a disclosed bearer. The account service
isolates diagnostic logger faults after an acknowledged store result, returning a safe
internal warning rather than inventing an unacknowledged create/update/reset/delete.
Pre-write validation, missing-record and version conflicts remain known refusals. These
changes do not query a username to infer success or treat a generated ID as proof of commit.

Public HTTP property names and numeric credential categories, successful token/account
schemas, 200/204 responses and `no-store` are unchanged. Ambiguous infrastructure failure
uses the existing redacted 503 pipeline. Hashing, username rules, scope policy, registration,
authentication revisions, trusted-local/validated-admin distinction and session invalidation
remain real owner responsibilities. Actual secured tests prove scope use, revoke/delete
rejection, update/reset/disable/delete invalidation, no re-enable revival, recreated-name
new identity and database-switch/restart control-plane continuity.

## Independent scenario host

The sandbox mounts exactly the shipped renderers/controllers over three in-memory ports.
It has no database, vault, filesystem account registry, signing or authentication server.
Synthetic `NOT-A-CREDENTIAL` values and the distinct `fixture.*` vocabulary are labelled.
Each store is bounded to 128 account/token records; 61-row scenes exercise 25-item pages.
Typed gates/faults cover access, lists, exact reads, writes, before/after ambiguous commit,
known warnings, validation, missing records and conflicts. Mutation-time checks prevent
held writes from overwriting a newer account version, duplicating a normalized username or
restoring a deleted token.

Reset retires the current session and creates an independent store. Admitted old writes
complete against the old store. Eight retained stores and 16 held operations are bounded;
unresolved writes are not silently dropped for capacity. Alt+Shift+R releases held operations
inside dialogs. The shortcut does not rerender the whole page for unrelated key events.
The [sandbox README](../../src/Sandboxes/CanDoItAll.Workspace.ApiAccess.UiSandbox/README.md)
contains reproducible startup and scenario instructions.

## Discovery and execution

All filters below were source-inventoried before build-backed discovery; expected equals
actual execution, with zero skips in passing runs. Counts overlap across diagnostic reruns
and must not be added into a purported full-suite total. Configuration/output was isolated
as `ApiUiProof`; builds sharing it were sequential. Each run used this pattern:

```powershell
dotnet test <owning.csproj> -c ApiUiProof --list-tests --filter '<filter>' /m:1
dotnet test <owning.csproj> -c ApiUiProof --no-build --no-restore --filter '<same-filter>' --logger trx /m:1
```

The table's `~Class` abbreviation means literal `FullyQualifiedName~Class`; `|` joins these
clauses. Project names omit the common `CanDoItAll.` prefix. Full project roots are under `tests/Components`, `tests/Unit`, `tests/Integration`
and `tests/Playwright` respectively. Raw discovery/run logs and named TRX are retained.

| Proof / owning project | Exact filter clauses | Expected / discovered / passed |
| --- | --- | --- |
| `s0-core`, Workspace.UI.Tests | `~WorkspaceUi` | 52 / 52 / 52 |
| `s0-real-files`, Tests.Unit | `~FileApplicationPreferenceServiceTests` | 21 / 21 / 21 |
| `s0-resources`, Resources.UI.Tests | `~ResourceEditorReadinessTests` | 10 / 10 / 10 |
| `s0-hosts-exact`, Tests.Components | `~ResourcesExactEditorHostTests\|~WorkspaceApiStatusTests\|~CanDoItAll.Tests.Components.Shell.SettingsRendererTests.` | 9 / 9 / 9 |
| `api-production-components-final`, Tests.Components | `~ApiIssuanceCaptureTests\|~ApiTokenAdministrationTests\|~ApiUserAdministrationPanelTests\|~WorkspaceApiStatusTests` | 9 / 9 / 9 |
| `api-owner-final`, Tests.Unit | `~ApiAdministrationOutcomeTests\|~ApiAdministrationDurabilityTests\|~ApiCredentialRulesTests\|~ApiTokenRegistryTests` | 29 / 29 / 29 |
| `api-http`, Tests.Integration | `~ApiAccessAuthorizationIntegrationTests\|~ApiAccessContractTests\|~ApiSectionPermissionsTests\|~ApiSessionBoundaryTests\|~ApiUserSessionIntegrationTests\|~ApiAdministrationOutcomeHttpTests` | 38 / 38 / 38 |
| `api-light-final`, Workspace.ApiAccess.UI.Tests | `~CanDoItAll.Tests.Components.WorkspaceApiUi` | 71 / 71 / 71 |
| `api-sandbox-browser-fifth`, Tests.Playwright | `~ApiAccessSandboxBrowserTests` | 2 / 2 / 2 |
| `api-browser-final`, Tests.Playwright | `~ApiAccessSettingsBrowserTests\|~ApiAccessSandboxBrowserTests\|~ApiAccessDeploymentTests\|~WorkspaceSettingsBrowserTests.Production_settings_use_real_owners_and_keep_deferred_hosts_reachable` | 5 / 5 / 5 |

The 71 light cases exercise page/activation/observation A-B-A races, unavailable versus
empty, form capture, raw values, draft/child retirement, handler admission, unknown locks,
capacity, actual nested renderers, scenario versions and evaluated/runtime/public-type
boundaries with negative forbidden/unresolved/cycle fixtures. The 29 owner cases include
private real file writes at before/after durable fault stages. The 38 secured HTTP cases
assert exact successful JSON keys and real administration/session/middleware rules.

Failing attempts are preserved separately. WSC-R1 initially failed 2/2; the five initial
owner-capture/logger-fault cases failed 5/5; the original immediate-input form failed on its
missing input binding. A broad `SettingsRendererTests` substring discovered 12 instead of
9 and was rejected before execution, then replaced with the exact Shell namespace.
The first light run passed 30/31; one ImmutableArray assertion used wrapper equality instead
of element comparison and was corrected. Subsequent light runs passed 45, 54, 62 and 71.

Initial browser runs are not green evidence: clipboard was read before its asynchronous
copy completed; the assertion now waits for BaseLib's copied state. The first publish left
reusable MSBuild workers holding redirected output; serial nonreusable publish fixes that,
and only its verified workers were stopped. Fast input following Escape raced dialog
retirement; the journey now awaits actual dialog removal before the next form interaction.
No delay or input/validation assertion was removed. Final sandbox journeys pass in both
Development and independently published Production. Earlier production and Core navigation
cases also pass. The consolidated final selection passes 5/5, including existing TLS,
CORS, trusted-proxy, administrator rotation and failure-pipeline checks. Sandbox browser
assertions also reject unexpected HTTP origins and horizontal desktop overflow.

## Dependency and development-loop proof

Restore `.dgspec` edges and `project.assets.json` were traversed with unresolved/cycle checks;
runtime assembly and exported-type traversal separately guards UI/contracts. Evaluated
source replacement is included, not just explicit ProjectReference text.

| Host | Projects including root | Packages | Native asset entries | Watch-list paths |
| --- | --- | --- | --- | --- |
| Original Web | 153 | 140 | 30 | 4,520 |
| Final Web | 155 | 140 | 30 | 4,539 |
| Independent API sandbox | 5 | 1 | 0 | 263 |
| Core sandbox before / after | 8 / 8 | 1 / 1 | 0 / 0 | 285 / 285 |

All graphs resolve with no cycles. Core watch lists are identical and its evaluated graph
has no added API edge. The sandbox's sole package is the framework assets package. Web's
only added production closure is API contracts/UI; the sandbox is not a Web runtime edge.
The new tests are in Components/Stable and all three actual CI component project lists,
not the product solution's test membership. Core/Infrastructure references are unchanged.

Measurements use owned loopback hosts, headless Chromium at 1600x1000, warm restored source
builds and actual interactive scope-picker readiness. Web used a private PostgreSQL fixture
and private control-plane root, seeded only with nonissued metadata for screenshots. Setup
before process startup is excluded. Razor edits changed the visible create label; C# edits
changed executed status maximums and reactivated the view. Each probe restored exact input
bytes in `finally` and stopped only its own process tree.

| Host | Startup ms | Razor edit-to-visible ms (three samples) | Executed C# ms (three samples) |
| --- | --- | --- | --- |
| Original Web | 46,029.3634 | 2,932.3482; 1,886.3115; 1,897.2628 | 665.3777; 1,954.4546; 1,662.4116 |
| Final Web | 56,860.2145 | 2,913.4507; 1,897.5808; 2,926.0117 | 1,341.4978; 2,365.2942; 1,353.4905 |
| Independent API sandbox | 9,643.3168 | 2,919.6046; 885.9469; 863.0087 | 594.4386; 593.5570; 598.6278 |

No statistically robust full-Web speedup is claimed;
Web startup was slower in these individual samples. The smaller independent graph and
backend-free loop are the architectural result. Scoped CSS/feature JS edits are N/A:
this slice owns neither. Shared dialog/clipboard/CSS/font assets are browser/publish tested.

At 1600x1000, inspected published screenshots show readable status/issuance, compact
confirmations, medium account editors and correctly layered nested dialogs. Footers remain
reachable; multi-page token content scrolls inside the dialog. The page owns normal vertical
scrolling. The fixture toolbar consumes extra vertical space only in the sandbox. Safe
unknown receipts show candidate identity without implying success. No revealed bearer or
password screenshot is retained. Production baseline/final screenshots use nonissued
metadata and empty password fields.

## Gate decisions and closure

The broad-Stable trigger assessment is bounded to this change: the root solution only adds
three leaf projects; CI only adds the owning light test to existing lists; module DI adds
three API adapters and a scoped presentation ledger. There is no cross-cutting registration,
Directory.Build, shared fixture, infrastructure store, schema/migration, middleware policy
or public successful wire change. The changed API owners have explicit focused real-file,
HTTP and production-browser consumers above. Other feature families only consume unchanged
issuer fields. Therefore no new unfiltered Stable execution is triggered for this leaf
checkpoint. The earlier Core mixed Stable result (15,567 pass / four fail followed by
focused repairs) is historical and is not claimed as a clean all-green run here.

Final direct builds of API contracts, UI, Workspace module, sandbox and Web all pass with
zero warnings/errors. After restoring the watch probes, fresh discovery/execution again
passes 71 light, nine production-component and 29 real-owner cases. The secured HTTP 38
remain valid against unchanged owner inputs. The five consolidated browser cases pass
with zero skips, including the published DLL in Production without production backends.

The full proposed-tree portability scan included new untracked files: 7,228 files,
32,539 findings. All added/stale protected findings were reviewed. The moved picker retains
explicit ordinal case policy; scenario username/search/scope comparisons and shortcut keys
are deliberate ordinal comparisons, not filesystem assumptions. Two README detections are
the `powershell` code-fence language label, not elevation or executable launching. The two
old picker allowances were removed and ten new fingerprint entries (11 occurrences) added.
After inspecting the baseline diff, final enforcement without `--write-baseline` passes
with 15,207 reviewed protected occurrences. Scanner rules and exclusions were not changed.
Portability-tool tests pass 6/6 and secret-tool tests 4/4.

Documentation validation passes for 290 maintained Markdown files; its nine evidence tests
pass. The initial documentation run identified three missing new-project READMEs, now added.
The sealed delivery validator passes all 38 manifest entries/54 links without changing the
bundle; delivery tooling tests pass 18/18. Artifact secret scanning at a 64 MB text limit
passes with no oversized/unreadable text skips or findings. Playwright MCP text artifacts
also pass. PNGs are excluded by the text scanner and were visually reviewed separately;
this is not an OCR claim. No revealed production credential was captured.

All task-owned watch/manual/test hosts were stopped through their exact process lifetimes.
PostgreSQL 18.6 used a verified loopback-only task container; its full identity, image,
port and `codex.task=workspace-api-access-ui` label were checked before removal. Private
fixture roots use the existing test-environment disposal. Ordinary port 5032, its database,
configuration and unrelated containers/processes were not operated. The three sibling
repositories remain clean at their entry SHAs. Changes are limited to this checkout;
local signing is retained, with no push, merge or deployment.

## C# Architecture Gate Result

Status: Pass.

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Resolved | Files destination identity followed unrelated path revision | Two failing-first cases, 14 identity cases, 21 real Files owner cases | None |
| Resolved | Issuance could combine fields across a permission await | Failing-first form/owner capture and immutable captured-intent proof | None |
| Resolved | Logger faults concealed acknowledged account commits | Four real-store mutation kinds plus secured HTTP schema/status proof | None |
| Resolved | Late reads/observations could cross view/outcome origins | Page, exact-editor, session and observation fences with controlled completion tests | None |
| Resolved | Held scenario writes could miss intervening versions/identity changes | Mutation-time conflict/uniqueness/existence checks and deterministic tests | None |

### Dependency direction

Contracts have no implementation dependency. UI depends only on its API contracts and
BaseLib/Common. Production composes narrow adapters over existing owners. The sandbox
uses exactly that renderer/controller tree without production services. Evaluated edges,
runtime/exported types and negative graph fixtures agree; Core is unchanged. No service
locator, broad settings facade, reverse foundation edge or hidden runtime escape exists.

### Partial-class policy

No partial service cluster or runtime manager was introduced. Framework-generated Razor
partials remain rendering components. Issuance, paging, account editing, activation and
safe receipt admission each have a specific lifetime/responsibility.

### Testability proof

The light graph independently proves real renderers and presentation behavior. Private
file-owner and secured HTTP tests prove durability and authority; production and published
browser journeys separately prove composition and assets. Synthetic metadata cannot stand
in for JWT/password/session enforcement, and no such claim is made.

### Closure decision

S0 and A1–A5 are complete at Behavioral depth. Raw inputs for WSC-R1, complete API surfaces,
owner compatibility, independent sandbox, Core containment and all validation-matrix groups
V-S0/BD/ST/AU/UI/CL are Solved by the named proofs above. No known in-scope blocker remains.
Remote cross-platform CI and a new unfiltered Stable run were not executed; they are not
claimed. No further Workspace slice is authorized by this completion.
