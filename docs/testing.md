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

Those commands use sibling source projects. CI checks out Components and FileTools at the
pinned commits declared in its workflow, and Docker receives the same repositories as
named build contexts. Keep source roots and commits identical for the whole gate; do not
substitute an unpublished package graph for any command.

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
