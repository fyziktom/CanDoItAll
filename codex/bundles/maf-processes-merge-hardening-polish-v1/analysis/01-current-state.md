# Current State

## Branch shape

- `maf-processes-refactor` is ahead of `development` and not behind based on the compare inspected during bundle preparation.
- The branch has already removed many old `codex/bundles/*` artifacts, but at least one transient ZIP export appears in the compare: `codex/bundle-exports/process-runtime-live-openai-verification-host-alpha-v1.zip`.
- Root `01-execution-report.md` is present and contains a reference to `codex/bundles/maf-processes-provider-hardening-followup-v1` plus a list of changed files. That report is useful as temporary Codex proof but should not be a stable repo artifact.

## MAF decoupling state

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` does not show a project reference to `CanDoItAll.Modules.Processes` in the inspected branch.
- Existing unit test `Maf_runtime_has_no_compile_time_processes_module_dependency` already scans the MAF project and source for `CanDoItAll.Modules.Processes`, `ProcessToolBuilder`, `CreateProcessToolBuilder`, and `MafAgentRuntime.ProcessTools`.
- This boundary should be kept and expanded only if new process abstractions risk leaking back into MAF.

## Process Core / driver shape

- `CanDoItAll.slnx` includes separate projects for:
  - `CanDoItAll.Processes.Core`
  - `CanDoItAll.Processes.Contracts`
  - `CanDoItAll.Processes.Drivers.Abstractions`
  - `CanDoItAll.Processes.Drivers.ArtifactEvidence`
  - `CanDoItAll.Processes.Drivers.BusinessAnalysis`
  - `CanDoItAll.Processes.Drivers.ObservationAggregation`
  - `CanDoItAll.Processes.Drivers.OfficeEvidence`
  - `CanDoItAll.Processes.Drivers.RuntimeEvidence`
  - `CanDoItAll.Processes.Drivers.TranscriptVerification`
  - `CanDoItAll.Processes.Drivers.VerificationGateway`
- `CanDoItAll.Processes.Core.csproj` references `CanDoItAll.Processes.Contracts` only in the inspected source.
- `CanDoItAll.Modules.Processes.csproj` references all current driver packages and Process Core/Contracts.
- The gateway currently exposes explicit typed methods: `VerifyTranscript`, `VerifyRuntimeEvidence`, `VerifyArtifactEvidence`, `VerifyOfficeEvidence`, `VerifyBusinessAnalysis`, `AggregateObservations`, and `VerifyBatch`.
- The gateway lane descriptors include transcript, runtime evidence, artifact evidence, Office evidence, and business-analysis evidence.

## Bundle / subbundle naming leak state

Observed concrete leaks:

- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs` contains test names with `SB018_INV_001`, `SB018_INV_002`, `SB012_INV_001`, `SB018_INV_003`, `SB033_INV_001`, and `SB024_INV_001`.
- These names encode temporary subbundle/invariant tracking and should be renamed to semantic test names before merge.
- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs` skips `codex/bundles` paths. This is acceptable only for local ignored transient files, but it must not be the only guard against tracked bundle artifacts.
- `.gitignore` currently ignores only `codex/bundles/input/` and one historical bundle validation log, not the broader transient surfaces.

## Domain-specific logic still in generic process dispatcher

Observed concrete domain logic inside `src/CanDoItAll.Modules.Processes/Automation/Dispatch`:

- `ProcessImplementationStackRules.cs` contains rules for runnable applications, `.NET`, `ASP.NET`, `dotnet`, `C#`, `Blazor`, `Razor`, `.csproj`, `.sln`, `.slnx`, NuGet, JavaScript, TypeScript, Node/npm/pnpm/yarn/vite/react/vue/svelte, and implementation test phrasing.
- `ProcessRunAutomationDispatchService.ImplementationProof.cs` contains runnable .NET host proof checks, host project path resolution, concrete product mutation/read validation, and tool receipt timeline logic.
- `ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs` provides empty domain hooks, while the actual software-delivery proof policy remains in generic dispatcher partials.
- This means domain drivers cover transcript/runtime/artifact/Office/business evidence, but software-delivery proof ownership is still mixed into the generic process dispatcher runtime.

## Working behavior to preserve

- The branch has reportedly run the multi-team app delivery process successfully and produced a simple Tetris game using project-structure inputs.
- The hardening plan must preserve that behavior. Treat the successful live run as a regression target, not as permission for a broad rewrite.
