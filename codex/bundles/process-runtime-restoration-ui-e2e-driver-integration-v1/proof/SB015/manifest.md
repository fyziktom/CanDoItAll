# SB015 Proof Manifest

Status: Passed.

## Scope

Gate E covers the large-desktop browser proof for process UI route access, process template selection/import, launch-plan creation, ready-state execution, and durable run selection in `P05: UI process-start E2E skeleton`.

No production UI, API, template, runtime, driver, Core, scheduler, workflow, shell, Office, Graph, workspace/storage, or process mutation code was changed in SB013-SB015. The only source change for this gate is the Playwright smoke in `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`.

## Command Transcripts

- `bundle://proof/SB013/transcripts/large-screen-process-route-source-assertions.txt`
- `bundle://proof/SB014/transcripts/template-import-process-start-source-assertions.txt`
- `bundle://proof/SB015/transcripts/focused-large-screen-process-start-playwright.txt`
- `bundle://proof/SB015/transcripts/anti-stub-no-small-viewport-scan.txt`
- `bundle://proof/SB015/transcripts/large-desktop-screenshot-inventory.txt`
- `bundle://proof/SB015/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB015/transcripts/prepared-validator-after-sb015.txt`
- `bundle://proof/SB015/transcripts/changed-file-hashes.txt`

## Source Assertions

- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs` starts a real Playwright browser context through `fixture.Browser.NewContextAsync`.
- The browser proof uses `ViewportSize` `1900x1200` and the source scan rejects smaller viewport terms in the new smoke.
- The smoke navigates to `/processes`, waits for `processes-workspace-shell`, opens the template library, selects `business-plan-development`, and imports the template through the real UI.
- The smoke publishes the imported definition through the process API, creates a launch plan through the UI, advances the plan through real API lifecycle endpoints, and executes it through `processes-launch-execute-button`.
- The smoke waits for the created run through `/api/processes/runs`, selects the durable run from Activity using `processes-run-history-item-{runId}`, and verifies `processes-selected-run-summary`.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-restore --filter "FullyQualifiedName~Process_start_SB015_INV_001_large_screen_imports_template_and_executes_ready_launch_from_ui"` passed with 1 test.

The test captures large-desktop proof artifacts in `repo://output/playwright/process-start-smoke`:

- `01-template-selected-large-desktop.png`
- `02-runs-tab-before-launch-large-desktop.png`
- `02-launch-plan-created-large-desktop.png`
- `03-run-selected-large-desktop.png`

## Anti-Stub And Adversarial Proof

- The anti-stub scan confirms the smoke uses the real browser fixture, `fixture.BaseUrl`, browser clicks/fills, `HttpClient`, JSON API calls, and the real Blazor error marker check.
- The scan rejects mock, substitute, test-server, fake, stub, bundle-path, sleep, and small/medium/mobile viewport patterns in the new proof.
- The test rejects shallow UI proof by requiring imported template step content, ready launch plan state, UI execution feedback, a persisted process run from API, Activity run selection, and selected-run summary content.

## Forbidden Drift

`bundle://proof/SB015/transcripts/forbidden-drift-scan.txt` confirms:

- no production source files under `repo://src` changed in SB013-SB015;
- the only code source addition for this gate is the Playwright smoke;
- no generic runtime host, registry, selector, driver DI registration, manager command, scheduler/workflow hook, shell execution, Office/Graph call, workspace/storage write, or process mutation behavior was introduced by this gate.

## Changed-File Hashes

See `bundle://proof/SB015/transcripts/changed-file-hashes.txt`.

## Downstream Dependency Check

SB016-SB018 can rely on the existing `/processes` browser route, template import path, launch-plan UI controls, process API launch lifecycle endpoints, and durable run selection path. Runtime and driver phases remain untouched by this browser proof gate.
