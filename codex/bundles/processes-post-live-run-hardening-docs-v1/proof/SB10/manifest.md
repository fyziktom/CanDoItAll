# SB10 Proof Manifest

## Status

Completed.

## Goal

Ensure agents have needed skills/tools and do not improvise.

## Implementation Summary

- Added typed capability requirement and diagnostic records in `repo://src/CanDoItAll.AgentFramework.Models/Capabilities/CapabilityModels.cs`.
- Added `AgentCapabilityRequirementEvaluator` in `repo://src/CanDoItAll.AgentFramework.Core/Capabilities/AgentCapabilityRequirementEvaluator.cs` to detect missing, stale, missing-catalog, and retired role capabilities.
- Reused the evaluator's retired-capability filter in runtime capability composition in `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`.
- Added the role-by-role skill/tool matrix to `repo://src/CanDoItAll.AgentFramework.Core/README.md` and `repo://codex/skills/candoitall-api-processes/SKILL.md`.
- Synced the updated repo skill to the active Codex skill root; matching SHA-256 proof is in `bundle://proof/SB10/transcripts/skill-sync.txt`.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.AgentFramework.Models/Capabilities/CapabilityModels.cs | Defines typed role capability requirements, diagnostic codes, diagnostics, and evaluation result. | bundle://proof/SB10/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Core/Capabilities/AgentCapabilityRequirementEvaluator.cs | Evaluates missing, uncataloged, stale, and retired role capabilities. | bundle://proof/SB10/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs | Uses the shared retired-capability predicate during runtime capability composition. | bundle://proof/SB10/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Core/README.md | Documents the governed process role skill/tool matrix and anti-improvisation layers. | bundle://proof/SB10/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs | Adds missing, retired, and positive role capability diagnostic proof. | bundle://proof/SB10/transcripts/changed-file-hashes.txt |
| repo://codex/skills/candoitall-api-processes/SKILL.md | Documents the process role skill/tool matrix for API users and agent workflows. | bundle://proof/SB10/transcripts/changed-file-hashes.txt |

## Changed-file Hashes

- SHA-256 `1DD8783544F13517FEC7A8E14C641AEA536C516206FEB87664FA6B3E298AC953` repo://src/CanDoItAll.AgentFramework.Models/Capabilities/CapabilityModels.cs
- SHA-256 `287273CFF11D2BF50D510484D4F651617FEB65539A785A43696955B69CE90619` repo://src/CanDoItAll.AgentFramework.Core/Capabilities/AgentCapabilityRequirementEvaluator.cs
- SHA-256 `7539720EDFFD0FA1191A38393213327D3B1185CE158BEEEBB6C211E7FF7D1A68` repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs
- SHA-256 `CFF1632B22D57E203EA6E0C428800AE94757B8EF2828E0FAE3A1ADF87ACC0C1A` repo://src/CanDoItAll.AgentFramework.Core/README.md
- SHA-256 `0D524926F1065FD391ED1A9CD308ED980208E7CC599682DC8F3DB4E42199B3F3` repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs
- SHA-256 `BA1E9979B91B78CA3B365C619C12B8F8953579BEB49D2DB8F7BF635F18DD74E9` repo://codex/skills/candoitall-api-processes/SKILL.md
- SHA-256 `BA1E9979B91B78CA3B365C619C12B8F8953579BEB49D2DB8F7BF635F18DD74E9` active Codex `candoitall-api-processes` skill copy

## Failing-first or adversarial proof

`bundle://proof/SB10/transcripts/failing-first.txt`

- `AgentCapabilityRequirementEvaluator_reports_typed_missing_required_skill` proves a missing required process skill produces typed `MissingRequiredCapability`.
- `AgentCapabilityRequirementEvaluator_reports_retired_required_skill` proves retired sandbox skills are rejected as typed `RetiredCapability`.
- `EvaluateAsync_denies_unknown_tool_even_when_read_like` and `EvaluateAsync_denies_known_tool_when_policy_classification_is_missing` prove tool improvisation is denied.

## Passing proof

`bundle://proof/SB10/transcripts/passing.txt`

- `AgentFrameworkExecutionCapabilityFilteringIntegrationTests`: 6 passed.
- `AgentToolInvocationPolicyTests`: 117 passed.

## Source assertions

`bundle://proof/SB10/transcripts/source-assertions.txt`

## Anti-stub audit

`bundle://proof/SB10/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`bundle://proof/SB10/transcripts/changed-file-hashes.txt`

## Closure Validator

`bundle://proof/SB10/transcripts/closure-validator.txt` records no SB10 validator findings.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `AgentCapabilityRequirement` | Staffing or dispatch caller declares role capability needs. | `AgentCapabilityRequirementEvaluator.Evaluate`. | Created before dispatch from role matrix or process staffing policy. | Missing skill test in `bundle://proof/SB10/transcripts/failing-first.txt`. |
| `AgentCapabilityDiagnostic` | `AgentCapabilityRequirementEvaluator`. | Staffing, dispatch, tests, and operator diagnostics. | Emitted for missing assignment, missing catalog item, stale assignment, or retired capability. | Missing and retired diagnostic tests in `bundle://proof/SB10/transcripts/failing-first.txt`. |
| Retired capability filter | `AgentCapabilityRequirementEvaluator.IsRetiredCapability`. | `ResolveAttachedCapabilities` runtime composition. | Applied every time an agent's catalog capabilities are composed for execution. | Retired skill filtering and retired required skill tests in `bundle://proof/SB10/transcripts/passing.txt` and `bundle://proof/SB10/transcripts/failing-first.txt`. |
| Role-by-role skill/tool matrix | AgentFramework Core README and active `candoitall-api-processes` skill. | Process authors, process managers, dispatch code, and future SB11/SB12 docs/API work. | Updated with repo skill and active Codex skill hashes. | Skill sync and source assertions in `bundle://proof/SB10/transcripts/skill-sync.txt` and `bundle://proof/SB10/transcripts/source-assertions.txt`. |
| Tool anti-improvisation policy | `DefaultAgentToolInvocationPolicy`. | AgentFramework runtime tool calls. | Denies unknown tools and known tools without policy classification. | Unit policy tests in `bundle://proof/SB10/transcripts/failing-first.txt` and `bundle://proof/SB10/transcripts/passing.txt`. |
