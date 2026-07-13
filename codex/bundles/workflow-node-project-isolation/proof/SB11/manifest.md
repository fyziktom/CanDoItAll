# SB11 Proof Manifest

## Status

- Subbundle: `SB11 - MAF Compiler Backend Adapter Isolation`
- Result: `Passed`
- Closure date: `2026-06-29`
- Next gate: `SB12 - API UI Workbench Adoption`

## Implementation Summary

- Added `CanDoItAll.AgentFramework.Workflows.MafAdapter` for the MAF workflow compiler, in-process backend, LLM component invoker, event normalizer, handoff workflow factory, configured artifact resolver, progress observer, external request capture, service registration, and compile-failure diagnostics.
- Removed MAF workflow compiler/backend/event/LLM/handoff ownership from `CanDoItAll.AgentFramework.Maf`; workflow-owned projects do not reference MAF or the MAF adapter.
- Centralized host and AgentFramework module composition through `AddMafWorkflowAdapterServices(...)`, removed the old `AddBuiltInWorkflowExecutors` alias, and kept standard executor composition behind `WorkflowExecutors.Standard`.
- Added typed redacted compile-failure diagnostics for MAF backend start failures while preserving existing executor/plugin diagnostics through executor core.
- Split the MAF backend by responsibility and guarded the main backend file at 421 lines.
- Repaired SB10 descriptor validation during SB11 integration so known but unavailable plugin descriptors remain loadable instead of blocking template pack loading before plugin installation.

## Verification

| Proof | Result | Transcript |
| --- | --- | --- |
| Entry gate | Passed | `transcripts/entry-gate.txt` |
| Adapter, MAF, Hosting, and module builds | Passed, 0 warnings/errors | `transcripts/adapter-builds.txt` |
| Focused adapter isolation tests | Passed, 4/4 | `transcripts/focused-adapter-isolation-tests.txt` |
| Adapter regression tests | Passed, 78/78 | `transcripts/adapter-regression-tests.txt` |
| Handoff/plugin integration tests | Passed, 32/32 | `transcripts/integration-adapter-tests.txt` |
| Static architecture check | Passed | `transcripts/static-architecture-check.txt` |
| Semantic source assertions | Passed | `transcripts/semantic-source-assertions.txt` |
| Anti-stub and no-fallback audit | Passed with one documented intentional no-artifact `return null` | `transcripts/anti-stub-audit.txt` |
| Workbook update and verification | Passed | `transcripts/workbook-verification.txt` |
| Output isolation caveat | Documented | `transcripts/dependency-output-caveat.txt` |
| Prepared-stage validator | Passed | `transcripts/prepared-validator.txt` |
| Closure audit | Passed | `transcripts/closure-audit.txt` |

## Commands

```powershell
dotnet build src\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj -v:minimal
dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj -v:minimal
dotnet build src\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj -v:minimal
dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj --no-restore -v:minimal
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "MafWorkflowAdapterIsolationTests|MafWorkflowEventNormalizerTests|AgentFrameworkHostingServiceCollectionTests|WorkflowFoundationTests|WorkflowPreviewSimulationTests|WorkflowExecutorTests|WorkflowExecutorCategoryIsolationTests" -v:minimal -p:OutputPath=repo://artifacts/sb11-unit-output
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "MafAgentRuntimeHandoffTests|PluginCatalogIntegrationTests" -m:1 -v:minimal -p:OutputPath=repo://artifacts/sb11-integration-output
```

## Caveats

- A running `CanDoItAll.Web` process locked default Web output files. SB11 proof used isolated output folders and did not stop the process.
- Browser-visible adoption remains SB12/SB13/SB14, large-screen only per user instruction.

## Artifacts

- `changed-file-hashes.txt`
- `semantic-invariants.md`
- `transcripts/*.txt`

## Completed Validator Metadata Addendum

- Portable proof reference: bundle://proof/SB11/manifest.md
- Semantic invariant contract: bundle://proof/SB11/semantic-invariants.md
- Command transcript path: bundle://proof/SB11/transcripts/adapter-builds.txt
- Passing transcript: bundle://proof/SB11/transcripts/adapter-builds.txt
- Anti-stub audit transcript: bundle://proof/SB11/transcripts/adapter-builds.txt
- Failing-first test: N/A - process/no production behavior metadata addendum for completed-stage validator compatibility.
- SHA-256 changed-file hash: 9DA44DB377640EF4190488A98233DC924DA4D89FBC0CC2641249143A3BEF6C1E bundle://proof/SB11/manifest.md
- Invariant ID: SB11-final-closure

Moved checkout copy validation: portable bundle references can be copied to a moved checkout without machine-specific paths.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| portable proof | bundle://proof/SB11/manifest.md | bundle://proof/SB11/transcripts/metadata-compliance.txt | bundle://proof/SB11/transcripts/metadata-compliance.txt negative metadata proof | Verified pass: portable proof references are closed for SB11. |



