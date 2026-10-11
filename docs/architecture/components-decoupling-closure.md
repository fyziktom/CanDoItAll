# Components decoupling closure

CD1 is **in progress**. This record is the current closure register; historical family
receipts and the shared bundle retain their original scope and results.

## Current decision

`CanDoItAll.Application.UI` owns the Dashboard, Runtime Capabilities and safe database
profile presentation. It depends on the existing AppComponents and BaseLib presentation
closure. Web retains routes, snapshot/cache/timer policy, capability probes, database
profile authority, navigation, startup gating and application context. The application
root and framework recovery remain native.

The Dashboard's immutable display records move into the light assembly; consumers are
explicitly migrated. They are application-only values, not an HTTP serialization contract.
The clock remains native and supplies the render timestamp. Activity selection stays in
the host. Existing identity-based link construction is a neutral route contract, protected
by the real Home page assertions. No cache or polling service is added to the renderer.

Runtime and database models cross the seam as narrow, safe display projections. Their
native enums continue to determine authority and readiness; display text never determines
permission. Database activation carries the rendered profile identity. Native admission
and opening lifetime must protect repeated callbacks and late completion.

A single application UI library is justified by independent rendering of these application
operations. AppComponents remains feature-neutral; Configuration.UI remains a schema-field
renderer. A view-controller framework, new transport, second cache and backend redesign
are unnecessary. No new partial classes will be added to the native hosts.

## Composition and proof

The Dashboard keeps compact header statistics, four quick actions, six project cards and
two activity tabs. Runtime keeps the full capability card and its real CapabilityFact
child. Database profiles keep the Wide dialog, safe copy controls, status distinctions and
stable footer. Utilities and status retain their current compact shell composition. The
existing AppShell body owns page scrolling; dialog bodies own their own overflow. Desktop
proof targets 1920 × 1080, with constrained desktop geometry for flyouts and overlays.

Characterization includes HomePageTests, DashboardSnapshotCacheTests,
DashboardSnapshotLoadRunnerTests, DashboardSnapshotLoaderTests, RuntimeCapabilitiesPageTests,
MainLayoutDatabaseProfileTests, MainLayoutTopBarTests, MainLayoutRetirementTests and
MainLayoutCollaborationTests. Independent component tests must exercise the actual leaf
without Web registrations. Browser journeys must prove native integration, publication,
assets and cross-family shell behavior. Expected discovery is derived for each exact lane
before execution; historical results are never counted as fresh execution.

The final broad-gate decision will name actual invalidation from the new project boundary,
root solution registration and shell composition after the focused lanes settle. Static
portability and maintained-documentation checks remain mandatory. No completion is claimed
from the lexical census, project names or package verification.

## Entry evidence

- Application: `663fb1391e4acb12439173f1cb1d287fb1f24685`, clean
  `components-decoupling`; only CD1 package additions since review
  `fd9e43547457eb18901185da47de123efdfd8108`.
- Components: `f3745356182444656edfdef56aa827095632d3ea`; FileTools:
  `3a080ecd31068a77c1e1bd639f7a78e21c93db85`; both clean.
- SharedInfo: `30330978cf48d702382663a3d5231880fc034936`; SDK 10.0.303,
  Windows, Release, sibling source dependencies.
- CodeAnalytics snapshot `snap-20261011111130-dacba1c1`: inventory reports 215 projects,
  4,684 documents, 208 product and seven supporting test projects. Build/dashboard
  responses exceed the transport limit; the persisted snapshot was inspected locally.
  Its 788 diagnostics comprise 602 duplicate generated/top-level type warnings, 95 partially
  interpreted DI factories, 27 unresolved DI registrations and 64 truncated diagram notices.
  No workspace-load failure is reported. The 12,384 types, 92,312 members and 47,003 type
  relationships support the census; ambiguous DI and dynamic rendering require source and
  runtime checks. Diagram truncation is not used as a completeness certificate.
- The optional lexical collector read 1,119 entries with zero unreadable inputs. Dynamic
  activation, public types, evaluated references and assets require complementary review.
- Baseline Web build passed in 123.8 seconds with one existing nullable warning in
  StructureWorkspace.razor. CD1 package verification and its 34 helper tests passed.
- Local GPG challenge signed and verified with fingerprint
  `96E836FAA8854EE98ABC10903C206549E1D7EAD6`; commit verification follows each checkpoint.

## Dashboard and readiness checkpoint

The actual Home and Runtime Capabilities routes now consume the independent application
renderers. Existing native cache, timer, failure, cost and capability meanings are retained.
The focused native selection discovers and passes 41 cases across HomePageTests,
DashboardSnapshotCacheTests, DashboardSnapshotLoadRunnerTests, DashboardSnapshotLoaderTests
and RuntimeCapabilitiesPageTests. The independent suite discovers and passes 14 cases,
including actual capability children, separate views and direct/transitive/unresolved
dependency negative controls. Both owning test assemblies were rebuilt in Release.

The entry Web executable, built before production edits, reached HTTP 200 at
`/settings/runtime-capabilities` in 8.33, 9.20 and 9.27 seconds (three launches, the same
seeded private PostgreSQL 18 database, warm filesystem). This measures server startup,
not interactive circuit readiness or edit-visible time. An earlier probe used an incorrect
URL and timed out; that failed attempt is retained separately. No speedup is claimed.

Portability review at this checkpoint moves two existing `/processes/live` lexical findings
with the Dashboard and adds the documented route and shell code-fence findings. These are
route/documentation text, not process discovery or elevation. Both portability tool suites
pass and full no-write enforcement passes with 15,383 reviewed executable-source findings.
The final source, shell, browser and broad gates remain required.

## Remaining closure

The five application clusters, full source-addressed census, forbidden-edge guards,
independent/native/browser/published/development-loop proof, final required integration,
index repair and next-refactor handoff remain open. PC4's captured launch, retained-origin
recovery and role-resource corrections remain unchanged. Authoring-aware transfer/export,
large catalog paging and future backend ownership refactoring remain separate work.
