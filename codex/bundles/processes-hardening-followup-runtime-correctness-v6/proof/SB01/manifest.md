# SB01 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`: `GroundPromptExternalTargetAliases` now calls `RemoveWritableCoveredReadOnlyExternalTargetAliases` after prompt merge; the helper removes read-only aliases equal to or below a trusted writable alias while preserving sibling/parent read-only aliases.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`: `EvaluateReadOnlyExternalTargetMutation` skips read-only denial for referenced aliases covered by `AllowedExternalTargetAliases`.
- `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs`: tests cover same alias, child alias covered by writable parent, and sibling outside writable parent.
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`: tests cover read-only/writable overlap, read-only parent with writable child, and read-only-only denial.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB01 verified runtime behavior | repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs | bundle://proof/SB01/manifest.md | bundle://proof/SB01/transcripts/passing.txt | bundle://proof/SB01/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Failing-First or Red-Team Proof

- Transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Test name: `CanDoItAll.Tests.Unit.AgentWorkspaceToolAccessMetadataTests.GroundPromptExternalTargetAliases_does_not_add_same_trusted_writable_alias_as_read_only`
- Test name: `CanDoItAll.Tests.Unit.AgentWorkspaceToolAccessMetadataTests.GroundPromptExternalTargetAliases_does_not_add_child_alias_covered_by_writable_parent_as_read_only`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_allows_product_file_mutation_when_external_target_is_trusted_writable_despite_readonly_overlap`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_does_not_let_readonly_parent_deny_writable_child_alias`

## Passing Proof

- Transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Test name: `CanDoItAll.Tests.Unit.AgentWorkspaceToolAccessMetadataTests.GroundPromptExternalTargetAliases_does_not_add_same_trusted_writable_alias_as_read_only`
- Test name: `CanDoItAll.Tests.Unit.AgentWorkspaceToolAccessMetadataTests.GroundPromptExternalTargetAliases_does_not_add_child_alias_covered_by_writable_parent_as_read_only`
- Test name: `CanDoItAll.Tests.Unit.AgentWorkspaceToolAccessMetadataTests.GroundPromptExternalTargetAliases_adds_sibling_alias_outside_writable_parent_as_read_only`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_allows_product_file_mutation_when_external_target_is_trusted_writable_despite_readonly_overlap`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_does_not_let_readonly_parent_deny_writable_child_alias`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_denies_product_file_mutation_when_external_target_is_only_read_only`

## Anti-Stub Audit

- Transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`
- `42E9361D8E5C6DCED75A1FEF5D006AFC0A90059F68B4173CB8A9D1FBECAA6154` `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `C1089DCEA677C48E349F049732E68FA87ED349EECAB1C665A88D26C45F0DFB12` `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `037473819CFD8B234711E746D6F61DEDE40F4C274B062A343A9606189E6AE1D5` `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs`
- `782A33167D96C57BCC3136B8035F246CAF95DC7B428204CCE1BD5913A4C12232` `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`

## Validation

- Focused unit tests passed: `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentWorkspaceToolAccessMetadataTests|FullyQualifiedName~AgentToolInvocationPolicyTests"`.
- Prepared-stage bundle validator passed before SB01 implementation.

## Blockers

None.


