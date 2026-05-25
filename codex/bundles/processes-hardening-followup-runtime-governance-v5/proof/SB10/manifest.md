# SB10 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessDefinitionFormTests.cs
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB10 runtime governance artifact | repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs | repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs and bundle://proof/SB10/transcripts/passing.txt | Verified by bundle://proof/SB10/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB10/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB10/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB10/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB10/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessDefinitionLinterTests.Analyze_SB10_INV_001_accepts_architecture_report_without_product_mutation_contract`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.PublishAsync_SB10_INV_001_applies_strict_lint_for_high_criticality_definitions`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.StartRunAsync_SB10_INV_001_applies_strict_lint_for_delegated_published_definitions`
- Test name: `CanDoItAll.Tests.Components.ProcessDefinitionFormTests.Render_SB10_INV_001_shows_all_lint_issues`

## Anti-Stub Audit

- bundle://proof/SB10/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB10/transcripts/changed-file-hashes.txt
- `723a55dcba4d78b63c9ea24f6d820e95ce6694a86bfd30d75f0b18b893bc6af8`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `3083277fccf897fb6a73f49bef706d3584fd0250164f245b831a0309f01fccc6`  `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `834d86d0959e3672f7b21ebed2b33f3bdb0a99f3b233d3dab3821eada9651f43`  `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`

## Validation

- Focused proof commands passed for SB10; see bundle://proof/SB10/transcripts/passing.txt.
- Source assertions passed for SB10; see bundle://proof/SB10/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB10/transcripts/anti-stub-audit.txt.

## Blockers

None.
