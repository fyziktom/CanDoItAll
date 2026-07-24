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

## Final Architecture And Performance Review

- Ownership: reusable browser mechanics are in AppComponents; CRM/HR owns party, opportunity, financial, staffing, activity, and agent projections; Projects owns project search. No shared UI domain dependency or Projects-to-CRM/HR inversion was introduced.
- Testability: new query services and dialog/browser components have direct component, unit, or PostgreSQL integration seams and do not require exercising the full CRM page for their core behavior.
- Composition: new services/components are top-level cohesive types. No new feature partial was used to expand `CrmHrServices.cs`; the existing broad aggregate remains a measured follow-up rather than a reason to add another layer in this change.
- Fallback audit: party/project selectors do not accept a hidden full-list options fallback; missing ids, stale edits, loader failures, incomplete recognition, and unavailable financial sources fail or render explicitly.
- Data integrity: `20260724114400_ImproveCrmHrRecordSelectionAndRecognitionIntegrity` protects contact/relationship/opportunity/recognition behavior. `20260724144440_OptimizeCrmHrHighCardinalityQueries` adds scoped lookup/history indexes and persisted AI-resource projection fields; its `Down` is reversible and the final EF drift check passed.
- Performance: source snapshot paging performs `Count`/stable `Order`/`Skip`/`Take` before related lookups; duplicate-import candidates are capped and queried in batches; activity history, project assignments, directory assignments, and high-cardinality route catalogs are paged or lazy; AI-agent page reads and the Home KPI use bound persisted `AiResourceBinding` AgentFramework projections rather than enumerating the technical catalog or counting legacy `AiAgentProfile` rows.
- Deferred measurements: selected-party workforce capacity/allocation reads should be profiled at production volume, assignment `ToUpper().Contains` is non-sargable, and the pre-existing `CrmHrServices.cs` aggregate should be decomposed only under measured change pressure.
- Evidence gap: CodeAnalytics and Components transports were unavailable during final review. Direct dependency/source review, focused tests, a zero-error Release solution build, EF drift proof, application startup, and browser inspection mitigated the gap.

## Final Gate

- Final status: `Pass`.
- Every checkpoint passed, no listed blocking finding remains in the affected paths, and the source/test/migration/browser evidence agrees with `reviews/01-execution-report.md`.
- The repository-wide `System.Security.Cryptography.Xml` `10.0.7` `NU1903` advisory and unrelated broad-suite baseline failures remain explicit follow-ups; neither is misrepresented as green closure evidence.
