# SB28 Proof Manifest

## Gate

- Critical gate: line-count and consumer parity.
- Status: Completed.
- Semantic contract: bundle://proof/SB28/semantic-invariants.md.

## Changed File Hashes

# Changed File Hashes

| File | SHA-256 | Lines |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs | 3c0850b1986ef5634b47ac06eb761c1e2c8e7d3d0daefb169552422839d9f50a | 2483 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionSnapshot.cs | 3178aaa5ebfe64602d68d5bbec2d880872a8f6242c82062cdf0e68765b5347c7 | 20 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactRecordedSatisfactionRules.cs | da8818fe1a33353a03b7d3b47ad8e1d631a6c0b75e1f01f9592bf90ffbccb3e4 | 13 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessFreshImplementationArtifactSatisfactionRules.cs | 37bd132c98eaa51e02b61126520048e37b964cc61a62597304ace0ec284f830a | 34 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRequiredArtifactAutoSatisfactionRules.cs | 9072b0851ca0834d01b1b357edefc1f42bd85a1db51237cbe2acdb725b625053 | 48 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessResponseTextArtifactSatisfactionRules.cs | f0de34aaee6ad60eaacf0d94831777021814740dd0bfdbf6e78d7c4b6f3119db | 100 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagedArtifactPathClassificationRules.cs | 4f68bc62b103cd7910eabf400431e109e92aee2ca74c90ae8861539ab6378a83 | 60 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessQualityValidationEvidenceAggregator.cs | 60bff161e5f91f474ca39d1d30803237b7c7654299289dfc5091a575a87b918a | 47 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessIncompleteImplementationSignalRules.cs | 8f2ad5dd33a7aa0ab0e7383f13a8c781aeefd512ffc03d3be5e574ba1dab74f1 | 77 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetReferenceGuard.cs | 592204c4ae63217d67979b80cb342d84419704fd1725db5fc95049befb069b55 | 10 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessShallowManagedArtifactReferenceGuard.cs | a4be145ec651f3d6ed24295519b3f36f52240a4956abe9febeadfd31244dea5b | 36 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionBlockerSummaryBuilder.cs | 168aa6b36cb581b2e963a3a7931bf78dd8fc4d0030a9c8786ec39afebbd1dd5c | 20 |
| repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs | e741715cb78300408f3e956c73d36ef33141b1638715f31e85174f49736f163f | 1816 |


## Command Transcripts

- Prepared validator transcript: bundle://proof/shared/transcripts/prepared-validator.txt.
- Passing unit boundary transcript: bundle://proof/shared/transcripts/unit-boundary-test.txt.
- Passing artifact-contract integration transcript: bundle://proof/shared/transcripts/integration-artifact-contract.txt.
- Passing recovery-routing integration transcript: bundle://proof/shared/transcripts/integration-recovery-routing.txt.
- Passing solution build transcript: bundle://proof/shared/transcripts/solution-build.txt.
- Source assertions transcript: bundle://proof/shared/transcripts/source-assertions.txt.
- No-core/no-driver scan transcript: bundle://proof/shared/transcripts/no-core-no-driver-scan.txt.
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt.
- No prohibited viewport proof scan transcript: bundle://proof/shared/transcripts/no-prohibited-viewport-proof-scan.txt.

## Semantic Adequacy Gate

- Raw note owned: preserve behavior while continuing smaller dispatcher isolation steps; no Process Core; no production driver API; viewport proof remained N/A.
- Shipped behavior: artifact satisfaction and evidence-validation decisions are delegated to module-local helpers while dispatcher side-effect orchestration remains in epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs.
- Source proof: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionSnapshot.cs, epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRequiredArtifactAutoSatisfactionRules.cs, epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessQualityValidationEvidenceAggregator.cs, and related helper files listed above.
- Test proof: bundle://proof/shared/transcripts/unit-boundary-test.txt, bundle://proof/shared/transcripts/integration-artifact-contract.txt, and bundle://proof/shared/transcripts/integration-recovery-routing.txt.
- Shallow-pass trap: merely adding helper file names or moving code into helpers while changing artifact branch order would be accepted by weak structure-only tests but rejected by the integration slices and source assertions.
- Adversarial negative proof: N/A - process refactor made no intended behavior change; existing negative integration cases for placeholder artifacts, malformed content, stale or wrong-run artifacts, response-text misuse, and missing required artifacts are rerun as regression proof.
- Failing-first proof: N/A - process refactor with no intended production behavior change; source and regression proof are the acceptance path.
- Semantic positive proof: passing artifact-contract and recovery-routing integration transcripts prove the same required-artifact, response-text, provider-native, external-target, shallow-path, and quality-validation flows still work.
- Anti-stub audit: bundle://proof/shared/transcripts/anti-stub-scan.txt.
- Downstream dependency check: source assertions include SB28-INV-001 and all critical invariant IDs in bundle://proof/shared/transcripts/source-assertions.txt.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| Module-local artifact satisfaction boundary preserves runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs and helper files above | bundle://proof/shared/transcripts/unit-boundary-test.txt plus integration transcripts | Existing artifact-contract negative cases rerun in bundle://proof/shared/transcripts/integration-artifact-contract.txt | Passed |