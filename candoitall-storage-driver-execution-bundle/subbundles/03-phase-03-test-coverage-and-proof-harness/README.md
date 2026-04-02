# Phase 03 Test Coverage And Proof Harness

## Status

- `Completed`

## Objective

Extend automated and manual proof so storage behavior and browser-visible adoption can be verified honestly.

## Covered Inputs

- N007
- N010
- N011
- N012
- N013
- N014
- RQ-013
- RQ-014
- RQ-015

## Prerequisites

- `subbundles/01-phase-01-models-interfaces-and-persistence-contracts` completed.
- `subbundles/02-phase-02-provider-services-routing-and-batch-pipeline` completed or stable enough for tests to target real runtime contracts.

## Exact Source References

- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/LocalFileStorageTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkspacePathResolverGuardTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProfileHarnessIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/shared-prompts/qa-prompt.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/03-ui-proof-surfaces.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/plan/03-command-sequence.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/shared-prompts/qa-prompt.md

## Deliverables

- Expanded unit tests for contracts, routing, and capability gating.
- Expanded integration tests for unified access, IPFS, batch transfer, and honest FTP proof paths.
- Expanded Playwright automation and screenshot artifacts for storage settings/workbench/factory surfaces.
- Manual Playwright MCP proof contract embedded into the execution report and QA prompt.
- Nested workstream notes under `workstreams/` for automated and manual proof slices.
- Nested workstream files listed below:
- `P3-WS01` - Unit and contract tests (`workstreams/01-p3-ws01-unit-and-contract-tests.md`)
- `P3-WS02` - Integration tests and harness expansion (`workstreams/02-p3-ws02-integration-tests-and-harness-expansion.md`)
- `P3-WS03` - Playwright automation and manual Playwright MCP proof contract (`workstreams/03-p3-ws03-playwright-automation-and-manual-playwright-mcp-proof-contract.md`)

## Dependency Impact

- Phase 04 cannot claim closure without this proof harness, because the request explicitly requires real Playwright MCP validation.
- Weak provider tests here would let runtime bugs hide behind good-looking UI screenshots.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add or update unit tests for contracts, routing, compatibility, and capability gates.
2. Add or update integration tests for unified access and provider behavior.
3. Add or update Playwright tests and screenshot capture.
4. Wire the manual MCP proof expectations into the execution report and QA review flow.

## Scope Exceptions

- If a provider remains blocked from real integration proof, keep that status explicit and keep closure criteria honest.

## Do Not Do

- Do not treat automated Playwright tests as a substitute for manual MCP screenshot review.
- Do not leave screenshot paths or browser findings blank in the execution report.
- Do not mark provider support complete when only unit tests exist.

## Acceptance Checklist

- Automated tests exist at unit, integration, and Playwright layers for the changed storage surfaces.
- Manual MCP screenshot review is required by the phase notes and QA prompt.
- Blocked proof states remain visible.

## Proof Required

- All targeted `dotnet test` commands from `plan/03-command-sequence.md`.
- Screenshot artifacts from Playwright automation where applicable.
- Execution-report rows ready to capture manual MCP evidence.

## Browser Validation Logging

- Routes: settings storage tab, workbench upload/recommendation/preview surfaces, Prompt Factory attachment flow.
- Viewports: `1900x1200` and `1366x900` for layout-affected surfaces.
- Required artifacts: screenshot paths under `artifacts/screenshots/storage-driver/...` and execution-report analytics rows.

## Progression Gate

- Do not allow Phase 04 to close until both automated browser proof and manual MCP proof rules are in place.
- If screenshot review questions cannot be answered, reopen this phase or keep it blocked.

## Suggested Agent Prompt

```text
Implement Phase 03 only.

Add the required unit, integration, Playwright, and manual MCP proof scaffolding.
Do not convert blocked proof into green status.
Ensure the execution report can record real screenshot findings.

Read this phase README, the nested workstream notes, the workbook inventories, and the execution checklist before changing code.
Update reviews/01-execution-report.md as you go.
Do not skip Playwright MCP proof when a browser-visible surface is touched.
```

