# SB11 Proof Manifest

## Status

Completed.

## Goal

Keep HTTP API, OpenAPI route coverage, MAF process tools, policy classification, and process API skill guidance aligned after live-run profile governance was added.

## Implementation Summary

- Added typed `FreshRunPolicy` to `ProcessTemplateLiveRunProfileSummary` in `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs`.
- Returned the fresh-run policy from `GET /api/processes/templates/live-run-profiles` in `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`.
- Added governed MAF read tool `processes_template_live_run_profiles_list` in `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`.
- Registered `ProcessesTemplateLiveRunProfilesList` as a read-classified process tool in `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`.
- Tightened API/OpenAPI and MAF tool parity tests in `repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs`, and `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`.
- Updated `repo://codex/skills/candoitall-api-processes/SKILL.md` and synced it to the active Codex skill root; matching SHA-256 proof is in `bundle://proof/SB11/transcripts/skill-sync.txt`.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs | Adds `FreshRunPolicy` to the live-run profile summary DTO. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Web/Api/ProcessesApi.cs | Projects typed fresh-run policy through the live-run profiles API route. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs | Registers the live-run profiles process tool as read-only. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs | Exposes the live-run profiles summary through a governed internal process tool. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs | Proves OpenAPI route inventory and fresh-run policy API shape. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs | Proves MAF process tool composition includes the new read tool and role-add mutation coverage. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs | Proves the new process tool is read-classified and approval-free. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |
| repo://codex/skills/candoitall-api-processes/SKILL.md | Documents live-run profile policy fields and the new template-curator tool. | bundle://proof/SB11/transcripts/changed-file-hashes.txt |

## Changed-file Hashes

- SHA-256 `7F84DB3D5892823058BD31A8CD6F35ACBA115AA46ED832811EBE069B50A08986` repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs
- SHA-256 `B42862DB3D7F37F692455BD426E2DB53FF2BA3E02A524BBB5DB60B75EC033EFE` repo://src/CanDoItAll.Web/Api/ProcessesApi.cs
- SHA-256 `BA943B12323AE91D4926501D4C050758F2E89D72DF60F097FBB0EB42D5AB6D61` repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- SHA-256 `36B71AD7FEE94D4A58865EF874A56C8E846EE560FA21185024959E2FC5D11428` repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs
- SHA-256 `B041D15F9455718DB8F7C126004BA1C9E4A7ADEEA25918FD3C5DD236CD59BA36` repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs
- SHA-256 `F23A0708F690C90BECF97576B1BCE8847E7F4A1BE2A5C3B283B2A6D8E433AE6B` repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs
- SHA-256 `9CE8FF3C7157E668817CDC7DC73768E29AA4B344B3004C97063631A3FDB402A1` repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- SHA-256 `CD4E2CFF782BD4AFF42F6E6ACD9F026006710374C273168B8B2633C18F0E71BE` repo://codex/skills/candoitall-api-processes/SKILL.md
- SHA-256 `CD4E2CFF782BD4AFF42F6E6ACD9F026006710374C273168B8B2633C18F0E71BE` active Codex `candoitall-api-processes` skill copy

## Failing-first or adversarial proof

`bundle://proof/SB11/transcripts/failing-first.txt`

- Rejects the stale summary DTO shape where `TriggerReasonTemplate` flowed directly to count fields without `FreshRunPolicy`.

## Passing proof

`bundle://proof/SB11/transcripts/passing.txt`

- API/OpenAPI/MAF parity focused integration tests: 3 passed.
- `AgentToolInvocationPolicyTests`: 119 passed.

## Source assertions

`bundle://proof/SB11/transcripts/source-assertions.txt`

## Anti-stub audit

`bundle://proof/SB11/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`bundle://proof/SB11/transcripts/changed-file-hashes.txt`

## Closure Validator

`bundle://proof/SB11/transcripts/closure-validator.txt` records no SB11 validator findings.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessTemplateLiveRunProfileSummary.FreshRunPolicy` | `ProcessTemplatePackLoader` and `ProcessesApi` projection from `ProcessTemplateLiveRunProfile`. | HTTP API clients, OpenAPI consumers, process API skill users, and MAF process tools. | Loaded from the template pack, projected into summary responses, and read before starting fresh live runs. | Stale DTO-shape rejection in `bundle://proof/SB11/transcripts/failing-first.txt`. |
| `processes_template_live_run_profiles_list` | `MafAgentRuntime.ProcessToolBuilder`. | Agents with process read access and template-curator roles. | Composed with internal process tools when services are available, enforced by `AgentProcessAccessMetadata`, and classified as read-only. | Tool policy and MAF composition tests in `bundle://proof/SB11/transcripts/passing.txt`. |
| OpenAPI template route inventory | `ProcessesApi.MapTemplateEndpoints`. | API clients and docs/skill refresh work in SB12/SB17. | Route inventory includes detail, envelope, mermaid, import, baseline scenarios, and live-run profiles. | Focused OpenAPI route test in `bundle://proof/SB11/transcripts/passing.txt`. |
| Active `candoitall-api-processes` skill live-run guidance | Repo skill synced to active Codex skill root. | Human operators and agents using the process API skill. | Documents `freshRunPolicy` preservation and the new live-run profiles process tool. | Skill sync hash proof in `bundle://proof/SB11/transcripts/skill-sync.txt`. |
