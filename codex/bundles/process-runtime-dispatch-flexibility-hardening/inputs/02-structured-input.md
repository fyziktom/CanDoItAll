# Structured Input

## Core Objective

- Prepare an initiative bundle for architecture refactoring of process runtime and dispatch integration after commit `6775de820 phase1`.
- The refactor must preserve existing process behavior while splitting overgrown integration code into maintainable driver services, prompt strategies, evidence policies, and domain-specific launch contributors.
- The runtime must support non-software enterprise processes as a normal case, not as an afterthought behind software-delivery prompt rules.

## Success Criteria

- The prepared bundle passes `validate_bundle.py --stage prepared`.
- Every raw request item maps to normalized requirements and owning subbundles.
- Each critical subbundle requires semantic adequacy proof and artifact-backed manifests.
- Subbundle boundaries are coherent enough for an implementation agent to execute without rediscovering the architecture.

## Hard Constraints

- No production implementation during bundle preparation.
- Preserve all existing behavior and tests unless execution proves a behavior is wrong and records a replacement requirement.
- Do not remove functionality to simplify the refactor.
- Keep generic process runtime/application contracts domain-neutral.
- Move AgentFramework, project-structure, .NET, UI screenshot, and software-delivery specifics behind explicit driver or contributor boundaries.
- Use typed contracts, options, enums, and result records for new seams; do not add magic string decision paths.
- Include validation, logging, and exception behavior as part of the implementation contract.

## Allowed Side Effects

- Bundle artifacts under `codex/bundles/process-runtime-dispatch-flexibility-hardening`.
- No production code, test code, template, project, or migration edits during preparation.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`

## Input Coverage Signals

- `N001`: improve flexibility of runtime/dispatcher.
- `N002`: split `ProcessRuntimeIntegrationServices.cs` and similar runtime hotspots.
- `N003`: isolate prompt builders into drivers or strategies.
- `N004`: preserve all functionality.
- `N005`: support non-software enterprise process domains.
- `N006`: isolate domain-specific behavior into own drivers, contributors, or projects.
- `N007`: completion evidence policies, runtime process dispatching, and prompt fragment composition must be driver-owned.
- `N008`: Processes must not depend on the MAF wrapper; MAF/AgentFramework must sit below Processes by implementing process driver abstractions.

## Dependency And Sequencing Signals

- Boundary decisions must come first because every extraction depends on project/reference direction.
- Driver port design must come before adapter, prompt, evidence, and dispatch extraction because the MAF/AgentFramework implementation must plug in below Processes.
- Adapter decomposition, prompt strategy extraction, evidence policy extraction, domain launch isolation, and dispatcher cleanup can be staged after the boundary decision.
- Regression proof must happen last because it consumes the extracted service tests and validates behavior preservation.

## Validation Expectations

- Critical subbundles require shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, and artifact-backed proof manifests.
- Behavior-preserving refactors require tests before and after extraction that prove the same runtime outcomes.
- Generic process flexibility requires explicit non-software prompt/launch tests.
- Dependency-direction proof must fail if a `src/Processes/*` project references MAF, AgentFramework, `Modules.AgentFramework`, or an MAF-owned driver implementation.

## Evidence Contract

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter ...` for affected unit slices.
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ...` for affected integration paths.
- `dotnet build CanDoItAll.slnx` or targeted project builds when project references change.
- `proof/SBxx/manifest.md`, `proof/SBxx/semantic-invariants.md`, transcripts, changed-file hashes, source assertions, and anti-stub audit outputs for critical subbundles.

## UI Validation Strategy

- UI proof is not expected for the extraction itself.
- If implementation changes process dashboard, process API, project-structure launch UI, or visible Workbench behavior, use a large-screen Playwright pass first, then narrower widths when layout is affected.

## Browser Validation Analytics

- `reviews/01-execution-report.md` is pre-seeded with browser analytics rows.
- Rows may remain `N/A` for backend-only subbundles.
- SB07 must fill route, viewport, actions, screenshot paths, and result if UI/API/dashboard behavior changes.

## Working Assumptions

- Exact AgentFramework driver project placement must be decided in SB01 after validating dependency direction.
- Model-specific prompt variants are not created speculatively; SB03 creates the strategy seam and preserves the current AgentFramework strategy.
- Generic dispatch orchestration may remain in Processes only for scheduling, claims, lifecycle transitions, and strategy invocation. Step execution dispatch behavior belongs to the selected driver.

## Primary Risks

- Mechanical file splitting could leave one untestable design under more filenames.
- Generic process runtime could remain biased toward app-building if prompt and launch variables are not separated.
- Dispatcher cleanup can regress claim lifetimes, branch gates, skipped-step propagation, or recovery behavior.
