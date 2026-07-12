# SB01 Proof Manifest

- Subbundle ID: SB01
- Status: Completed
- Baseline commit: 5f9d13dc04362442073b4782d544fbb88429af55
- Owned requirements: WF-ARCH-01, WF-ARCH-02, WF-PLUGIN-01
- Semantic invariant contract: bundle://proof/SB01/semantic-invariants.md

## Evidence

- Failing-first transcript: bundle://proof/SB01/transcripts/closure.txt
- Passing transcript: bundle://proof/SB01/transcripts/closure.txt
- Anti-stub transcript: bundle://proof/SB01/transcripts/closure.txt
- Failing-first: bundle://proof/SB01/failing-first.txt
- Passing: bundle://proof/SB01/passing-build.txt
- Passing: bundle://proof/SB01/passing-unit.txt
- Semantic positive proof: bundle://proof/SB01/passing-integration.txt
- Anti-stub: bundle://proof/SB01/anti-stub.txt
- Architecture source/dependency proof: bundle://proof/SB01/architecture-snapshot.txt
- Host test limitation and adversarial comparison: bundle://proof/SB01/validation-limitations.txt

## Named Test Proof

- Test name: WorkflowContractsAreOwnedOnlyByWorkflowAbstractions
- Test name: WorkflowCoreProjectDoesNotReferenceForbiddenImplementationProjects
- Test name: CoreServicesResolveStandardContributionAndCompatibilityAliasWithoutDuplicates
- Test name: CoreServicesRejectDuplicateContributionIds
- Test name: CoreServicesRejectRunnableLegacyDescriptorWithoutImplementation
- Test name: CoreServicesRejectMismatchedLegacyDescriptorAndImplementation
- Test name: DescriptorOnlyContributionPreservesPlannedExecutorWithoutImplementation
- Test name: PluginManifestProjectionPreservesStableContributionMetadata
- Test name: RuntimePackageRegistrationRejectsManifestImplementationCountMismatch
- Test name: RuntimePackageContributionRejectsManifestRuntimeIdMismatch
- Test name: PluginModuleDoesNotRegisterLegacyWorkflowExecutorDescriptorSource

## Changed-File SHA-256

| File | SHA-256 |
|---|---|
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowServiceContracts.cs | 6daffa30e65ed2f0b69893fe732894a0e7da485ee2ac62bc705740ba0d578c65 |
| repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowRuntimeContracts.cs | c99a0a2b6992e5e67b583f0ff9ab2301990f8ee362406692770181ad05d7b82d |
| repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContributionContracts.cs | 2d3cd3b2f7d0f107385a37b910d737c44354904224a8fa2413a66f9d5b8760ac |
| repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowExecutorContributions.cs | e25ab6fdfb5e2287b9f9562eb08f58f2b2665c422a6be0e096a99c42c36d21a4 |
| repo://src/plugins/Implementations/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutorDescriptors.cs | e820d7557e904d9cd646896c5c90cf706ed0311d1d6b167ec83f15c9c68581ee |
| repo://src/plugins/Implementations/CanDoItAll.Plugin.Office365/Office365WorkflowExecutorDescriptors.cs | fffb75fcb681d2ec4bd9809affaecbe803b40e55ade8b8866e29937540e38107 |
| repo://src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerWorkflowExecutorDescriptors.cs | 318d5aef3aeca1a3eec3fe4eb7717b4797627829c7ff1df55ec10940cc4234f7 |

## Result

- SB01-CONTRACT-OWNERSHIP is satisfied by failing-first extraction assertions, consolidated source ownership, a clean full build, and the no-new-cycle focused snapshot.
- SB01-CONTRIBUTION-IDENTITY is satisfied by real DI contribution tests, duplicate/missing/mismatch rejection tests, and the anti-stub audit.
- SB01-PLUGIN-DESCRIPTOR-PARITY is satisfied by manifest projection/runtime mismatch tests, real email plugin integration tests, and bundled plugin source inspection.
- Known test-host startup hangs are isolated in bundle://proof/SB01/validation-limitations.txt; they reproduce in a pre-existing API-host test and do not invalidate the passing non-hosted semantic gates.
