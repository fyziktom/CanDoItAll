# SB10 Proof Manifest

## Status

- Subbundle: `SB10 - Workflow Template And Descriptor Loading`
- Result: `Passed`
- Closure date: `2026-06-29`
- Next gate: `SB11 - MAF Compiler Backend Adapter Isolation`

## Implementation Summary

- Added `CanDoItAll.AgentFramework.Workflows.Templates` for template pack root resolution, YAML parsing, template DTOs, input parameter materialization, model/runtime settings mapping, graph materialization, preview simulation fixture loading, descriptor validation, and typed template diagnostics.
- Deleted the Blazor-module-owned `WorkflowTemplatePackLoader` implementation; the module now delegates template services through `AddWorkflowTemplateServices()`.
- Extended workflow builders so template materialization can preserve existing workflow input/output port ids and start/end null shapes without duplicating graph construction.
- Added focused unit coverage for all current templates plus malformed YAML, missing executor, invalid routing, invalid input parameter, invalid runtime policy, invalid executor settings, malformed preview simulation JSON, descriptor validation, and no UI fallback ownership.
- Updated bundle docs and the XLSX workbook through SB10.

## Verification

| Proof | Result | Transcript |
| --- | --- | --- |
| Entry gate | Passed | `transcripts/entry-gate.txt` |
| Template project and consuming module builds | Passed, 0 warnings/errors | `transcripts/template-builds.txt` |
| All-template and negative diagnostic tests | Passed, 10/10 | `transcripts/all-template-and-negative-tests.txt` |
| Component compile and UI scope note | Passed, 0 warnings/errors | `transcripts/component-compile-large-screen-note.txt` |
| Static ownership and responsibility check | Passed | `transcripts/static-ownership-and-responsibility-check.txt` |
| Semantic source assertions | Passed | `transcripts/semantic-source-assertions.txt` |
| Anti-stub and fallback audit | Passed with documented intentional null/empty-fixture cases | `transcripts/anti-stub-audit.txt` |
| Workbook update and verification | Passed | `transcripts/workbook-verification.txt` |
| Prepared-stage validator | Passed | `transcripts/prepared-validator.txt` |
| Closure audit | Passed | `transcripts/closure-audit.txt` |

## Commands

```powershell
dotnet build src\CanDoItAll.AgentFramework.Workflows.Templates\CanDoItAll.AgentFramework.Workflows.Templates.csproj -v:minimal
dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj --no-restore -v:minimal
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WorkflowTemplatePackLoaderTests -v:minimal -p:OutputPath=C:\repositories\CanDoItAll\artifacts\sb10-unit-output\
dotnet build tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -m:1 -v:minimal -p:OutputPath=C:\repositories\CanDoItAll\artifacts\sb10-components-output\
```

## Caveats

- A running `CanDoItAll.Web` process locked default Web output files. SB10 proof used isolated output folders and did not stop the process.
- Visible workflow template selection behavior was not intentionally changed. Browser proof remains deferred to SB12/SB13/SB14, large-screen only.

## Artifacts

- `changed-file-hashes.txt`
- `semantic-invariants.md`
- `transcripts/*.txt`

