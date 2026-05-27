# SB07 Proof Manifest

## Scope

- Subbundle: SB07 - A2A v1 and handoff regression.
- Invariant ID: SB07-INV-001
- Shipped behavior: A2A/handoff behavior is covered by source proof and a deterministic two-agent communication smoke.

## Source Proof

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafHandoffWorkflowFactory.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/A2ARemoteAgentToolFactory.cs
- repo://src/CanDoItAll.AgentFramework.Hosting/AgentA2AHostCardFactory.cs
- repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs
- bundle://proof/SB07/semantic-invariants.md
- bundle://analysis/04-maf16-feature-adoption-matrix.md

## Command Transcripts

- Passing transcript: bundle://proof/SB07/transcripts/passing.txt
- Adversarial negative proof transcript: bundle://proof/SB07/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/SB07/transcripts/anti-stub-audit.txt
- Source assertions transcript: bundle://proof/SB07/transcripts/source-assertions.txt
- Changed-file hashes transcript: bundle://proof/SB07/transcripts/changed-file-hashes.txt
- Test name: CanDoItAll.Tests.Integration.MafAgentRuntimeHandoffTests.Handoff_workflow_transfers_from_entry_agent_to_target_agent

## Changed File Hashes

- repo://codex/bundles/maf16-processes-real-usage-hardening-v2/analysis/04-maf16-feature-adoption-matrix.md: 19B3DD358326D819E0D890A76F8111A622DF45D513D05EF0118F221ED946DBDB
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs: EE2154A3C026E749BED344F798887FB5B1633CD644751BF4DFE25901E1D931FD
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs: 61ADE5D9098CB0549F2AAD53A8CC381B88D0785A0263CDE8EBDCBE418BA2CC29
