# Testing

`CanDoItAll.slnx` is the product build graph. It deliberately contains no test or
test-support projects. Local and bundle verification starts with the affected production
project and the narrowest owning test topic; it does not start with every test assembly.
Browser, live-process, long-running, Docker-host, runtime-portability, and quarantined
tests are separate lanes and must not be described as passing unless their exact commands
pass.

## Prerequisites

The primary solution covers provider-neutral Memory and its isolated provider drivers.
Native Cognitive Memory implementation tests run only in the standalone repository and
are not a prerequisite for this solution. The default build graph requires sibling
`CanDoItAll.Components` and `CanDoItAll.FileTools` repositories as documented in the root
README. DotNetWatch integration tests additionally require the sibling `CanDoItAll.Mcp`
repository.

## Test Entry Points

| Entry point | Scope |
| --- | --- |
| `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | Unit tests |
| `tests/Solutions/CanDoItAll.Tests.Components.slnx` | Component and component-host tests |
| `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | HTTP, persistence, and cross-boundary integration tests |
| `tests/Solutions/CanDoItAll.Tests.Memory.slnx` | Provider-neutral Memory and AgentFramework Memory tests |
| `tests/Solutions/CanDoItAll.Tests.Playwright.slnx` | Browser automation only |
| `tests/Solutions/CanDoItAll.Tests.Stable.slnx` | Unit, Components, Integration, and both Memory projects |

The stable aggregate excludes the Playwright project. Its command filter also excludes
special traits because those tests remain in their owning assemblies for focused and
environment-specific execution. Test-support projects are transitive dependencies of
their owning test projects and are not standalone gates.

## Local And Bundle Loop

For each implementation slice:

1. Build every changed production project directly.
2. Select the owning suite entry point from the table above.
3. Use an exact `FullyQualifiedName=<namespace>.<class>.<method>` filter when one behavior
   owns the change. Use a bounded `FullyQualifiedName~<topic>` filter or explicit `|`
   expression only when the change intentionally spans that topic.
4. State the expected discovery count before execution. Run `--list-tests` for every new
   or changed filter. Zero tests or a count different from the expectation invalidates
   the proof; data-driven case counts must be included in the expectation.
5. Execute the filtered tests. `--no-build --no-restore` is valid only after the owning
   test assembly has been refreshed for the current source state.

Example for a one-case provider-policy change:

```powershell
$affectedProject = "./src/MAF/Common/CanDoItAll.AgentFramework.Providers/CanDoItAll.AgentFramework.Providers.csproj"
$testSolution = "./tests/Solutions/CanDoItAll.Tests.Unit.slnx"
$testFilter = "FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.OpenAiRequestCompatibilityPolicyTests.Luna_chat_completions_function_tools_require_explicit_none"
$expectedDiscovery = 1

dotnet build $affectedProject --configuration Release /m:1
dotnet test $testSolution --configuration Release --list-tests --filter $testFilter /m:1
# Verify that discovery reports $expectedDiscovery test case before executing it.
dotnet test $testSolution --configuration Release --no-build --no-restore --filter $testFilter /m:1
```

Do not run an unfiltered test project or the stable aggregate merely because a bundle
phase completed. The bundle proof must record the production projects built, the exact
filter, expected and actual discovery counts, and the filtered result.

Component tests that dispose rendered components while reusing a `BunitContext` must use
`DisposeRenderedComponentsAsync()` from `BunitContextLifecycleExtensions`. In bUnit 2.7.2,
calling `DisposeComponentsAsync()` outside a busy renderer dispatcher can clear the root
list before the queued disposal reads it, leaving old components and their scoped
registrations alive. The helper dispatches the entire operation; its regression test
holds the renderer busy to verify that disposal still releases the rendered components.

### Prompt Gallery UI slice

For changes under `src/UI/CanDoItAll.Prompts.UI`, `src/Modules/CanDoItAll.Modules.Prompts.Contracts`,
the Prompt Gallery hosts in `src/Modules/CanDoItAll.Modules.Prompts` or the
`src/Sandboxes/CanDoItAll.Prompts.UiSandbox` host, build the changed production projects and run
the owning slices with a stated discovery count:

```powershell
dotnet build ./src/Modules/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj --configuration Release /m:1
dotnet build ./src/Sandboxes/CanDoItAll.Prompts.UiSandbox/CanDoItAll.Prompts.UiSandbox.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts." /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts." /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Components.Prompts." /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.Prompts." /m:1
```

The trailing dot keeps the unrelated `PromptsDbContextTests` out of the Unit filter. Browser
evidence for the production page is `FullyQualifiedName~PromptGalleryBrowserTests` in the
Playwright project; the sandbox is exercised through `PromptsSandboxTests` and manually via
[its README](../src/Sandboxes/CanDoItAll.Prompts.UiSandbox/README.md).

For changes under `src/UI/CanDoItAll.CrmHr.UI`, the CRM / HR Home host in
`src/Modules/CanDoItAll.Modules.CrmHr` (`CrmHrHomePage`, `CrmHrHomeReadSession`,
`CrmHrHomePresentationMapper`) or the `src/Sandboxes/CanDoItAll.CrmHr.UiSandbox` host, build the
changed production projects and run the Home topic with a stated discovery count:

```powershell
dotnet build ./src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj --configuration Release /m:1
dotnet build ./src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/CanDoItAll.CrmHr.UiSandbox.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrHome" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrHome" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrHome" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrHome" /m:1
```

The existing Home facts on the real component harness (`OpportunityBoardTests.Home_page_surfaces_open_pipeline_preview`,
`CrmHrPrivacyBoundaryTests.Home_and_workforce_routes_surface_sensitive_handling_and_history`) and
`CrmHrNavigationTests` stay the composition baseline. Browser evidence for the production route
is `FullyQualifiedName~CrmHrHomeBrowserTests` in the Playwright project; the sandbox is exercised
through `CrmHrHomeSandboxTests` and manually via
[its README](../src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/README.md).

For the account summary and activity history surfaces (`Accounts/`, `Activity/` in the
rendering library, the `AccountSummaryPanel` and `InteractionTimeline` adapters, the two
presentation mappers, or the `/crm-hr/account-activity` sandbox specimen), run the topic with a
stated discovery count; the filters select the mapper, surface, adapter, sandbox and boundary
classes by their `CrmHrAccount` and `CrmHrActivity` prefixes:

```powershell
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrAccount|FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrActivity" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrAccount|FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrActivity" /m:1
```

The CRM, Directory and Workforce pages compose the adapters, so the whole CRM / HR Components
topic (`FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.`) is the composition baseline for
an adapter change. Browser evidence is `FullyQualifiedName~CrmHrAccountActivityBrowserTests`
(account summary and activity in the CRM workspace, the Directory timeline host, and the
conversion mutation clicked once on the interactive summary); `CrmHrSensitiveDataFlowTests`
exercises the Directory activity tab as well. The three timeline owners read through
`CrmHrActivityHistorySession` (`FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrActivityHistorySessionTests`).

For the CRM Financials surface (`Financials/` in the rendering library, `CrmFinancialsPanel`,
`CrmFinancialsReadSession`, `CrmFinancialsPresentationMapper`, or the `/crm-hr/financials`
sandbox specimen), run:

```powershell
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmFinancials|FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrFinancials" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmFinancials|FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrFinancials" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Integration.CrmHr.CrmFinancialSnapshotQueryIntegrationTests" /m:1
```

The integration facts protect the query owner's recognition semantics, which the extraction
must not change. Browser evidence is `FullyQualifiedName~CrmHrFinancialsBrowserTests` (seeded
sparse multi-currency sales, plotted chart geometry and legend, monthly/yearly categories, a
constrained width and an account without sales).

For a change anywhere in the CRM / HR module, its rendering library, its contracts or its sandbox,
the maintained [completion record](architecture/crm-hr-ui-completion.md) maps each workspace to its
lanes. The whole-module lanes are the Unit and Components topics (the Components topic includes the
host invariant facts `CrmHrSameTargetRerenderTests`, `CrmHrPostDispatchEditTests`,
`CrmHrCommittedReadBackTests`, `CrmHrAssignmentsMutationTests`, `CrmHrOpportunityConversionHostTests`,
`CrmHrDirectoryRowActionTests`, `CrmHrEditorFieldCoverageTests`, the footer-save and sandbox facts,
and the boundary guards) and the browser journeys:

```powershell
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CrmHr|FullyQualifiedName~CrmAgent|FullyQualifiedName~HrAgent|FullyQualifiedName~CrmPlanning|FullyQualifiedName~ProjectAssignmentGanttProjectionAdapterTests|FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.|FullyQualifiedName~OpportunityBoardTests|FullyQualifiedName~AssignmentEditorAdmissionTests" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Playwright.slnx --configuration Release --no-build --no-restore --filter "(FullyQualifiedName~CrmHr|FullyQualifiedName~ProjectStructureTaskAssigneeJourneyTests)&Category!=Quarantined&Category!=LiveAgent" /m:1
```

The six CRM / HR evidence-capture browser scripts that predated these journeys are gone. They were
quarantined, wrote their screenshots to an absolute Windows path, asserted almost nothing, and no
longer ran against the current UI at all. The journeys above cover their flows; the secondary
editor fields they used to type into are covered by `CrmHrEditorFieldCoverageTests` and by their
owners' Unit and Integration tests. The
[completion record](architecture/crm-hr-ui-completion.md#retired-browser-scripts) lists what is
still not driven through a browser.

The opt-in live model smoke (`Category=LiveAgent`: `CrmHrLiveAgentToolUiSmokeTests` in the
Playwright project, `CrmHrLiveAgentToolSmokeIntegrationTests` in the Integration project) returns
without effect unless both `CANDOITALL_RUN_LIVE_AGENT_VALIDATION=true` and
`CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true` are set for the test process; it uses the seeded provider
profile and its configured credential, synthetic records and a bound of ten model requests per
execution. `CANDOITALL_LIVE_AGENT_UI_REHEARSAL=true` runs the UI smoke up to its first message
without a model request.

**A runner result of passed is not live evidence in this lane.** The test runner in use does not
honour xUnit's dynamic skip for these projects, so a closed gate and a rehearsal are both reported
as passing tests. Read `output/live-agent-smoke/<timestamp>/evidence.json` instead: its `execution`
field is `not-run` when the gate was closed, `rehearsal` when the journey stopped before its first
Send, and `live` only when a model was actually reached, and `modelRequests.used` is the number of
requests the provider journal counted. A report that claims live proof cites that manifest, the
provider and model it names, and the persisted run and owner state it recorded.

## Broad Stable Gate

Run this gate only for CI, release or merge closure, a frozen checkpoint, an explicit
operator or reviewer request, or a named invalidation trigger in the work plan. Typical
invalidation triggers are cross-cutting composition/DI changes, root solution or
`Directory.Build.*` changes, and shared persistence, migration, or test-infrastructure
changes. A trigger must be named; "run everything to be safe" is not one.

Run from the repository root:

```powershell
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet restore ./tests/Solutions/CanDoItAll.Tests.Stable.slnx
dotnet build ./tests/Solutions/CanDoItAll.Tests.Stable.slnx --configuration Release --no-restore /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Stable.slnx --configuration Release --no-build --no-restore --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true" /m:1
```

`/m:1` avoids `bin` and `obj` contention when local MCP or watch processes are active. A developer with an isolated workspace may increase parallelism, but the result must still come from the same configuration and filter.

Those commands use sibling source projects. CI resolves the Components branch matching the
application branch (`development` or `main`); pull requests use their target branch, while
pushes and manual runs use the selected branch. A missing matching branch fails checkout.
The dependency job resolves that branch once to a commit, and every platform and container
job checks out that exact commit. FileTools retains its explicit workflow commit pin.
Docker receives the same repositories as named build contexts. Keep source roots and
commits identical for the whole gate; do not substitute an unpublished package graph for
any command. A PR from `development` to `main` therefore validates against Components
`main`; required Components changes must reach that branch before the application merges.

The gate is long. Measure it when you run it and compare the number with the `timeout-minutes` of
the stable job in the CI workflow before assuming the two agree: this workstation has recorded runs
close to, and above, that budget, and a runner that is slower than the budget fails the job without
a test failing. Neither reducing the filter nor raising the timeout without a measurement is an
acceptable answer to that.

The CI workflow gives each platform its own budget in the stable job's matrix. They come from the
first CI run of this gate on all three platforms (commit `d0f3c41a4`, 2026-09-19), whose logs time
every phase:

| Platform | Checkout, setup and build | Components | Integration | End of the test step |
|---|---|---|---|---|
| Linux (`ubuntu-24.04`) | 6.7 min | 16.0 min | 90.0 min | 114 min |
| macOS (`macos-15`) | 7.6 min | 12.1 min | 98.9 min | 120 min |
| Windows (`windows-latest`) | 16.6 min | 32.0 min | still running after 131.4 min | cancelled at 180 min |

Windows was cancelled by the former 180-minute budget without a failing test. Projecting its
Integration run from the Components-to-Integration ratio of Linux (5.6) and of this workstation
(4.1, already exceeded) puts it between 131 and 181 minutes, so the whole Windows job, with the
Memory and Unit assemblies and the portability gates that follow the test step, needs about 205 to
265 minutes; its budget is 300. Linux and macOS keep 180. The portability gates after the test step
did not run in that measurement on any platform.

The later [run 35577411480](https://github.com/fyziktom/CanDoItAll/actions/runs/35577411480)
(2026-09-21, application `8744d2dd1`) completed Windows in 245.5 minutes: 27.1 minutes
for Components, 188.9 for Integration, then all remaining stable and portability gates.
This replaces the Windows projection and supports the existing 300-minute budget.
Linux passed Components and all 3,120 integration cases, then failed one tuning-request
unit test at its five-second polling deadline. That test now observes status events with
a bounded wait, reports failed terminal states, and avoids asserting a transient queued
snapshot after background execution has already been scheduled.

macOS did start and build. Its Components tests passed, but the integration assembly
reported database and HTTP timeouts and was still running when the 180-minute job budget
expired. The old job-level `FILE_COPY` setting forced checkpoints for every new database.
The broad stable gate now uses `WAL_LOG` on all platforms; the focused PostgreSQL migration
and restart step retains the matrix strategy, including `FILE_COPY` on macOS. This keeps
both strategies covered without adding that checkpoint cost to thousands of tests.
[PostgreSQL's CREATE DATABASE documentation](https://www.postgresql.org/docs/16/sql-createdatabase.html)
describes this tradeoff. The logs do not prove that checkpoint pressure caused every
macOS timeout; a new macOS run is required to confirm the resulting duration and failures.
The macOS budget and test exclusions remain unchanged.

The filter intentionally excludes:

- browser automation
- process-spawning and live-host integration
- long-running suites
- actual-host runtime-portability cases
- tests requiring host Docker
- tests with an explicit `Quarantined` trait

Quarantine is not a passing result. Remove a quarantine only with focused replacement evidence and a passing owning gate.

## Documentation

```powershell
./tools/Validation/Test-Documentation.ps1
```

Run this after changing maintained Markdown, repository metadata, public paths, or source-truth claims represented by the validator.

## Portability Static Gate

This gate is mandatory before closing a CI/test repair, any change under `.github`,
`src`, `Templates`, or `tools`, or any protected root build/configuration file. Include
supporting production edits, shared test fixtures, and changes brought in by a merge
when assessing the final change. Running only affected tests or leaving the full .NET
suite to CI does not waive this static gate.

The baseline fingerprints reviewed portability-sensitive source. A legitimate source
edit can therefore produce both an `ADDED` finding and a `STALE` allowance even when the
resulting code remains portable, including dependency version updates, constructor
signature changes, or additional shell steps.

Run the same tooling used by CI from the repository root:

```powershell
$portabilityScan = Join-Path ([System.IO.Path]::GetTempPath()) (
    "candoitall-portability-{0}.json" -f [guid]::NewGuid().ToString("N")
)

python ./tools/Validation/Portability/test_enforce_portability_baseline.py
python ./tools/Validation/Portability/test_scan_artifacts_for_secrets.py
python ./tools/Validation/Portability/scan_portability.py --repo-root . --output $portabilityScan --tracked-only
python ./tools/Validation/Portability/enforce_portability_baseline.py --scan $portabilityScan --baseline ./tools/Validation/Portability/portability-risk-baseline.json
```

The scan must cover the complete proposed source and must not be truncated. Check
`git status --short`: if new protected files are untracked, repeat the scan without
`--tracked-only` to include them before reviewing or refreshing the baseline. Do not use
a scan limited to changed files or an old CI artifact as the refresh input.

If enforcement reports a delta, inspect every finding. Repair new platform assumptions,
hard-coded machine paths, shell coupling, or other genuine portability defects, then
regenerate the scan after any source edit. When the remaining findings are intentional,
including reviewed fingerprint/count changes or removed findings, refresh the baseline
in the same change only after that review:

```powershell
python ./tools/Validation/Portability/enforce_portability_baseline.py --scan $portabilityScan --baseline ./tools/Validation/Portability/portability-risk-baseline.json --write-baseline
git diff -- ./tools/Validation/Portability/portability-risk-baseline.json
python ./tools/Validation/Portability/enforce_portability_baseline.py --scan $portabilityScan --baseline ./tools/Validation/Portability/portability-risk-baseline.json
```

Do not use `--write-baseline` to conceal an unexplained result, weaken scanner patterns,
or defer the update to a later change. `ADDED` and `STALE` findings both block closure
until the code and reviewed baseline agree and the final no-write enforcement passes.

## Documentation evidence validation

`./tools/Validation/Test-Documentation.ps1` rejects tracked runtime logs. It carries one
format rule for sealed evidence: a durable `.log` inside a tracked working bundle is accepted only
when the owning tracked `MANIFEST.sha256` contains exactly one matching path and its hash
matches the current file. Untracked manifests, modified logs and unsealed logs do not
qualify; `.pid` and `.pyc` files remain forbidden. The rule is about the format, not about
any particular directory: when no such path is tracked it simply never applies, and its own
tests build a disposable fixture instead of reading a real one. New task-specific evidence
limits still apply.

Run `./tools/Validation/Test-DocumentationEvidence.ps1` to check the acceptance and
rejection cases, then run the canonical documentation validator. Both commands only
validate; the evidence tests create and remove their own temporary fixture.

## Focused HTTP Integration

For CRM/HR API changes, build the affected production project and run the real HTTP-host
slice. Record and confirm its expected discovery count first:

```powershell
dotnet build .\src\Modules\CanDoItAll.Modules.CrmHr\CanDoItAll.Modules.CrmHr.csproj --configuration Release /m:1
dotnet test .\tests\Solutions\CanDoItAll.Tests.Integration.slnx --configuration Release --list-tests --filter "FullyQualifiedName~CrmHrApiIntegrationTests" /m:1
dotnet test .\tests\Solutions\CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CrmHrApiIntegrationTests" /m:1
```

This proof must create and read linked records through `/api/crm-hr`; direct service or database setup does not validate the HTTP boundary.

Use the same pattern for other API families: choose the narrowest real-host test slice.
Run the stable aggregate afterward only when one of its explicit triggers applies.

For LLM Chats, the focused real-host slice keeps Web, hosted dispatch, application behavior, provider
resolution, EF stores, SSE transport, and PostgreSQL real while replacing only the live external
provider boundary:

```powershell
dotnet test .\tests\Solutions\CanDoItAll.Tests.Integration.slnx --configuration Release --list-tests --filter "FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests"
dotnet test .\tests\Solutions\CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LlmChatsApiPostgreSqlIntegrationTests"
```

Use the focused migration and `LlmChatsDatabaseTransferIntegrationTests` cases when changing schema or
transfer behavior. Use a `FullyQualifiedName~LlmChat` filter for the narrow owning Unit or Integration
project while iterating; do not run an unfiltered project merely to validate one LLM Chat change. The
stable aggregate is additional proof only when a broad-gate trigger applies.

The LLM Chat event-stream slice verifies `202 Accepted` before slow-provider completion, durable lease
dispatch, replay, `Last-Event-ID`/`after`, gap signaling, heartbeats, terminal closure, disconnect
independence, explicit cancellation, server-owned origin, exact authorization scopes, and redaction.
Do not replace it with an in-memory endpoint test when changing HTTP, SSE, migration, or multi-host
ownership behavior.

## Browser Gate

Build the Playwright project and install Chromium once per machine:

```powershell
dotnet build .\tests\Solutions\CanDoItAll.Tests.Playwright.slnx --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Playwright\CanDoItAll.Tests.Playwright\bin\Release\net10.0\playwright.ps1 install chromium
```

Run the non-quarantined browser gate:

```powershell
dotnet test .\tests\Solutions\CanDoItAll.Tests.Playwright.slnx --configuration Release --no-build --no-restore --filter "Category!=Quarantined" /m:1
```

Playwright hosts infer the active build configuration from the test output path. Set `CANDOITALL_TEST_CONFIGURATION` only for a non-standard output layout.

## Live-Process Gates

Run the application integration slice:

```powershell
dotnet test .\tests\Solutions\CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --filter "Category=LiveProcess" /m:1
```

Run the sibling DotNetWatch integration project from this repository root:

```powershell
dotnet test ..\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj --configuration Release --filter "Category!=Quarantined" /m:1
```

The DotNetWatch assembly uses this repository for workspace settings and runtime state. Its live and long-running tests remain outside the routine gate.

## Unfiltered Suite

```powershell
dotnet test .\tests\Solutions\CanDoItAll.Tests.Stable.slnx --configuration Release --no-build --no-restore
dotnet test .\tests\Solutions\CanDoItAll.Tests.Playwright.slnx --configuration Release --no-build --no-restore
```

Do not report the full suite as green unless both exact no-filter commands pass after the
required browsers, hosts, databases, and sibling processes are available. Expected
quarantine failures and missing environment dependencies are still failures of this
gate and must be reported as such.
