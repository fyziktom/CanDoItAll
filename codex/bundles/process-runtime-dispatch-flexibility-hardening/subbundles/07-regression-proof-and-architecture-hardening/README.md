# Regression Proof And Architecture Hardening

## Status

- `Completed`

## Objective

- Close the initiative with regression proof, test decomposition audit, driver-boundary/dependency hardening, file-size guardrails, browser proof if UI changed, and note-by-note closure.

## Covered Inputs

- `R001` Preserve all functionality.
- `R002` Split integration file.
- `R011` Decompose tests along service boundaries.
- `R012` Prove enterprise-domain flexibility.
- `R013` Driver ports own completion evidence, prompt composition, and step execution dispatch behavior.
- `R014` Maintain one-way dependency direction from MAF/AgentFramework to Processes contracts.
- `R015` Keep generic runtime dispatch orchestration separate from driver-owned step execution dispatch.
- All raw notes `N001` through `N008`.

## Prerequisites

- SB02, SB03, SB04, SB05, and SB06 closure gates passed.
- Every completed critical subbundle has `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md`.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `repo://tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/App/CanDoItAll.Web/Api/ProcessesApi.cs`

## Deliverables

- Test decomposition audit proving key behavior is covered by focused tests, not only monolithic adapter tests.
- Driver-boundary audit proving prompt composition, completion evidence, and step execution dispatch are owned by driver ports or driver implementations.
- Dependency-direction audit proving `src/Processes/*` does not reference MAF, AgentFramework implementation projects, or `CanDoItAll.Modules.AgentFramework`.
- Full affected test run transcripts for unit and integration test slices.
- Build transcript for changed projects or solution.
- File-size/coupling audit for the original hotspot and new extracted files.
- Anti-stub audit across production runtime/driver/dispatcher code.
- Browser/Playwright proof if UI/API/dashboard behavior changed.
- Final raw-note closure table with `Solved`, `Partially solved`, or `Not solved` and proof citations.
- Final completed-stage validator run.

## Dependency Impact

- This is the final closure phase. Weak proof here reopens the subbundle that introduced the gap.

## Validation Depth

- Process-critical closure.
- Requires Semantic Adequacy Gate proof, artifact-backed proof manifest, and red-team/fake-proof resistance audit.

## Implementation Steps

1. Re-read all critical subbundle proof manifests and semantic invariant contracts.
2. Run or verify targeted unit test commands for extracted adapter, prompt, evidence, domain launch, driver-dispatched step execution, dispatch, recovery, and process runtime behavior.
3. Run or verify targeted integration tests for AgentFramework execution evidence and project-structure process flows.
4. Run `dotnet build CanDoItAll.slnx` or an explicitly justified narrower build when the full solution is blocked.
5. Run source/project-reference scans from `inventories/02-dependency-direction-inventory.md` and record the transcript.
6. Run file-size/coupling audit to confirm `ProcessRuntimeIntegrationServices.cs` and new files meet SB01 targets.
7. Run driver-boundary source assertions proving prompt, evidence, and step execution dispatch behavior no longer live in generic runtime/application private methods.
8. Run anti-stub audit over changed production files for `TODO`, `NotImplemented`, fixture-specific branching, and template-only output.
9. If UI/API/dashboard files changed, run Playwright/browser validation and screenshot review.
10. Update `reviews/01-execution-report.md`, raw-note closure, analytics review, and final proof paths.
11. Run `validate_bundle.py --stage completed` and repair any bundle/proof failures.

## Scope Exceptions

- If a full solution build or integration test suite is blocked by an unrelated environment issue, record the exact blocker transcript and run the narrowest affected substitute. Do not mark closure passed solely from substituted proof if the blocker affects changed behavior.

## Do Not Do

- Do not close the bundle with prose-only proof.
- Do not leave completed critical subbundles without proof manifests or semantic invariant contracts.
- Do not accept tests that only assert file existence, non-empty strings, diagnostic headings, or count changes.
- Do not accept a final architecture where any `src/Processes/*` project references MAF, AgentFramework implementation projects, or `CanDoItAll.Modules.AgentFramework`.
- Do not accept prompt fragment composition, completion evidence policy, or step execution dispatch policy remaining as generic Processes private helpers.
- Do not skip browser proof if UI/API/dashboard behavior changed.

## Acceptance Checklist

- Every raw note has `Solved`, `Partially solved`, or `Not solved` status with proof.
- All critical proof manifests exist and cite existing artifacts.
- Unit, integration, build, anti-stub, and file-size/coupling proof is recorded.
- Generic enterprise process flexibility is proven with non-software scenarios.
- Driver-owned prompt/evidence/step-dispatch boundaries are proven by source assertions and tests.
- Dependency-direction scans prove MAF/AgentFramework process support is below Processes, not referenced by Processes.
- Product/software-delivery compatibility remains proven.
- Final closure validator passes or records an honest blocker.

## Proof Required

- `proof/SB07/manifest.md` with changed-file hashes, command transcripts, source assertions, anti-stub audit output, file-size/coupling audit, and red-team/verifier artifact.
- `proof/SB07/semantic-invariants.md` covering full-regression preservation, service-boundary proof, driver-owned prompt/evidence/dispatch proof, dependency-direction proof, enterprise-domain flexibility, and fake-proof resistance.
- `proof/SB07/red-team-proof-audit.md` re-reading all critical manifests and rejecting shallow proof.
- Passing test/build transcript paths or explicit blocker transcripts.
- Dependency-direction scan transcripts and driver-boundary source assertion transcript.
- Browser screenshots and Playwright transcripts if UI/API/dashboard behavior changed.

## Browser Validation Logging

- Route/window: process dashboard, process launch/API, or Workbench process start surfaces only if changed.
- Viewport: large desktop first, then narrower widths when layout changed.
- Actions: navigate, trigger relevant launch/dispatch/projection interactions when applicable, assert no console/runtime errors, capture screenshot.
- Screenshots: `bundle://proof/SB07/screenshots/process-dashboard-large.png` and additional route-specific names when required.
- Review questions: are runtime status, branch/skipped states, subprocess waits, dispatch recovery indicators, and action affordances readable without clipping or overlap?

## Progression Gate

- The bundle can close only when final validator passes, raw notes are closed with proof, driver-owned prompt/evidence/dispatch boundaries and no Processes-to-MAF dependency are proven, and any unresolved gap is represented as a blocker or follow-up subbundle rather than residual-risk prose.

## Suggested Agent Prompt

```text
Implement SB07 only after SB02-SB06 have passed. Re-read every critical proof manifest, run targeted build/unit/integration/UI proof as required, audit driver boundaries, dependency direction, file size, and coupling, run anti-stub and fake-proof resistance checks, close raw notes N001-N008 with proof, and run the completed-stage validator. Reopen the responsible subbundle if any critical proof is shallow, missing, or permits Processes-to-MAF dependencies.
```

