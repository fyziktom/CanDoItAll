# CodeAnalytics-first validation plan

## 1. Discover before editing; query the real diff after editing

Read the current shared skill `CanDoItAll.SharedInfo/codex/skills/candoitall-codeanalytics-mcp/SKILL.md` [R23]. Discover the installed `candoitall_codeanalytics` tools and their live schema. Do not invent wrapper suffixes or assume this package executed the MCP.

For the initial review use a scoped `code_analytics_snapshot_build`, verify `code_analytics_dashboard_get` health, then inspect inventory/dependencies and exact symbol definitions/references. Start with MAF, its workflow adapter, Agents hosting bridge, Processes, and their test owners. Read exact files before edits. Use supporting direct/reverse test-project references as well as production references; the production solution alone omits all tests.

Once a real diff exists, call **`code_analytics_impacted_tests_get`**. This is a live workspace query and does **not** require a snapshot. Its currently documented fields are:

- `repositoryRootPath`, `testWorkspaces`, `changes`, `contextOnlyPaths`;
- optional `maxVisitedMembers`, `maxSelectors`, `maxReasonPaths`;
- each change has `path`, optional one-based inclusive `lineRanges` with `startLine`/`endLine`, and `behaviorIntent`.

Intents: `Unknown`, `BehaviorChange`, `ContractOrShapeChange`, `BehaviorPreservingImplementation`. Begin conservatively. Dependency, API, serialization, public contract and approval-semantic changes are not “behavior preserving” simply because method signatures did not change.

Use the actual diff from the recorded starting HEAD, including working/staged edits and new files. Deleted/renamed files and zero-line deletions require the tool's supported conservative representation; do not invent invalid line 0 ranges. Only modified files are impact seeds. Read-only context belongs in `contextOnlyPaths`. The supplied JSON scaffold is deliberately empty until populated from the real workspace.

Before the first diff, use symbol references and test inventory to identify baseline/owner repros; do not fabricate changes just to call the impact tool. After adding/changing a regression test, query its actual diff and refresh selection as implementation expands.

## 2. Healthy and honest impact results

Supply all affected runnable test workspaces: Unit, Components, Integration and both Memory projects as needed. Include Playwright for planning even though execution is deferred. `Stable.slnx` aggregates non-browser suites, but does not include browser proof. Record overlap rather than double-count duplicate selections.

Verify each workspace loaded, source-test discovery is non-zero where expected, and changed ranges resolve to the right symbols/shapes. Inspect confidence, unresolved edges, fallback scope/reason, conditional scopes and promotion triggers. DI, reflection, dynamic/context tools, generated code, Razor, package properties and native serialization can require conservative broadening. Static reachability is not exhaustive runtime proof.

`AllSuppliedSuites` is a real execution requirement even if its selector array is empty. Do not interpret that as no tests. If the dependency update broadens to all suites, retain that pending obligation for the final gate; during editing run the relevant owner regressions and targeted consumers without claiming they satisfy the broad result. An unhealthy/incomplete snapshot or impact workspace blocks a narrow-coverage claim; repair the tooling/workspace and broaden safely. Do not silently substitute grep as if CodeAnalytics had succeeded. If the MCP is unavailable, first diagnose its connection/configuration and retry through the supported setup. Read-only source review and independent preparation may continue, but mark the mandatory MCP-first/impact-selection gate Blocked; do not claim a narrow test selection is complete. Avoid an open-ended reconnect loop or a new analytics implementation as a workaround.

Promote conditional scopes when a required test fails, the diff expands, a reported containment assumption breaks, or dynamic/DI/persistence/public behavior becomes uncertain. Rerun the analysis on the final diff.

## 3. Iterative test execution

CodeAnalytics is read-only analysis, not the test runner. Use current repository-supported `dotnet build/test` or equivalent verified CI execution. Translate result scope correctly: exact `FullyQualifiedName`, fully qualified Class/Namespace prefixes, or unfiltered Project/Workspace. Record list-tests output for each new/changed filter, expected cases (including theories), observed counts and results. A zero or unexpected discovery count invalidates proof.

Build the changed production projects and required test projects with the coherent dependency graph before using `--no-build --no-restore`. Prefer Release and `/m:1` with the repository's sibling source properties. Do not reuse binaries from another revision or an earlier package restore. Run F01–F03 regressions and semantic M02/M03/M04/process owner slices first. Run bUnit/component tests as standard automated tests before browser execution.

The file [reference/test_map.csv](reference/test_map.csv) maps requirements to known test locators and new scenario specifications. It is **not** an MCP-generated selector list and does not claim an existing test already covers a new requirement.

## 4. Final platform checkpoint

This task explicitly requests the long full suite, and cross-cutting package properties/serialization are named broad-gate triggers. Defer these expensive runs until the focused loop is stable, then validate **the same final source and resolved dependency/sibling revisions** on Windows and actual Linux. A Windows host simulating a Linux enum is not Linux proof. A Linux VM/container is acceptable when it runs the real Linux SDK/runtime with the required infrastructure; record architecture and container provenance. Preserve current macOS CI coverage, but do not substitute it for either requested platform.

Prerequisites from R02/R03:

- SDK from current `global.json`; production and test solutions restored and built;
- matching Components branch resolved once and its exact revision reused; FileTools at the current CI pin; no untracked source dependency drift;
- isolated **PostgreSQL 18**, sanitized endpoint, `show server_version_num` in [180000,190000), disposable DB leases, required privileges and tested cleanup;
- no auto-probing/starting development DBs or reusing retained app state/port 5032;
- Linux process-spawning containers use a real init/reaper (`--init`) as required by the test guide;
- required browsers, optional host dependencies, and sibling `CanDoItAll.Mcp` process tests prepared in their proper lanes; no missing prerequisite passed off as green.

### Standard non-browser gate — before any UI execution

Run the documented **unfiltered Stable solution** on Windows and Linux, plus required non-browser portability, migration/restart, logical-restore and live-process/sibling lanes not actually included in that solution. Use current CI/tests inventory to avoid omitting a separate gate. The repository's filtered CI Stable run is useful but not a replacement for the requested unfiltered run.

Illustrative commands, from repository root after a current build, with result directories chosen outside production state:

```powershell
dotnet test ./tests/Solutions/CanDoItAll.Tests.Stable.slnx --configuration Release --no-build --no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --logger "trx;LogFilePrefix=full-stable" --results-directory <platform-results>
```

Replace `<platform-results>` with an actual path before running. Do not add a category filter to that command and still label it “full”. If a separate lane is already included in the unfiltered run, prove its discovered cases rather than unnecessarily rerunning identical work.

Use the current test guide for `Category=LiveProcess`, actual-host portability and PostgreSQL protocol restore. The sibling DotNetWatch integration project is currently `../CanDoItAll.Mcp/tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj`; its prerequisites and revision must be recorded. Changing only an OS profile is not proof of actual-host subprocess behavior.

### Browser gate — only after required standard proof is green

Build `tests/Solutions/CanDoItAll.Tests.Playwright.slnx`; install Chromium using its generated `playwright.ps1`. On Windows use the documented PowerShell invocation; on Linux use installed `pwsh` and required browser dependencies. Do not assume Windows PowerShell exists on Linux.

Run targeted browser journeys and then the final **unfiltered Playwright solution** on Windows and Linux:

```powershell
dotnet test ./tests/Solutions/CanDoItAll.Tests.Playwright.slnx --configuration Release --no-build --no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --logger "trx;LogFilePrefix=full-playwright" --results-directory <platform-results>
```

The separate `Category!=Quarantined` browser gate can aid triage, but it is not the documented full gate. Both unfiltered commands must pass on each requested platform to claim full-suite success. Expected quarantine failures and missing infrastructure remain failed/blocked full-suite proof. Do not delete or reclassify tests to avoid them. If truly unrelated failures remain, provide a baseline comparison and exact exception request; absent a user waiver, acceptance remains incomplete.

Live-provider journeys are an additional acceptance layer after standard green, not implied by a browser suite running with mocks. See document 07.

## 5. Static, documentation and artifact checks

Before closure execute the current documented portability gate across the complete proposed source, including protected untracked additions. Review both ADDED and STALE findings. Refresh a portability baseline only after inspecting legitimate changes, and finish with enforcement **without `--write-baseline`**. Do not weaken rules.

Current tooling paths [R02]:

```text
tools/Validation/Portability/test_enforce_portability_baseline.py
tools/Validation/Portability/test_scan_artifacts_for_secrets.py
tools/Validation/Portability/scan_portability.py
tools/Validation/Portability/enforce_portability_baseline.py
tools/Validation/Portability/portability-risk-baseline.json
tools/Validation/Test-Documentation.ps1
```

Run `Test-DocumentationEvidence.ps1` when changing evidence-format handling. Keep raw logs/traces out of maintained source unless explicitly retained and sealed under current documentation rules. Scan deliverable evidence for secrets; redact provider credentials, tokens, authorization headers, customer content and machine-specific sensitive paths.

## 6. Results and invalidation

For every run record platform, source/dependency revisions, build provenance, exact command/filter, discovered/passed/failed/skipped counts, duration and artifact paths. UI and live journeys also need process/execution IDs, screenshots/trace references and authoritative artifact/state readback. Declare blocked dependencies accurately; no “passed with missing tests”.

After a late backend/persistence/contract/dependency change, refresh impact analysis, rerun affected standard tests before UI, and rerun invalidated final platform/full-suite gates at the final revision. Do not issue a finished report that combines old Windows results with newer Linux code. Measure long-gate duration; do not repeatedly terminate a healthy suite or silently shrink coverage because it is slow. Distinguish a measured infrastructure timeout or exhausted resource limit from a test failure, preserve partial logs/counts, correct the runner prerequisite, and rerun the invalidated gate with a justified budget. A rerun after interruption never turns the interrupted attempt into a pass. Changes confined to the final report or unrelated prose need not rerun hours of runtime tests when their irrelevance is demonstrated; record the tested commit plus an exact reviewed documentation-only diff. Production, package, fixture, generated asset, configuration or behavior changes do not qualify for that exception.
