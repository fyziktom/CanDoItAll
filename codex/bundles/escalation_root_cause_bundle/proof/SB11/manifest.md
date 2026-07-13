# SB11 Proof Manifest

## Implementation Scope

- Added `DotNetSolutionSetupRuntimeExecutor` as a runtime-owned executor for guarded deterministic .NET setup plans.
- The executor first evaluates `DotNetSolutionSetupToolPlanGuard` and does not mutate workspace state when the typed plan is absent or invalid.
- Clean `create-dotnet-project` setup scaffolds missing solution and app project targets with governed `workspace_dotnet_new`, writes/stats the deterministic helper script, runs `workspace_pwsh_run_script`, and verifies file-content readback.
- Existing solution/app targets emit runtime-owned idempotent skip receipts instead of destructive regeneration, then run the helper/readback repair path.
- `add-test-project` uses the guarded `DotNetAddTestProject*` helper plan, produces a current-run `workspace_pwsh_run_script` receipt, and verifies solution membership plus `ProjectReference` readback.
- Adapter integration consumes a successful runtime-owned result before agent execution and still runs managed artifact materialization, grounding, completion gates, acceptance, and result conversion.
- Runtime-owned setup failures return explicit structured diagnostics through the adapter failure path instead of falling back to agent prose.

## Raw Analysis Closure

- GPTPro's finding that deterministic scaffold/wire/readback work was only prompt-owned is closed for the .NET solution setup contract by a runtime-owned executor.
- GPTPro's blocked 5032 example is directly covered by the existing-project empty-solution repair test: the project is not recreated, the helper script runs, and solution membership is read back before `Completed`.
- The broader template risk is preserved through the SB07 guard and SB09 typed contracts: runtime-owned execution activates only when deterministic launch variables and completion gates are typed enough to execute and verify.

## Validation

- `proof/SB11/transcripts/01-targeted-runtime-owned-dotnet-tests.txt`
  - `DotNetSolutionSetupRuntimeExecutorTests`, adapter runtime-owned execution test, and SB07 guard regression test.
  - Result: 7 tests passed, 0 failed.
- `proof/SB11/transcripts/02-modules-processes-build.txt`
  - `dotnet build src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`.
  - Result: build passed with 0 warnings and 0 errors.
- `proof/SB11/transcripts/03-source-assertions.txt`
  - Source assertion transcript for SB11-RUNTIME-001 through SB11-RUNTIME-007.
- `proof/SB11/transcripts/04-anti-stub-audit.txt`
  - Anti-stub audit for changed SB11 runtime and test files.
- `proof/SB11/transcripts/05-codeanalytics.txt`
  - Scoped CodeAnalytics summary.
- CodeAnalytics snapshot: `snap-20260708212205-c7d874cd`.
- CodeAnalytics dependency cycle query: `cycles: []`.
- Known unrelated warning in broad test/build graph: existing NU1903 advisory for `Microsoft.OpenApi` during unit-project restore/build graph loading.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Negative proof |
| --- | --- | --- | --- |
| `DotNetSolutionSetupRuntimeExecutionResult` | `DotNetSolutionSetupRuntimeExecutor.TryExecuteAsync` returns structured success/failure with receipts, summary, and evidence. | `AgentFrameworkProcessExecutionAdapter.TryExecuteRuntimeOwnedDotNetSetupAsync` materializes, gates, accepts, hashes, and converts the runtime-owned outcome. | `TryExecuteAsync_returns_failure_when_helper_script_fails` proves helper failure returns structured failure instead of agent fallback. |
| Runtime-owned `workspace_dotnet_new` receipts | Clean create path invokes governed `workspace_dotnet_new`; existing targets emit `RuntimeOwned:IdempotentSkip` receipts. | Existing completion receipt gates continue to consume receipt records through the normal adapter result path. | `TryExecuteAsync_repairs_existing_project_empty_solution_without_regenerating_project` proves existing project repair skips destructive regeneration. |
| Runtime-owned `workspace_pwsh_run_script` receipt | Executor writes/stats the resolved helper and invokes `workspace_pwsh_run_script` with product-mutation manifest. | Completion gates still verify required tool receipt and file-content readback after runtime-owned execution. | `TryExecuteAsync_returns_failure_when_helper_does_not_satisfy_readback` proves a successful script receipt alone is not enough. |
| Add-test project readback | Add-test helper test creates test project membership and `ProjectReference` in product files. | Executor validates `ProductCompletionRequiredFileContentChecksByStep` before `Completed`. | Helper failure and readback failure tests prove missing project/reference evidence blocks success. |

## File Hashes

- Hash ledger: `proof/SB11/changed-file-hashes.txt`.

## Completed Validator Metadata

- Semantic invariant contract: `proof/SB11/semantic-invariants.md`.
- Portable source proof: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs`.
- Portable bundle proof: `bundle://proof/SB11/changed-file-hashes.txt`.
- SHA-256 changed-file hash: `771A8599ECAD469CDE5CAC26788D0F2598D31E29EAF3CB7A6ECE05671C6DAEB0`.
- Passing transcript: `proof/SB11/transcripts/01-targeted-runtime-owned-dotnet-tests.txt`.
- Anti-stub audit transcript: `proof/SB11/transcripts/04-anti-stub-audit.txt`.
- Failing-first: N/A - process/non-production final proof uses adversarial helper/readback negative tests inside the passing targeted transcript rather than preserving a historical failing transcript.


