# SB02 Proof Manifest

## Status

- Result: `Passed`
- Scope: disabled-mode guards for optional Cognitive Memory integration points.

## Source Assertions

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` line 27 skips agent context contribution when disabled, before project-scope resolution.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` line 281 reads settings in the recall workflow executor; line 286 returns a skipped payload.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` line 396 reads settings in the probe workflow executor; line 401 returns a skipped payload.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` line 460 reads settings in the learning proposal workflow executor; line 465 returns a skipped payload.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` line 23 skips scheduled automation before actor, take, ingestion, or consolidation work.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs` line 124 centralizes the disabled reason and message.

## Semantic Contract

- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB03/transcripts/tests-passing.md`.
- Failing-first: N/A process. The runtime failure was supplied as the input artifact; guard tests reproduce the failure shape by omitting project scope while disabled.
- Adversarial negative proof: disabled tests cover missing scope and invalid downstream inputs.
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.md`.

## Changed-File Hashes

- `6368BF8410F03009C4B7B6D559DE804D17CE5E6C99A3C388A4B47D7097EEF9DA` `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `5FA600101775F5461A7F486DEC32A7FC87BB50EF13AE4456EABE4DF16AB304F2` `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs`
- `7F466F3B98AE041ECD7F83647AD9B7D994CAD0BA2A2D06602A336EB074674763` `repo://tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `C4192A04A47503E1009A2C1A30D00B1AD7AF5CD204B2358FD12F3731EB97F2CD` `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalServicesTests.cs`

## Validation

- `tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs:278` proves disabled agent context skips the reported missing project scope failure.
- `tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs:294` asserts the disabled trace reason.
- `tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalServicesTests.cs:222` proves disabled scheduled automation does not call downstream memory services.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~CognitiveMemoryOperationalSettingsTests|FullyQualifiedName~CognitiveMemoryOperationalServicesTests"` passed.
- `dotnet build CanDoItAll.slnx --no-restore` passed.

## Changed Files

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalServicesTests.cs`
