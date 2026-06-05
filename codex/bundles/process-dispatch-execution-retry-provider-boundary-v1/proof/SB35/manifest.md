# SB35 Proof Manifest - Gate G Provider Recovery Parity

## Status

- Completed.

## Portable References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoverableProviderFailureRules.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderFallbackSelectionRules.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderHealthProbeCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAssignedAgentProviderRepairCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRecoveryDirectiveBuilder.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs
- bundle://proof/SB35/semantic-invariants.md
- bundle://proof/SB35/transcripts/focused-provider-recovery-tests.txt
- bundle://proof/SB35/transcripts/source-assertions-and-scans.txt

## Changed Source SHA-256

- 2aeab21c26113b763b9fc6b3cfbecf5e9cb3a328329abc1a0fd6b9a25c7ee982 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs
- cb0a38c7bac414731c0dd7fb7be9f1182c98f52e589a0049091f52f7275da776 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs
- a884b46bd9516af51c5853f4be57227878f7016fc9a60a328100731cfb17011d repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs
- 254ead79a0a82728ef3423b10588f38ea800666459548dd66087fd2f170613a9 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs
- b51b3de2cfc004c2fc79f3c7e38cf42cf3a73c0a068052241e8d2f02884f4fc2 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs
- c47b64c440dfa06062cee54217ff4d7cdec1f36112be590024f2354a299da920 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoverableProviderFailureRules.cs
- ce5697fc43f1830717891b43549e03af053cd86181983e056de08153fa476d5e repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderFallbackSelectionRules.cs
- 66c61fe6d3f6af88e99290ea3ad25982c25d8e683c0a82711fdee69e4cca5874 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderHealthProbeCoordinator.cs
- 09b055ef09ea282ef0a5c14142d83b7226700322edd33ad37c00c6d4e1e9c886 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAssignedAgentProviderRepairCoordinator.cs
- 4c6e07c74ef0c44c6fa1c15f6786c60283bca9caeae711ddf85fefac0d6a24cf repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRecoveryDirectiveBuilder.cs
- 3b4fe164dee28e18bf41115a54de21194d932c7f4533b66a6d1620acca9fbec6 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs

## Changed Source Files

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoverableProviderFailureRules.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderFallbackSelectionRules.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderHealthProbeCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAssignedAgentProviderRepairCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRecoveryDirectiveBuilder.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs

## Command Transcripts

- bundle://proof/SB35/transcripts/focused-provider-recovery-tests.txt
- bundle://proof/SB35/transcripts/source-assertions-and-scans.txt

## Semantic Contract

- Invariant ID: SB35-INV-001
- Contract: bundle://proof/SB35/semantic-invariants.md

## Passing Evidence

- Passing transcript: bundle://proof/SB35/transcripts/focused-provider-recovery-tests.txt
- Semantic positive proof: bundle://proof/SB35/transcripts/focused-provider-recovery-tests.txt

## Failing-First And Negative Evidence

- Failing-first: N/A - process non-production refactor with no behavior change; provider recovery parity remains covered by focused tests and source assertions.
- Adversarial negative proof: bundle://proof/SB35/transcripts/source-assertions-and-scans.txt
- Anti-stub audit transcript: bundle://proof/SB35/transcripts/source-assertions-and-scans.txt

## Downstream Dependency Review

- Downstream dependencies checked: SB36-SB40 execution loop work can rely on explicit provider classification, fallback selection, health probe, repair mutation, and provider directive boundaries.
- Result: verified complete for SB35.
