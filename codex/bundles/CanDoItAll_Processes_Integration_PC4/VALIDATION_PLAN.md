# Validation plan: exact affected evidence, not repeated whole-suite runs

## Source and environment

Read current `docs/testing.md` and CI before choosing commands. Record branch/HEAD/tree,
source deltas, public sibling SHAs, SDK, platform, configuration, test assembly fingerprints,
owned database server version and sanitized resource identifiers. Never output a database
password or entire environment. Use isolated task-owned PostgreSQL 18 and ephemeral loopback
hosts; leave the ordinary application and retained resources untouched.

The reviewed PC3 report used FileTools `3a080ecd31068a77c1e1bd639f7a78e21c93db85`, while
reviewed CI pins `498b36825bd5a5222429972af120b04becf4b3f6`. This is a **verification question**,
not proof that either fails. Inspect current values. Build at the intended clean source tuple;
if a different sibling is truly necessary, identify the exact required public change, ensure
it is available and pin/test it using normal repository policy. Do not silently move siblings
to 'latest' or certify a different pin using local outputs from an unrecorded sibling.

## CodeAnalytics and discovery

Discover the installed CanDoItAll CodeAnalytics MCP and record index root, actual source/index
revision, freshness and queries. Query changed symbols and reverse call/type/project/test
edges; inspect exact production consumers. Supplement dynamic Razor, DI, reflection/source-gen,
HTTP routing, serializer/legacy payload, fixture and browser asset edges. New regressions have
no index entries until written. A source text search is not a semantic index result.

If unavailable, record the failed availability check, use explicit source/references and
build/DI inspection as fallback, and mark the reduced selection confidence. Do not fabricate
MCP outputs or skip known consumers. Never assume default-branch GitHub search is the reviewed
branch. Recalculate impact after later repairs or configuration/sibling changes.

For each selected lane, build changed production projects directly; refresh the owning test
assembly, run `--list-tests` with the exact filter, record expected/discovered counts including
expanded theories, then execute. Zero or unexplained count mismatches invalidate evidence.
Use no-build/no-restore only with a current matching assembly/source/configuration tuple.

## Required selection families

These are existing source-discovery anchors, not fixed permanent counts or an instruction to
run every test assembly. New PC4 cases must be added to the selected owning families.

| Lane | Existing anchors | Minimum oracle |
| --- | --- | --- |
| Unit | ProcessLaunchProducerRequestTests, ProcessLaunchAtomicCommitTests, ProcessAuthoringSemanticCoverageTests, definition/resolver/preparer tests | Fingerprint and replay rules, exact closure/identity, empty drivers, no hidden-field loss |
| Integration | ProcessAuthoringPublicationTests, ProcessAuthoringWorkspaceTests, ProcessAuthoringNativeCompatibilityTests, ProcessAuthoringSemanticTests and affected store/migration tests | Native scopes, real request/receipt, role resource sequence, predecessor payload compatibility and immutable plans |
| HTTP/native consumers | ProcessLaunchProducerApiTests, ProcessApiIntegrationTests, ProjectStructureAgentIntegrationTests, ProjectStructureTaskHttpBoundaryTests | Real project+node API enrichment, direct native and subprocess caller paths, no duplicate effect |
| Component/host | ProcessWorkspaceShellTests partials, affected role state, ProjectStructurePageProcessLaunchScopeTests, ProjectStructurePageAssetAndActivationTests | Editable native hosts, late results, selection/raw draft and captured source lifetime |
| Browser | ProcessAuthoringBrowserTests, ProcessAuthoringMultiSessionBrowserTests, ProcessAuthoringRestartBrowserTests plus new PC4 journey | Native delete/import/read-back, project API/native consistency, old preparation and exact workflow after restart |
| Preservation floor | ProcessNativeBrowserTests, ProcessWorkbenchBrowserTests, ProcessVoiceBrowserTests, relevant files/cancel/chat/activity and sandbox tests | Preserve original workflow/agent/file/voice/cancellation integrations where invalidated |
| Contracts/static | ApiDocumentationCoverageTests, HostPlatformTestClassificationTests, dependency/public-type guards, canonical model/transfer coverage | API text and actual contract, appropriate host traits, no forbidden UI dependencies or schema drift |

Select full bounded classes when member/theory coverage or partial files could otherwise be
missed. Some live dashboard cases live in ProcessWorkspaceShellTests. Test descriptions must
not be weakened to match a regression, and production behavior must not be altered solely
because an echoing fake lacks required metadata.

## Product lanes and broad closure

While iterating, run targeted tests, not the full Stable aggregate or unfiltered giant
projects. The expected PC4 launch-selection/recovery changes affect native authority and
multiple consumers: evaluate current Broad Stable invalidation rules and schedule the
required broad campaign **once the implementation and focused lanes are stable**. Do not
run it once per stage. If current drift resolves the findings without such invalidation,
record the concrete reason rather than running broad tests ceremonially.

If a broad run fails, preserve its source, exact command, all failed identities and result.
Repair causes, rebuild and run all affected identities/consumers. Apply the current repository
policy for source-bridged/composite closure; never relabel the original run PASS or sum
intersecting selections into a fictitious independent total. A source, sibling, configuration,
shared fixture or build change can invalidate prior evidence. Record retained versus replaced
sets with exact identities and rationale; no claim of a second complete run without one.

Browser, HostPlatform, LiveProcess, actual restart, paid/live external inference and
portability lanes are separate. Exclusion from Stable is not a waiver or a PASS. Run required
browser journeys explicitly. No paid external model is needed for deterministic fixtures;
make no live-model claims. Retain cleanup assertions and investigate flaky voice/network
failures instead of hiding them with unbounded retry or disabled checks.

## Mandatory static and packaging checks

Run current `portability-static` scan, inspect added/stale findings, repair real defects,
review any intentional baseline diff and finish with enforcement without write-baseline.
Check XML/OpenAPI descriptions, public projection/DI contracts and light UI dependency
closure. Run canonical pending-model checks when owner/model code could be affected. No
schema edit is expected; if one becomes necessary, include populated migration/transfer
coverage and no destructive SQL. Preserve existing transfer refusal tests.

Build required production and sandbox outputs in standard Debug/Release configurations
and serialize shared-output writes before launching
browser hosts. Use the current Fast/Parity asset protocol if any assets or graph changed;
otherwise reuse only evidence justified by unchanged source and still run the affected
native UI journey. Record host startup/build/query samples under identical conditions if
selection/refactoring changes the hot path. Do not claim statistically established speedup
from one sample or impose a fabricated latency threshold.

## Evidence and commits

Use [evidence-template.json](evidence-template.json). Each outcome belongs to its executed
source tuple and exact discovery/execution command. Product results start `NOT_RUN`.
Historical PC3 report claims are labeled REPORTED, not imported into current PASS rows.
Create tested signed checkpoints and one report-only signed commit if appropriate; the
report references the tested source commit, avoiding a circular self-hash requirement.
