# SB02 Proof Manifest

- Subbundle: SB02
- Status: Completed
- Owned requirements: RQ-003
- Raw notes: provider seam needs stable identity, domain tags, tool ownership, operation kind, approval expectation, and supported purposes before more migrations.
- Semantic invariant contract: bundle://proof/SB02/semantic-invariants.md

## Changed File Hashes

- Representative SHA-256: manifest.md FEFB65C6DCE4F21397283E56A0A19263DF3CCD02FD6F141B975C7E5767D5A6EC
- Hash manifest: bundle://proof/SB02/source-assertions/changed-file-hashes.txt

## Command Transcripts

- Failing-first absence check: bundle://proof/SB02/transcripts/failing-first-no-provider-descriptor-absence-check.txt
- Provider metadata tests: bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt
- Policy regression tests: bundle://proof/SB02/transcripts/agent-tool-invocation-policy-tests.txt
- Solution build: bundle://proof/SB02/transcripts/solution-build.txt
- Anti-stub audit: bundle://proof/SB02/transcripts/anti-stub-audit.txt

## Failing-First And Passing Proof

- Failing-first: bundle://proof/SB02/transcripts/failing-first-no-provider-descriptor-absence-check.txt
- Passing: bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt, bundle://proof/SB02/transcripts/agent-tool-invocation-policy-tests.txt, and bundle://proof/SB02/transcripts/solution-build.txt

## Source Assertions

- Source assertions: bundle://proof/SB02/source-assertions/provider-metadata-source-assertions.txt
- Changed-file hashes: bundle://proof/SB02/source-assertions/changed-file-hashes.txt

## Anti-Stub Audit

- Anti-stub audit transcript: bundle://proof/SB02/transcripts/anti-stub-audit.txt

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `AgentRuntimeToolProviderDescriptor` | `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`; `bundle://proof/SB02/source-assertions/provider-metadata-source-assertions.txt` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` records descriptors during capability composition | `bundle://proof/SB02/transcripts/failing-first-no-provider-descriptor-absence-check.txt`; duplicate-key test in `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` |
| `AgentRuntimeToolMetadata` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB02/source-assertions/provider-metadata-source-assertions.txt` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`; `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` | Metadata is produced for provider tools during every runtime capability composition path | Unknown-tool metadata rejection in `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` |

## Browser And Host Proof

- Browser proof: N/A; SB02 changes runtime contracts, provider composition, tests, and proof artifacts only.
- Host proof: N/A; no desktop or process-launch behavior changed.

## Downstream Smoke Proof

- bundle://proof/SB02/transcripts/solution-build.txt passed before SB03.
