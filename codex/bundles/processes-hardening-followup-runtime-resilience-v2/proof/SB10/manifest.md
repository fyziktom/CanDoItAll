# SB10 Proof Manifest

## Status

Completed.

## Owned Requirements And Raw Notes

- Requirements: RQ14 plus final closure coverage for RQ01-RQ13
- Raw notes: N001, N002, N003, N004, N005, N006, N007
- Semantic invariant contract: `bundle://proof/SB10/semantic-invariants.md`

## Source Assertions

- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs` covers business, legal, manufacturing, research, workflow, and strict lint red-team scenarios.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers software process boundaries, artifact validation, workflow/subprocess producers, upstream unblock, no-progress retry compression, and active execution adoption.
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` and `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs` cover tool-policy and metadata no-autopromotion boundaries.
- Transcript: `bundle://proof/SB10/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Generic red-team validation suite | `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` | Final bundle closure and regression suite | Focused and full test runs execute before closure | `bundle://proof/SB10/transcripts/failing-first.txt` covers false-positive and shallow-proof rejection cases |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB10/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB10/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB10/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB10/transcripts/changed-file-hashes.txt`

Representative changed-file SHA-256: `bbf765f7f238243eb9b79a70695d84c9d6145dbd4a39a8195e9badd4590a407e`

## Validation

Completed through focused integration tests, full unit tests, solution build, SQLite audit, and completed bundle validator.

## Blockers

None.
