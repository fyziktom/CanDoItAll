# Execution Report

## Status

Completed.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB01 | Passed via source prerequisites | Passed via proof/SB01/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | API and tool schema parity across typed process runtime contracts. |
| SB02 | Passed via source prerequisites | Passed via proof/SB02/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Operation contract and target scope fields remain visible through API/tool/read surfaces. |
| SB03 | Passed via source prerequisites | Passed via proof/SB03/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Artifact output mapping and projection lineage fields survive nested API routes. |
| SB04 | Passed via source prerequisites | Passed via proof/SB04/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Checkpoint A confirms API contract normalization is explicit and used. |
| SB05 | Passed via source prerequisites | Passed via proof/SB05/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Process skill and documentation governance stay aligned with typed runtime fields. |
| SB06 | Passed via source prerequisites | Passed via proof/SB06/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Template migration remains generic and does not regress operation contracts. |
| SB07 | Passed via source prerequisites | Passed via proof/SB07/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Grounding ledger policy remains backed by persisted artifact lineage. |
| SB08 | Passed via source prerequisites | Passed via proof/SB08/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Projection identity hash dedupe is visible through persisted and read surfaces. |
| SB09 | Passed via source prerequisites | Passed via proof/SB09/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Artifact validation remains centralized around typed expectation and lineage data. |
| SB10 | Passed via source prerequisites | Passed via proof/SB10/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Checkpoint B confirms artifact lineage validation is source-backed. |
| SB11 | Passed via source prerequisites | Passed via proof/SB11/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Typed block cause drives recovery routing without relying only on reason text. |
| SB12 | Passed via source prerequisites | Passed via proof/SB12/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Workflow and subprocess output mapping fields stay enforced and readable. |
| SB13 | Passed via source prerequisites | Passed via proof/SB13/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Script side-effect and post-execution audits have source-backed evidence. |
| SB14 | Passed via source prerequisites | Passed via proof/SB14/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Checkpoint C confirms recovery health values reach API/read models. |
| SB15 | Passed via source prerequisites | Passed via proof/SB15/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Process health observability exposes typed recovery and lineage state through API JSON. |
| SB16 | Passed via source prerequisites | Passed via proof/SB16/manifest.md | Checked through proof/SB16/transcripts/passing.txt | Completed | Final closure proves generic process hardening without SQLite or domain-specific assumptions. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| SB15 | api-process-run-detail-json | API JSON response | Not required for rendered UI; API response verified by ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state in proof/SB16/transcripts/passing.txt | N/A | Passed |

## Analytics Review

- API/read-model observability for typed block cause, recovery options, operation contracts, projection lineage, and projection identity is covered by repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs and proof/SB16/transcripts/passing.txt.
- Full solution build and focused unit/integration/component suites passed. Existing MSB3277 Entity Framework relational version conflict warnings are recorded in the transcript and were not introduced by this change.
- The original API/tool field audit command using README* produced rg exit 2 under PowerShell because the glob was passed literally; the normalized README.md audit passed and is appended to proof/SB16/transcripts/passing.txt.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| Generic process hardening follow-up after phase6 | Closed | proof/SB01/manifest.md through proof/SB16/manifest.md plus proof/SB16/transcripts/passing.txt |

## SB01 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB01/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB01/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB01/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB01/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB01/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB01/semantic-invariants.md and bundle://proof/SB01/transcripts/passing.txt prove invariant SB01-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB01/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB02 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB02/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB02/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB02/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB02/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB02/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB02/semantic-invariants.md and bundle://proof/SB02/transcripts/passing.txt prove invariant SB02-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB02/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB03 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB03/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB03/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB03/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB03/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB03/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB03/semantic-invariants.md and bundle://proof/SB03/transcripts/passing.txt prove invariant SB03-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB03/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB04 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB04/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB04/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB04/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB04/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB04/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB04/semantic-invariants.md and bundle://proof/SB04/transcripts/passing.txt prove invariant SB04-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB04/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB05 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB05/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB05/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB05/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB05/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB05/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB05/semantic-invariants.md and bundle://proof/SB05/transcripts/passing.txt prove invariant SB05-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB05/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB06 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB06/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB06/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB06/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB06/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB06/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB06/semantic-invariants.md and bundle://proof/SB06/transcripts/passing.txt prove invariant SB06-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB06/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB07 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB07/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB07/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB07/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB07/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB07/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB07/semantic-invariants.md and bundle://proof/SB07/transcripts/passing.txt prove invariant SB07-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB07/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB08 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB08/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB08/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB08/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB08/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB08/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB08/semantic-invariants.md and bundle://proof/SB08/transcripts/passing.txt prove invariant SB08-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB08/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB09 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB09/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB09/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB09/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB09/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB09/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB09/semantic-invariants.md and bundle://proof/SB09/transcripts/passing.txt prove invariant SB09-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB09/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB10 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB10/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB10/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB10/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB10/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB10/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB10/semantic-invariants.md and bundle://proof/SB10/transcripts/passing.txt prove invariant SB10-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB10/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB11 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB11/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB11/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB11/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB11/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB11/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB11/semantic-invariants.md and bundle://proof/SB11/transcripts/passing.txt prove invariant SB11-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB11/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB12 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB12/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB12/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB12/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB12/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB12/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB12/semantic-invariants.md and bundle://proof/SB12/transcripts/passing.txt prove invariant SB12-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB12/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB13 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB13/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB13/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB13/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB13/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB13/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB13/semantic-invariants.md and bundle://proof/SB13/transcripts/passing.txt prove invariant SB13-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB13/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB14 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB14/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB14/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB14/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB14/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB14/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB14/semantic-invariants.md and bundle://proof/SB14/transcripts/passing.txt prove invariant SB14-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB14/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB15 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB15/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB15/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB15/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB15/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB15/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB15/semantic-invariants.md and bundle://proof/SB15/transcripts/passing.txt prove invariant SB15-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB15/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.
## SB16 Semantic Adequacy Evidence

- Raw note owned: bundle://inputs/02-structured-input.md and bundle://requirements/01-normalized-requirements.md are closed by proof/SB16/manifest.md.
- Shipped behavior: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs expose typed process contract data through nested API and read-model surfaces.
- Source proof: bundle://proof/SB16/transcripts/source-assertions.txt cites repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, and repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Test proof: bundle://proof/SB16/transcripts/passing.txt and bundle://proof/SB16/transcripts/passing.txt include dotnet test commands and ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state.
- Shallow-pass trap: bundle://proof/SB16/transcripts/failing-first.txt rejects omitted DTO mapping and placeholder lineage/recovery handling.
- Adversarial negative proof: bundle://proof/SB16/transcripts/failing-first.txt records the expected non-zero sentinel search result.
- Semantic positive proof: proof/SB16/semantic-invariants.md and bundle://proof/SB16/transcripts/passing.txt prove invariant SB16-INV-001.
- Anti-stub audit: no stubs; bundle://proof/SB16/transcripts/anti-stub-audit.txt confirms concrete mappings and JSON/API regression coverage.

