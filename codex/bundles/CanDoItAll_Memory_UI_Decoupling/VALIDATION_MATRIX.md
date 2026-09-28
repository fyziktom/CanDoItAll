# Validation matrix

Read the current repository testing and CI policy before executing. Counts below
are **obligation IDs**, not test case counts. Determine exact expected, discovered
and expanded/executed counts from the current source and use the identical filter.
No test in this matrix has been executed by the reviewer. [EV08]

## S0: bounded Scheduler repair

| ID | Required observation | Minimum layer |
| --- | --- | --- |
| V-S0-01 | Two independently held exact-ID reviews cannot overwrite a later save's Pending receipt/submission or release its same-plan lock | Deterministic presentation test, then actual review control |
| V-S0-02 | After the stale-review ordering, a newer Unknown save remains recoverable; unrelated plan can progress | Presentation/owner-port test |
| V-S0-03 | Late success, mismatch, exception and finalization from save/flag/delete review cannot patch a newer receipt or disposed workspace | Per-branch regression; both review methods |
| V-S0-04 | Changing parent removes an old optional child's diagnostic; fresh validation is reached; required child/unrelated error/raw malformed input remain correctly enforced | Actual typed controls plus authoritative validator assertions |
| V-S0-05 | Existing field revisions, immutable submission, independent reads, retained drafts and exact calendar identity continue to pass | Targeted existing Scheduler light/page tests and affected browser journey |
| V-S0-06 | Prior Plugins selection and package-stage repairs remain present; rerun relevant proof only when their dependencies change | Source/drift review plus impact-selected tests |

A test that merely invokes the second call after the first completed does not cover
V-S0-01. Own each completion signal, await all test tasks and bound test waits. Do not
add arbitrary sleeps or assert a transient button flag as operation completion.

## M1/M2: real boundary and compatibility

| ID | Required observation | Minimum layer |
| --- | --- | --- |
| V-BD-01 | Current module/descendant/asset/public consumer map and before-state proof recorded | Source + evaluated graph + baseline |
| V-BD-02 | Light UI and sandbox exclude concrete module/transport/runtime/persistence/Web graph; unresolved edges fail | Evaluated source-mode closure + negative direct/transitive tests |
| V-BD-03 | All seven sections and meaningful real children render; production uses the same shipped surface | Light component + real routed page |
| V-BD-04 | Profile and operation contracts preserve exact IDs, defaults and meaningful status/acceptance facts; actual consumers still compile | Mapper/serialization/API tests selected by impact |
| V-BD-05 | HTTP, NativeRemote and MCP profile round trips preserve vendor JSON, limits, capabilities, UI surfaces and environment references; legacy raw credentials do not reappear | Existing and expanded mapper/configuration tests |
| V-BD-06 | Public Type/fragment extension boundary does not hide required backend dependencies; actual registered panel still works | Boundary + production extension-host test |
| V-BD-07 | New light tests appear in correct Components/Stable/CI membership; no tests enter product graph | Solution/CI inventory and build-backed discovery |

An assembly name or absence of direct EF is not a complete boundary test. Inspect
runtime/public-type and project/package closure, including source replacements.

## M3: host state, request snapshots and outcomes

| ID | Required observation | Minimum layer |
| --- | --- | --- |
| V-ST-01 | Same provider/tab/refresh and unrelated action preserve unblurred profile fields, nested transport, tags, validation and actual editor context | Real controls + focus browser check |
| V-ST-02 | Whole request is detached before guard/validation await; later driver/capability/query/provenance changes cannot alter accepted request | Controlled owner-entry delay, request equality and dispatch count |
| V-ST-03 | A -> B -> A reads/results/errors/finally do not overwrite successor selection, editor or result; no old action forces current tab | Presentation + actual component |
| V-ST-04 | Explicit missing selection is not rebound to first provider; remaining list is selectable; empty -> repopulated behavior is defined | Actual workspace surface |
| V-ST-05 | Duplicate direct handler invocation sends one operation; older completion cannot release newer gate; independent target remains usable | Deterministic admission tests |
| V-ST-06 | Known profile save/accepted query identity survives failed follow-up; retry reads only; newer raw edits survive | Presentation and real owner/ledger read-back |
| V-ST-07 | Genuine unknown dispatch keeps original provider/submission and no automatic replay; explicit review is fenced against newer operations | Controlled fault + actual recovery UI |
| V-ST-08 | Selection/disposal cancels reads but does not fabricate rollback or redirect old scenario writes; per-page state is not shared across mounts | Two-workspace lifecycle tests |
| V-ST-09 | Editing InstanceId preserves existing upsert semantics and captures explicit destination; no implicit rename/delete of original ID | Real persistence + component |
| V-ST-10 | Partial two-profile demo result is not called atomic; action is explicit and confirmed profiles are not silently recreated | Real profile-store fault ordering + UI receipt |
| V-ST-11 | Page Refresh performs store reads only; explicit operation-status call remains distinct and target-bound | Owner/driver call counters |
| V-ST-12 | Profile change or dependency failure yields unavailable/stale state, not another profile's data; query/context-pack/feedback handle provenance stays exact | Profile-scoped owner tests |

For unsupported operations V-ST outcomes may use clearly labelled synthetic display
fixtures. Such fixtures do not establish production capability, persistence or an
owner commit. Do not widen the runtime to make the table executable.

## Capabilities and dynamic Provider UI

| ID | Required observation | Minimum layer |
| --- | --- | --- |
| V-CP-01 | Zero-provider state creates no demos/workers and has meaningful unavailable query state | Actual module and sandbox |
| V-CP-02 | Healthy enabled Mock sync query goes through real guard/handler and returns correct context/ledger semantics | Actual handler with harmless shipped driver + isolated persistence |
| V-CP-03 | MCP configured async query/status contracts retain accepted operation semantics through a controlled transport; live provider claims remain separate | Affected Memory transport/handler tests, UI display |
| V-CP-04 | Unsupported ingestion, feedback, event acknowledgement and cancellation remain refused before dispatch for shipped drivers, even with stale imported claims | Direct real guard + page controls |
| V-CP-05 | Disabled/unhealthy provider, missing capability/tool and unsupported driver choices stay denied; no permission/feature-default expansion | Existing policy tests plus affected integration |
| V-CP-06 | Registered RCL receives correct Provider/Surface, missing key blocks, switch/disposal retires old instance | Real component registration and dynamic host |
| V-CP-07 | Safe iframe/external URL render; HTTPS/loopback rule and userinfo/query/fragment restrictions hold; required capability/health checks cannot be bypassed | Projector tests + actual DOM/browser fixture |
| V-CP-08 | Query/operation records preserve dispatch attempt, async acceptance, exact handles and unmatched feedback meaning; no synthetic trust promotion | Exhaustive mapping/negative tests |
| V-CP-09 | Secrets/legacy credentials/provider exception contents do not leak through generic notices, URLs, attributes, snapshots or logs | Targeted negative tests + artifact scan |

## Browser, assets and development loop

| ID | Required observation | Minimum layer |
| --- | --- | --- |
| V-BR-01 | Complete sandbox seven-tab journey with real BaseLib children, profile transports, dirty states and deterministic control of pending results | Playwright on backend-free sandbox |
| V-BR-02 | Real `/memory` production route: explicit demo/configure/save, real supported query, exact owner/ledger read-back, refusal and selection while held result returns | Task-owned production host/PG; controlled delay at named boundary only |
| V-BR-03 | Actual unblurred typing and focus survive read-back/tab/refresh where draft survives; stale result never activates a new tab | Browser interaction, not Change-only component events |
| V-BR-04 | Provider UI variants, disabled actions, unavailable ledger regions and reset/disposal remain coherent; no browser/circuit errors | Sandbox + production host checks |
| V-BR-05 | Scoped CSS, theme, fonts and scripts resolve in source and published standalone host; computed geometry matches supported desktop layout | Publish + network/computed-style/screenshots |
| V-BR-06 | Warm edit-to-visible observations for relevant Razor/C#/CSS, actual graph and watch set; unsupported JS measurement marked not applicable | Recorded commands and executed probes, not predicted speedup |

## Repository closure

| ID | Required observation | Minimum layer |
| --- | --- | --- |
| V-CL-01 | Direct changed production builds and all selected filters have current assembly/discovery/expanded case provenance | CLI transcripts and durable receipt |
| V-CL-02 | Full proposed-tree portability scan, individually reviewed baseline delta and final enforcement without baseline writing | Current mandatory repository gate |
| V-CL-03 | Documentation/evidence/secret checks and owned resource cleanup pass; normal app/DB/watch untouched | Current tools + resource ownership records |
| V-CL-04 | Stable/platform/runtime widening decision follows actual invalidation triggers, not routine full-suite repetition or unreasoned omission | Explicit impact and gate statement |

## Test routing and evidence

Known entry points remain `tests/Solutions/CanDoItAll.Tests.{Unit,Components,Integration,Memory,Playwright,Stable}.slnx`.
Use the actual owning project for narrow tests, including new light Memory tests,
existing Memory page/round-trip/validation/provider-surface tests, actual Memory
runtime/driver consumers and affected API/configuration tests. Do not prescribe an
unfiltered `FullyQualifiedName~Memory` run without first assessing its contents.

Use the repository's async bUnit event/disposal helpers. Re-query and dispatch
within the renderer's invocation where required, observe pending before releasing
a controlled operation, then await its task. Playwright must await the exact target
and completed result, not button existence or a fixed delay.

For every obligation record requirement ID -> concrete test/command -> owner/test
boundary -> actual count/result -> artifact path. One test can cover several IDs;
a screenshot cannot stand in for dispatch or persistence proof. Record skipped,
failed, blocked, not run and not applicable separately. Preserve failing-first
results as failures demonstrating the bug, not successful final runs.
