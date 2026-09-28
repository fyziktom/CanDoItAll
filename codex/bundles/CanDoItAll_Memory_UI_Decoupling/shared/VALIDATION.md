# Validation · fast iteration, credible closure

The current `docs/testing.md`, repository instructions and CI are authoritative [S01–S03, S19]. Commands below are examples/procedures observed during this review, **not results from this package**. Each child chooses and records its actual affected scope and discovery counts.

## Evidence layers

| Layer | What it can establish | What it cannot replace |
|---|---|---|
| Evaluated project/package/asset graph + boundary guards | Intended dependency cut and invalid-edge detection | Runtime rendering, behavior, benchmark |
| Focused unit/session tests | State transitions, classification, mapping, stale-result suppression | Real persistence, HTTP authorization, browser geometry |
| Real renderer/component tests with explicit dependencies | Markup, parameters, events, validation and host lifetime | JS/CSS/focus behavior of the browser |
| Integration against the real owner/isolated PostgreSQL or real HTTP host, as relevant | Commit/concurrency/admission and transport contracts | Operator browser journey |
| Sandbox browser scenario | Actual extracted closure, assets, interactive preview without backend | Production composition/authority |
| Playwright on the production Web host | Shipped route, real composition, controls, overlays, console/network errors and user journey | Every backend/provider capability or all platform behavior |
| Controlled watch measurement | Observed development-loop cost for the recorded setup | Universal speedup or architectural safety |

Use fakes at the boundary that the test intentionally excludes. A component-only test need not use PostgreSQL. A test claiming persistence or owner commit semantics must not replace that owner with a fake that returns the desired receipt. Test rendered behavior through public seams instead of exposing private fields or asserting a class/file quota.

## Iteration sequence

Establish a baseline for the touched behavior and its expected discovery count. Use Code Analytics MCP when available plus ordinary references/search to identify affected tests and consumers. The fact that a test name lacks the module name does not exclude it: shared host/contract changes may affect another module.

Build each changed production project directly. Select the narrowest owning test solution; use an exact fully qualified test or a bounded topic/filter union. Run `--list-tests` whenever a filter is new or changes; verify expected **and actual** case counts, including theory data. Execute the same filter on current assemblies. An empty selection, unexpected skip, discovery mismatch or build failure is not a passing test run.

Current entry points under `tests/Solutions` are `CanDoItAll.Tests.Unit.slnx`, `.Components.slnx`, `.Integration.slnx`, `.Memory.slnx`, `.Playwright.slnx`, and `.Stable.slnx`. The product `CanDoItAll.slnx` deliberately does not include test/support projects. Stable excludes Playwright and has further explicit trait exclusions; do not label it “all tests” [S03, S06].

Example of the existing Prompt Gallery topic, **not a required topic for other modules**:

```powershell
$ui = './src/UI/CanDoItAll.Prompts.UI/CanDoItAll.Prompts.UI.csproj'
$suite = './tests/Solutions/CanDoItAll.Tests.Components.slnx'
$filter = 'FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.'
dotnet build $ui --configuration Release /m:1
dotnet test $suite --configuration Release --list-tests --filter $filter /m:1
# Compare actual discovery with the current-source expectation before executing.
dotnet test $suite --configuration Release --no-build --no-restore --filter $filter /m:1
```

The trailing dot intentionally avoids unrelated prefix matches in the corresponding unit topic. The listing/build must refresh the owning test assembly for the current source; otherwise omit `--no-build`. Build all other changed production projects as well. `/m:1` is the documented safe local default when watch/MCP processes could contend for outputs, not a blanket ban on measured parallelism.

## Essential behavioral checks for an extraction

For the selected surface, verify same-target rerender/section changes, editor/context preservation, draft reset on genuine target change, A→B→A, disposal, cancellation-ignoring late success/error/finally, independent reads, restricted actions, real form validation, duplicate submission, owner rejection/conflict, committed-but-refresh-failed and the genuine unknown path where one exists. Include post-dispatch edits or explicitly verify the disabled-editor policy.

Test the actual parent and meaningful descendants/slots; do not pass by stubbing out the backend-coupled child. Test late overlay close and effects against the real dialog/navigation composition when affected. Use correct asynchronous event completion and current bUnit helpers from the testing guide; a queued click or heading-only “ready” assertion is not proof an editor interaction occurred [S03].

Boundary guards should reject direct **and transitive** implementation/persistence edges and inappropriate public types, cover the sandbox closure, and fail on unexplained unresolved dependencies. Review allowed read-port semantics, not only naming conventions. Negative fixtures are useful; a rigid interface count is not. The current `CrmHrUiModuleBoundaryTests` is a concrete starting point, with the traversal limitations recorded in the audit [S23, S24].

## Database and process safety

Database-backed tests require an explicitly isolated **PostgreSQL 18** endpoint through `CANDOITALL_TESTS_POSTGRES_CONNECTION` at the reviewed revision. Record sanitized endpoint and `server_version_num`; do not print credentials. Do not probe the developer's ordinary database, auto-start its Compose stack or reuse application resources on port 5032. Fixtures need their documented creation/cleanup privileges. A server/version/configuration failure is a blocked lane, not a reason to substitute EF InMemory for claimed PostgreSQL proof [S03].

Provision and clean up only owned disposable resources. Use `--init` for the Linux SDK container when running process-host tests as documented. DotNetWatch integration tests have an additional sibling MCP prerequisite; that does not make MCP a runtime prerequisite for every UI sandbox. Do not stop the user's existing watch sessions or delete shared volumes to fix a test setup.

## Production browser closure

After a meaningful UI extraction/wiring change, use the built production Web host and test the selected route's real actions, not just navigation or a screenshot. Include one representative success, meaningful rejection/failure and the affected nested/target-change scenario. Inspect asset requests, browser errors and server/circuit failures; do not silence all WebSocket/circuit traffic to obtain a clean report. Controlled external-provider replacements are acceptable only when that excluded boundary is named.

For shared conversation/agent/process/project changes, select the relevant actual user journey (for example a project-bound agent chat or a simple workflow/process start and return) from the affected boundary, not a fixed program-wide requirement to launch every runtime for every module. Live-provider/LiveProcess execution remains a separate, explicitly reported lane when triggered. Preserve supported desktop geometry, focus, scroll and overlay stacking.

## Mandatory repository gates

**Portability-static** applies before closing source/configuration/tool/test repair changes in the protected scope. Focused test execution does not waive it. At the reviewed revision the procedure is:

```powershell
$scan = Join-Path ([System.IO.Path]::GetTempPath()) (
  'candoitall-portability-{0}.json' -f [guid]::NewGuid().ToString('N'))
python ./tools/Validation/Portability/test_enforce_portability_baseline.py
python ./tools/Validation/Portability/test_scan_artifacts_for_secrets.py
python ./tools/Validation/Portability/scan_portability.py --repo-root . --output $scan --tracked-only
python ./tools/Validation/Portability/enforce_portability_baseline.py --scan $scan --baseline ./tools/Validation/Portability/portability-risk-baseline.json
```

Check each process exit code; do not continue as though a failed command passed. The complete proposed source must be covered. When new protected files are untracked, repeat the scan without `--tracked-only`; do not refresh a baseline from an incomplete or changed-files-only scan. Repair genuine defects and rescan after edits. Only then refresh intentional reviewed ADDED/STALE deltas in the same change, inspect the baseline diff, and rerun final enforcement **without `--write-baseline`**. Never weaken the scanner or blanket-baseline findings to obtain green status [S01–S03].

Run `tools/Validation/Test-Documentation.ps1` after maintained Markdown, metadata, public path or source-truth changes. Follow current evidence-format rules; do not commit arbitrary runtime logs, `.pid`, secrets or generated bytecode. The archive's own validator does not replace either repository gate.

## When to widen validation

| Invalidation trigger | Additional scope to select |
|---|---|
| Shared public types, component primitives, contracts, source build props/targets | All directly affected consumers/guards; relevant sibling and integration tests; re-evaluate graphs/assets |
| Static assets, assembly/resource identity, package surface | Relevant publish/source-mode and package-mode proof; clean-checkout assets; downstream consumers |
| Web registration, endpoint mapping, route discovery, auth/control plane | Real Web/HTTP composition and affected browser journeys |
| Persistence, commit semantics, admission, schema/transfer | Owning integration, concurrency/failure, migration/restore/restart tests |
| Native paths, files/processes, platform/headless behavior | Affected host-platform/runtime-portability lanes; classify new Components/Integration tests correctly |
| CI/release/merge/frozen integration checkpoint or another current documented stable trigger | Current stable/platform closure and any separately required browser/live/container gates |

Do not run the entire suite just because a small phase ended. Do not remove an existing required gate because a narrower test passed. Current CI separates stable Unit/Memory from sharded Components/Integration with split/full platform rules; historical all-platform timing paragraphs are not current launch instructions [S03, S19]. Record broad-gate applicability with a reason.

## Receipt

Use [the evidence template](templates/evidence.md) for exact source/configuration, commands, discovered counts, outcome and evidence paths. State `passed`, `failed`, `blocked`, `not run`, or `not applicable` separately. Historical passed logs, skipped tests, TODOs and unavailable runners are never “green by inheritance.” A partial implementation is a valid checkpoint, not full validation closure.
