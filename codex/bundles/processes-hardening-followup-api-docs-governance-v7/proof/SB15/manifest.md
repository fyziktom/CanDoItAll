# SB15 proof manifest

## Status

Completed.

## Owned subbundle

15-process-health-dashboard-api-and-observability

## Goal

Process health observability exposes typed recovery and lineage state through API JSON.

## Source assertions

- bundle://proof/SB15/transcripts/source-assertions.txt
- repo://src/CanDoItAll.Web/Api/ProcessesApi.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Reads.cs

## Semantic invariant contract

- proof/SB15/semantic-invariants.md
- bundle://proof/SB15/semantic-invariants.md
- Invariant ID: SB15-INV-001

## Failing-first or red-team proof

- Adversarial negative proof: bundle://proof/SB15/transcripts/failing-first.txt

## Passing proof

- Passing transcript: bundle://proof/SB15/transcripts/passing.txt
- Bundle aggregate validation: bundle://proof/SB16/transcripts/passing.txt
- Test name: ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state

## Anti-stub audit

- Anti-stub audit transcript: bundle://proof/SB15/transcripts/anti-stub-audit.txt

## Changed-file hashes

- Changed-file hashes transcript: bundle://proof/SB15/transcripts/changed-file-hashes.txt
- repo://src/CanDoItAll.Web/Api/ProcessesApi.cs SHA256 1b14c83c8af72622cdb408f6b18db5977f34e5c937efdb45933b9b553226a5f4
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs SHA256 510eab4ddf72f0818fb969434baa709b2bfdee42f91996dd9a759fedd0c89eb7
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs SHA256 590c29befda85c7275c66922bd7b54a2f2e7980ebe197de119dea65c4565dae3
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs SHA256 024db598893b9c32d89cabf6e844298a44b3e62e9c74368448413fb48bd0fadb
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Reads.cs SHA256 1729034f49c0358f974102c676cacb4919cc1b0c526b7458c0e0347293c5a4df
- repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs SHA256 d34bb133dae1b1dfbe84a3d4d0e3e7fac6a1fc452d943a33b0360f0c92f500b8

## Validation

- Prepared validator, focused API regression, AgentToolInvocationPolicyTests, ProcessRunAutomationDispatchServiceTests, ProcessesServiceIntegrationTests, ProcessDefinitionLinterTests, ProcessStepEditorFormTests, full solution build, PostgreSQL-only audit, and normalized API/tool field source audit are recorded in bundle://proof/SB16/transcripts/passing.txt.

## Shallow-pass trap

- A DTO-only property without runtime mapping is rejected by the HTTP regression and the adversarial negative transcript.

## Blockers

None.
