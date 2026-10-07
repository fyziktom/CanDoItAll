# Validation matrix — observations, not test-count quotas

Each row is an obligation. One test may prove multiple rows, but a source assertion, mock
callback, static scan or screenshot cannot stand in for a different layer. Record concrete test
names/filters, expected discovery, actual discovery/execution, configuration and sanitized
artifacts. Derive counts from the **current** source, including parameterized cases. An older
receipt's counts are not an expectation for new code.

## Resources and accepted Memory regression

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-RS-01 | Catalog Refresh while B's exact editor read is held does not mark that editor ready or admit placeholder Save/Delete. | Controlled controller + actual header Refresh/selection |
| V-RS-02 | Failed B editor followed by successful catalog Refresh permits exact B retry, not a same-ID no-op. Initial route failure recovers too. | Controller + real renderer |
| V-RS-03 | Same successfully acquired ID remains a no-op; A-B-A, superseded success/error/disposal do not change successor readiness. | Controlled lifecycle tests |
| V-RS-04 | Already acquired dirty/historical draft, EditContext, raw configuration, admission and allowed cleanup survive optional-reference failure. | Existing and new form/owner tests |
| V-RS-05 | Production routed Agent context remains Loading/Failed until its exact editor and route context are acquired. | Real routed component host |
| V-RS-06 | Memory replacement/removal ordering, preserved history/accepted facts/manual feedback and no replay remain correct. | Current MemoryResultOriginTests and affected host selection |

## Dependency boundary and production scope

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-BD-01 | Baseline actual selected sections, source consumers, sibling/source mode and graph recorded before extraction. | Source inventory + owned original host |
| V-BD-02 | Four shipped renderers and shell are used by production and sandbox; deferred three local sections stay real production hosts. | Build + real route/component/browser |
| V-BD-03 | Evaluated/transitive/public-type graph excludes concrete Workspace/Security/Infrastructure/database/driver implementations from sandbox. Negative forbidden and unresolved edges fail. | Evaluated graph + runtime contract guard |
| V-BD-04 | Reuse History.Abstractions; no Security→Workspace implementation cycle, copied vault, general facade or service locator. | Architecture review + graph |
| V-BD-05 | Moved values preserve defaults, numeric enums, wire shapes and affected public consumers; HTTP settings strict validation/scopes/pending-read-back behavior unchanged. | Real serialization/API tests |
| V-BD-06 | All eight navigation items and correct Providers redirect survive direct URLs, invalid/missing tab, back/forward and pending initialization. | Actual routed browser/host |
| V-BD-07 | Inactive deferred slots do not initialize their owners or issue tokens; production Data Sources/Storage/API Access remain reachable and functional in bounded smoke proof. | Host counters + current production tests |
| V-BD-08 | Product/test solutions, CI selections, asset inputs and published manifests include correct projects; tests not in product solution. | Build metadata + publish |

## Defaults and common lifetime rules

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-DF-01 | Initial read cannot overwrite later unblurred input; defaults, provider options and other sections fail independently. | Real inputs + held owner reads |
| V-DF-02 | Captured six-field save remains stable while newer text/edit-away-back survives normalized result; stable EditContext. | Controlled form tests |
| V-DF-03 | Committed defaults plus failed provider/list read retain saved result and read-only retry; no duplicate save. | Real owner fault + controller |
| V-DF-04 | Failed persistence does not publish uncommitted currency settings; successful normalized state and deliberate read publication still work. | Actual Workspace owner + controlled failure |
| V-DF-05 | Missing/disabled provider reference is visible without substitution; changing it is explicit. | Actual selector + owner/API compatibility |
| V-DF-06 | Profile change prevents rebinding reads/writes; old known effect remains at original owner and never populates a successor. | Profile-bound production host/owner |

## Secrets and sensitive state

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-SE-01 | Catalog/count/filter uses only metadata; opening unrelated sections never resolves plaintext. Explicit selected edit resolves exactly the authorized intended secret. | Owner spies + production secret fixture |
| V-SE-02 | Actual SecretField reveal/copy/masking/timeouts work; tab/record retirement unmounts reveal safely without late resurrection. | Real component + browser |
| V-SE-03 | Save captures all inputs before first await; delayed Get, Save, Delete and reset cannot overwrite/clear successor A-B-A or newer plaintext/metadata. | Controlled inputs and direct handlers |
| V-SE-04 | Create ID is adopted before refresh; duplicate Enter/click and conflicting Save/Delete blocked; confirmed create never becomes an implicit duplicate retry. | Actual form + real stored row count |
| V-SE-05 | Known metadata commit followed by old-payload/Activity/log/read-back failure retains redacted identity/stage, not a false refusal. | Real PostgreSQL/vault owner fault tests |
| V-SE-06 | Real ambiguous acknowledgement stays unknown and cannot blindly replay; cleanup must not destroy a payload potentially referenced by committed metadata. | Actual owner/transaction semantics + negative controls |
| V-SE-07 | Reference-blocked deletion, transaction/lock policy, legacy protection and runtime resolution preserved. Current missing explicit editor does not silently become a create. | Existing Security integration + UI regression |
| V-SE-08 | Receipt/history/log/URL/snapshot/evidence contain no plaintext/vault key/encrypted payload/full sensitive command, even after error/disposal. | Sentinel assertions + secret/evidence scan |
| V-SE-09 | Private vault/keyring roots isolated; operation completion after host retirement does not cancel known durable facts or reveal old input. | Host lifetime + cleanup proof |

## Files

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-FI-01 | Exact normalized extension captured; late save/delete cannot retarget or reset a newer selected extension/draft. | Real controls + held owner |
| V-FI-02 | Invalid/nonexistent executable refused, inactive/rebind-required path shown accurately, existing extension/host/path semantics retained. | Real owner private-root tests |
| V-FI-03 | Durable write followed by logger/list failure remains known; read-only refresh does not write/replay. Preserve existing documented read/migration behavior. | Owner fault + controller counters |
| V-FI-04 | Machine-local preferences do not migrate to a new database profile or launch a file; owned temporary roots only. | Origin/negative effect tests |

## Provider history

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-HI-01 | Zero automatic policy/history read on tab open; explicit Load uses Manage authorization and current partition. | Existing route test + real owner |
| V-HI-02 | Invalid raw numbers and limit relationships remain invalid without clamp; future-only update uses exact version and does not shorten old rows. | Actual form + persistence |
| V-HI-03 | Shorter retention requires bounded preview and separate exact policy/version confirmation; oversize or stale preview cannot apply. | Dialog/browser + transactional owner |
| V-HI-04 | Auth/profile change invalidates pending read/editor/preview and cancels owned continuation without publishing late data or automatically reloading. | Existing and held lifecycle tests |
| V-HI-05 | Conflict/denied/stale/known result/unknown acknowledgement remain distinct; current-policy observation never proves earlier destructive request completion. | Real owner fault + UI state tests |
| V-HI-06 | Partition/write fences, audit/version and canonical history protections unchanged; only disposable fixture expiry rows change. | Current History persistence/authorization consumers |

## Sandbox, browser and closure

| ID | Required observation | Minimum useful layer |
| --- | --- | --- |
| V-UI-01 | Four actual sections and relevant dialogs operate against real scenario state, not echoed messages; no backend runtime, database or real vault dependency. | Standalone source host + counters |
| V-UI-02 | Pending/fault/recovery/reset, missing references, raw fields and preserved original-store effects are reachable from scenario controls. | Real renderer/browser |
| V-UI-03 | Production `/settings` journey saves actual defaults/secret metadata/payload and file prefs, and performs controlled history update through current owners. | Real Web + owned PostgreSQL/vault/control-plane |
| V-UI-04 | Source and published Production sandbox load theme/font/scoped assets; inspect desktop viewport, caret, focus, overlays, masking and no console/asset errors. | Playwright + screenshot inspection |
| V-UI-05 | Hot reload edit-to-visible Razor/C#/CSS measured separately from startup; graph/watch recorded, no inferred full-Web speedup. | Executed development-loop evidence |
| V-CL-01 | Fresh direct production builds and correct discovery precede targeted executions; skipped/unavailable proof not called green. | Command/receipt ledger |
| V-CL-02 | Full proposed-tree portability scan, intentional baseline review and final no-write enforcement; documentation/evidence/secret gates pass. | Current repository tooling |
| V-CL-03 | Impact explicitly justifies Stable/platform/live widening or non-selection; actual shared-owner changes covered. | Architecture/testing decision record |
| V-CL-04 | Exact owned processes/DBs/vaults/roots cleaned, user app/port5032 untouched, signed local commit policy respected. | Cleanup/commit receipt |

Current test paths from [the source notes](WORKSPACE_REVIEW_NOTES.md) are starting points, not
complete filters. Inspect actual SettingsPageDataSourcesTests, SecretProviderSelectionTests,
ProviderFeatureMatrixTests, file preference tests and all touched Security/history/API consumers
before editing. For non-serializable theory data, record discovery and expanded execution
counts separately. Never guess a passing total from source annotations.
