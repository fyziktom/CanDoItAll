# SB10 Proof Manifest

## Implementation Scope

- Added capability-aware preflight for required runtime tools before provider/tool composition.
- Required `workspace_*` runtime tools now require an assigned `CapabilityKind.Tool` capability whose key matches the hyphenated tool capability key.
- Required `browser_*` runtime tools now require explicit browser/Playwright MCP capability evidence.
- Added capability diagnostics to `ProcessRuntimeToolPreflightResult` and preserved them in runtime issue conversion.
- Flowed template `ExecutionContract.RequiredRuntimeToolNames` into launch readiness and launch-plan runtime step state.
- Kept existing plan guard and provider-composition diagnostics ahead of capability checks so invalid scripts, wrong paths, denied scopes, invalid arguments, and missing manifests remain distinguishable.

## Raw Analysis Closure

- GPTPro's finding that a missing or denied tool capability causes rework to repeat with the same incapable agent is closed by explicit capability diagnostics and launch readiness rejection.
- GPTPro's finding that an agent can be named on a deterministic step without actually owning the required tool capability is closed by typed tool capability matching.
- The user's broader warning that similar failures can appear across process, template, and artifact flows is covered by applying the check to normalized required runtime tools, including migrated template execution contracts from SB09.

## Validation

- `proof/SB10/transcripts/01-targeted-unit-tests.txt`
  - `ProcessRuntimeToolPreflightServiceTests` and `ProcessLaunchExecutorResolverTests`.
  - Result: 36 tests passed, 0 failed.
- `proof/SB10/transcripts/02-adapter-preflight-tests.txt`
  - Adjacent runtime adapter preflight tests.
  - Result: 3 tests passed, 0 failed.
- `proof/SB10/transcripts/03-modules-processes-build.txt`
  - `dotnet build src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`.
  - Result: build passed with 0 warnings and 0 errors.
- `proof/SB10/transcripts/04-processes-application-build.txt`
  - `dotnet build src/Processes/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj`.
  - Result: build passed with 0 warnings and 0 errors.
- `proof/SB10/transcripts/05-source-assertions.txt`
  - Source assertion transcript for SB10-CAP-001 through SB10-CAP-006.
- `proof/SB10/transcripts/06-anti-stub-audit.txt`
  - Anti-stub audit for changed SB10 runtime and test files.
- CodeAnalytics snapshot: `snap-20260708203629-184e6305`.
- CodeAnalytics dependency cycle query: `cycles: []`.
- Known unrelated warning in broad test/build graph: existing NU1903 advisory for `Microsoft.OpenApi` during unit-project restore/build graph loading.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Negative proof |
| --- | --- | --- | --- |
| `ProcessRuntimeToolPreflightResult.CapabilityDiagnostics` | `ProcessRuntimeToolPreflightService.EvaluateRequiredRuntimeToolCapabilities` records typed missing capability diagnostics. | `AgentFrameworkProcessExecutionAdapter.ResultConversion.CreateRuntimeToolPreflightIssue` includes the diagnostics in missing/detail summary and evidence hash. | `EvaluateAsync_rejects_workspace_script_when_profile_can_expose_tool_but_agent_lacks_capability` rejects prose/profile-only capability and reports no generic missing-tool fallback. |
| Launch readiness required tool set | `AgentFrameworkProcessLaunchExecutorResolver.ResolveLaunchReadinessRequiredRuntimeToolNames` combines step and template execution-contract required tools. | `AgentProcessReadinessEvaluator` receives the combined required tool set before launch variable materialization. | `ResolveAsync_rejects_dotnet_setup_template_when_agent_lacks_required_tool_capability` returns no bindings and emits `agent.readiness.required-tool-capability-missing`. |
| Launch-plan required tool state | `ProcessLaunchApplicationService.ResolveLaunchPlanRequiredRuntimeToolNames` combines assignment and template execution-contract required tools. | Launch-plan runtime step state exposes the effective required runtime tools. | Source assertions prove no markdown instruction parsing is used for this runtime-tool requirement path. |

## File Hashes

- Hash ledger: `proof/SB10/changed-file-hashes.txt`.

## Completed Validator Metadata

- Semantic invariant contract: `proof/SB10/semantic-invariants.md`.
- Portable source proof: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`.
- Portable bundle proof: `bundle://proof/SB10/changed-file-hashes.txt`.
- SHA-256 changed-file hash: `6A738A99D9B70B37BA6765808FD0BF5318857BF53385340442EF88013231B89D`.
- Passing transcript: `proof/SB10/transcripts/01-targeted-unit-tests.txt`.
- Anti-stub audit transcript: `proof/SB10/transcripts/06-anti-stub-audit.txt`.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests inside the passing targeted transcript rather than preserving a historical failing transcript.


