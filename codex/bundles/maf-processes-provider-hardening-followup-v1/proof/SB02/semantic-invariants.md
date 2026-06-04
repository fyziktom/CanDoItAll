# SB02 Semantic Invariants

- Invariant ID: SB02-INVARIANT-001
- Source raw note: RQ-003 first-party runtime providers must expose descriptor/ownership metadata before more product providers are migrated.
- Expected behavior: Runtime provider composition accepts legacy raw AITool providers through an adapter, records explicit first-party provider descriptors, rejects duplicate provider keys, and records operation-kind/approval metadata for tools.
- Disallowed shallow implementation: Adding a descriptor type that MAF never consumes, or accepting duplicate provider keys while only preserving tool counts.
- Failing-first test: bundle://proof/SB02/transcripts/failing-first-no-provider-descriptor-absence-check.txt proves the no-descriptor shallow path is rejected by the delivered source shape.
- Passing test: bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt, bundle://proof/SB02/transcripts/agent-tool-invocation-policy-tests.txt, and bundle://proof/SB02/transcripts/solution-build.txt.
- Changed source files: bundle://proof/SB02/source-assertions/changed-file-hashes.txt.
- Production assertions: bundle://proof/SB02/source-assertions/provider-metadata-source-assertions.txt.
- Red-team negative case: Duplicate provider-key and unknown-tool metadata rejection in bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt would fail a shallow metadata-only implementation.
- Downstream dependency check: SB03 may use provider-neutral descriptors because bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt proves legacy adapters, explicit descriptors, duplicate-key failure, and operation-kind classification.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `AgentRuntimeToolProviderDescriptor` | `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`; `bundle://proof/SB02/source-assertions/provider-metadata-source-assertions.txt` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` records descriptors during capability composition | `bundle://proof/SB02/transcripts/failing-first-no-provider-descriptor-absence-check.txt`; duplicate-key test in `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` |
| `AgentRuntimeToolMetadata` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`; `bundle://proof/SB02/source-assertions/provider-metadata-source-assertions.txt` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`; `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` | Metadata is produced for provider tools during every runtime capability composition path | Unknown-tool metadata rejection in `bundle://proof/SB02/transcripts/agent-runtime-tool-provider-tests.txt` |
