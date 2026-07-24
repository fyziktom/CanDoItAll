# C# Architecture Gate

## Preparation Decision

- Status: `Pass for implementation, with explicit MCP evidence gap`.
- Basis: exact source/project/test inspection identifies current owners, target boundaries, dependency direction, pattern choices, test seams, partial-class policy, and blocking checkpoints.
- Gap: CodeAnalytics transport was unavailable, so no snapshot/dashboard/findings/cycle proof exists. Components MCP was intermittent: a successful recommendation confirmed the planned components, while a later library-list call failed. These limitations must remain explicit; they are not blockers because source inspection and build/reference/setup gates are defined.

## Required Review Questions

- Which responsibility moved, and which old owner no longer contains it?
- Can the extracted component/service be instantiated and tested without a large Razor page or `CrmService`/`PartyDirectoryService`?
- Does AppComponents remain domain-neutral?
- Do CRM/HR and Projects own their own EF queries and typed filters?
- Are all project references in the allowed direction and cycle-free by available proof?
- Did any new partial/nested service or service-locator path appear?
- Is `PartyPicker` removed or truly thin, with no hidden full-list fallback?
- Are contact tags fully migrated across persistence compatibility paths?
- Are opportunity dialogs isolated drafts rather than live model mutation?
- Are financial unavailable states typed and honest?
- Does workbench title composition use the typed CRM route catalog without polluting main navigation?

## Blocking Findings

Reject the owning subbundle if any is true:

- client-side paging is presented as scalable;
- shared UI imports a domain module;
- a string record-kind/filter/id protocol replaces typed contracts;
- a new partial file expands the old monolith;
- extracted tests still require constructing the old large service/page;
- old page/service retains the moved behavior while the new type is a facade in name only;
- errors silently fall back to dropdowns, stale results, empty charts, or zero metrics;
- contact tags lack migration/round-trip coverage;
- mixed currencies are summed;
- Projects references CRM/HR;
- workbench titles change without stable tab/restore identity proof or add duplicate/flattened main-navigation items.

## Required Proof Per Architecture-Relevant Subbundle

- Before/after project-reference and source-owner assertions.
- Targeted direct tests and at least one adversarial negative test.
- `dotnet build CanDoItAll.slnx --no-restore`.
- No-new-partial audit.
- Old-owner shrink/thin-facade evidence.
- Applicable downstream browser/composition check.
- Explicit checkpoint progression result.

## Execution Review Log

| Checkpoint | Reviewer decision | Evidence | Reopened work |
| --- | --- | --- | --- |
| `CP-01 / SB01` | `Pass` | Domain-neutral typed paging lives in `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowserContracts.cs`; stale/failure/retry component tests and 1,001-row source-paging integration proof passed. | Component failure/retry behavior was repaired during implementation, then its focused proof passed. |
| `CP-02 / SB02` | `Pass` | CRM/HR owns party adapters and privacy filtering; AppComponents has no CRM/HR import. Directory and affected route lists/selectors use bounded query contracts with no full-list picker fallback. | Privacy/tag filtering was tightened before progression. |
| `CP-03 / SB03` | `Pass` | Stable row identity tests cover contact/address/relationship reorder/remove behavior; the contact wizard owns an isolated draft; contact tags round-trip through the integrity migration. | Integrity and atomic import/relationship negatives were added before progression. |
| `CP-04 / SB04` | `Pass` | CRM/HR owns opportunities, Projects owns project search, and AppComponents remains neutral. Query/dialog tests cover paging, stale edits, cancel isolation, missing projects, conversion, and currency labels. | Opportunity validation/concurrency and conversion compensation were hardened before progression. |
| `CP-05 / SB05` | `Pass` | `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmFinancialSnapshotQueryService.cs` owns the read projection; tests prove immutable first-Won recognition, currency separation, incomplete records, errors, and typed unavailable sources. | Recognition immutability and missing-history handling were hardened before progression. |
| `CP-06 / SB06` | `Pass` | The typed CRM route catalog is consumed by Web in the existing direction; ids/routes remain identity keys. Directory/assignment/source-snapshot/activity/agent reads are paged or lazy, and focused navigation/privacy/paging tests passed. Home `AgentProjectionCount` counts only bound `AiResourceBinding` AgentFramework projections. | The EF-untranslatable activity-history `Concat` was repaired with a common anonymous server projection and its exact failing scenarios reran green. Final browser review also removed stale bundle-era Home/CRM copy and aligned the Home label to `Agent projections` before closure. |
| `CP-07 / SB07` | `Pass` | AppComponents owns the typed default-off `PagedRecordResultsScrollMode`; CRM-HR pages own controlled dialog orchestration and generation invalidation; no project reference or new partial was added; focused component proof reports `37/37` passing; populated normal/dialog browser proof passes. | Affected rendered and regression gates closed; no architecture rework required. |
| `CP-08 / SB08` | `Pass` | Web owns `CrmHrApi` transport/DTOs and delegates to canonical CRM-HR query/command services with no direct `DbContext`; real-host positive/negative tests report `2/2` passing; repo/active skill validation and hashes match. | No architecture rework required. SB09 may rely on this HTTP foundation. |
| `CP-09 / SB09` | `Pass` | The external HTTP operator adds no production reference; API-only seed/reconciliation, bounded readback, populated browser/console, focused and broader regressions, and final host proof pass. | No architecture rework required. Reopen on direct persistence/startup seeding or runtime-contract drift. |

## Final Architecture And Performance Review

- Ownership: reusable browser mechanics are in AppComponents; CRM/HR owns party, opportunity, financial, staffing, activity, and agent projections; Projects owns project search. No shared UI domain dependency or Projects-to-CRM/HR inversion was introduced.
- Testability: new query services and dialog/browser components have direct component, unit, or PostgreSQL integration seams and do not require exercising the full CRM page for their core behavior.
- Composition: new services/components are top-level cohesive types. No new feature partial was used to expand `CrmHrServices.cs`; the existing broad aggregate remains a measured follow-up rather than a reason to add another layer in this change.
- Fallback audit: party/project selectors do not accept a hidden full-list options fallback; missing ids, stale edits, loader failures, incomplete recognition, and unavailable financial sources fail or render explicitly.
- Data integrity: `20260724114400_ImproveCrmHrRecordSelectionAndRecognitionIntegrity` protects contact/relationship/opportunity/recognition behavior. `20260724144440_OptimizeCrmHrHighCardinalityQueries` adds scoped lookup/history indexes and persisted AI-resource projection fields; its `Down` is reversible and the final EF drift check passed.
- Performance: source snapshot paging performs `Count`/stable `Order`/`Skip`/`Take` before related lookups; duplicate-import candidates are capped and queried in batches; activity history, project assignments, directory assignments, and high-cardinality route catalogs are paged or lazy; AI-agent page reads and the Home KPI use bound persisted `AiResourceBinding` AgentFramework projections rather than enumerating the technical catalog or counting legacy `AiAgentProfile` rows.
- Follow-up performance result: populated Directory and Workforce retained bounded source pages and bounded result-region scrolling; no critical performance anti-pattern was found in the new API/operator or catalogue/dialog path.
- Runtime agreement: the public API seed/repeat/readback and actual Directory, Workforce, Recruiting, record-dialog, console, and port `5032` host checks agree; the API remains transport-only over canonical services.
- Deferred measurements: selected-party workforce capacity/allocation reads should be profiled at production volume, assignment `ToUpper().Contains` is non-sargable, and the pre-existing `CrmHrServices.cs` aggregate should be decomposed only under measured change pressure.
- Evidence gap: CodeAnalytics and Components transports were unavailable during final review. Direct dependency/source review, focused tests, a zero-error Release solution build, EF drift proof, application startup, and browser inspection mitigated the gap.

## Historical SB01-SB06 Final Gate

- Historical status: `Pass`.
- CP-01 through CP-06 passed in the original closure. The independently reviewed follow-up CP-07 through CP-09 gates now also pass.
- The repository-wide `System.Security.Cryptography.Xml` `10.0.7` `NU1903` advisory and unrelated broad-suite baseline failures remain explicit follow-ups; neither is misrepresented as green closure evidence.

## Follow-Up Re-entry Decision

- Status: `Pass`; SB07, SB08, and SB09 implementation and applicable Behavioral proof pass.
- The follow-up preserves existing source paging and application-service ownership. Its UI delta is a domain-neutral opt-in scroll modifier plus page-owned controlled dialogs; its API delta is a thin Web transport adapter; scenario orchestration remains external.
- Rejected expansions: copying the Agents in-memory data model, extracting duplicate editor state machines, adding direct persistence/seed routes, inventing generic repositories, or claiming JWT scope enforcement that the current API platform does not provide.
- CodeAnalytics and Components transports remain unavailable. Explicit source ownership assertions, project-reference inspection, targeted tests, the Release solution build, and inspected browser/runtime behavior supplied the final gate evidence.

### Closed Follow-Up Finding Audit

No listed rejection condition was observed: scrolling remains typed/default-off, dialogs are controlled rather than permanent hidden panes, stale-load behavior is tested, Web contains no direct persistence, list responses are bounded, seeding is external HTTP-only, skill/runtime behavior agrees, and workbench identity remains route-based.

## C# Architecture Gate Result

Status: Pass

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | Shared catalogue mechanics remain domain-neutral and typed. | `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowserContracts.cs` owns `PagedRecordResultsScrollMode`; CRM-HR adapts it in `PartyRecordBrowser.razor`. No `.csproj` changed. | None. |
| None | CRM-HR HTTP transport preserves the existing dependency direction. | `repo://src/App/CanDoItAll.Web/Api/CrmHrApi.cs` and `CrmHrApiContracts.cs` depend on CRM-HR application contracts/services and contain no direct `DbContext`, `IServiceProvider`, or `BuildServiceProvider` access. | None. |
| None | No partial-class expansion or service-locator shortcut was introduced. | Static source audit of the follow-up shared-browser/API seams returned no new `partial class`, `IServiceProvider`, `BuildServiceProvider`, `AppDbContext`, or direct `DbContext` path. | None. |
| Follow-up | CRM-HR routes inherit the current parent API authorization only; per-CRM-HR scope enforcement is not present. | `repo://src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs` owns conditional group authorization, and `repo://codex/skills/candoitall-api-crmhr/SKILL.md` states this limitation explicitly. | Do not claim scope isolation. Reopen the API boundary if per-domain scopes become a requirement. |
| None | SB07 Behavioral UI agrees with the source/test architecture. | `bundle://proof/SB07/browser-normal-and-dialog-review.md` records full-width bounded catalogues, actual paging, controlled dialogs, and usable scroll/action regions. | None. |
| None | SB09 runtime orchestration preserves the external-client boundary. | `bundle://proof/SB09/seed-first-run.md`, `seed-repeat-run.md`, `api-readback.md`, `browser-review.md`, and `host-5032.md` agree without a production reference or direct persistence path. | None. |
| Evidence gap | CodeAnalytics and Components transports remain unavailable. | Direct project/source inspection, targeted tests, and a zero-error Release solution build provide the available architecture evidence. | Re-run tool-backed dependency/component review if the transports become available before final closure. |

### Dependency direction

The follow-up keeps `CanDoItAll.AppComponents` domain-neutral, keeps page orchestration in CRM-HR, and places HTTP transport in Web over existing CRM-HR services. No project-reference change or cycle was introduced. SB09 remains an external HTTP client and must not add a production dependency.

### Partial-class policy

No new feature partial was added. The UI/API work uses cohesive top-level components, pages, route contracts, and endpoint files; the pre-existing broad `CrmHrServices.cs` aggregate was not expanded through a new partial.

### Testability proof

- SB07 focused component selection reports `37 passed`, `0 failed`, `0 skipped`, including typed opt-in scroll, controlled-dialog deep links, stale-close invalidation, freshness, stable route-title identity, and the populated Recruiting render-race regression.
- SB08 real-host `CrmHrApiIntegrationTests` report `2 passed`, `0 failed`, covering a linked HTTP scenario and meaningful invalid-reference/query failures.
- Broader affected CRM-HR integration regression reports `35 passed`, `0 failed`.
- The final Release solution build completed with `0 errors`; the existing `NU1903` advisories remain explicit.
- Inspected SB07 and runtime SB09 proof is recorded separately from test results in `bundle://proof/README.md`.

### Closure decision

The C# dependency, ownership, construction, partial-class, testability, performance, and applicable Behavioral checks pass. CP-01 through CP-09 and the final architecture gate pass. The bundle may close; the existing authorization-scope limitation, `NU1903` advisory, unrelated all-unit debt, and unavailable tool transports remain explicit follow-ups.
