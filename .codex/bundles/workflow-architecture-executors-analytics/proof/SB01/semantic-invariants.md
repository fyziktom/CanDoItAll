# SB01 Semantic Invariants

## Contract ownership and dependency direction

- Invariant ID: SB01-CONTRACT-OWNERSHIP
- Source raw note: improve workflow architecture, testability, and flexibility using the C# architecture skills.
- Expected behavior: active workflow catalog, runtime, and store contracts are owned by Workflows.Abstractions; Core and Runtime implement inward-facing contracts; Workflows.Core has no project reference to Workflows.Runtime.
- Disallowed shallow implementation: copy interfaces into Abstractions while leaving active duplicate contracts, consumers, or the Core-to-Runtime reference in place.
- Failing-first test: WorkflowContractsAreOwnedOnlyByWorkflowAbstractions, WorkflowCoreProjectDoesNotReferenceForbiddenImplementationProjects, and runtime registration assertions failed before the migration; see bundle://proof/SB01/failing-first.txt.
- Passing test: the extraction, hosting, foundation, voice, catalog, and runtime tests pass in bundle://proof/SB01/passing-unit.txt.
- Changed source files: repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowServiceContracts.cs, repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowRuntimeContracts.cs, and repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/CanDoItAll.AgentFramework.Workflows.Core.csproj.
- Production assertions: the Web/API, voice, scheduler, workbench, workflow core/runtime/MAF adapter, and persistence consumers compile against the consolidated abstractions in bundle://proof/SB01/passing-build.txt.
- Red-team negative case: tests assert the deleted legacy contract paths remain absent and reject a Core project reference to Runtime; the source audit is in bundle://proof/SB01/anti-stub.txt.
- Downstream dependency check: SB02, SB04, and SB05 can depend on one active contract identity; the focused dependency snapshot is bundle://proof/SB01/architecture-snapshot.txt.

## Executor contribution identity

- Invariant ID: SB01-CONTRIBUTION-IDENTITY
- Source raw note: improve executor architecture for testability and flexibility without creating parallel descriptor and implementation mechanisms.
- Expected behavior: one scoped contribution supplies a descriptor and optional implementation to both catalog and invocation; standard executors preserve scoped resolution; planned entries remain visible without a runnable adapter.
- Disallowed shallow implementation: make the catalog read contributions while invocation still indexes an independent executor list, or synthesize a throwing executor for planned entries.
- Failing-first test: the prior architecture lacked an enforceable contribution identity; negative tests now cover duplicate IDs, duplicate legacy aliases, missing runnable implementations, and descriptor/implementation drift, with the boundary failing-first transcript at bundle://proof/SB01/failing-first.txt.
- Passing test: CoreServicesResolveStandardContributionAndCompatibilityAliasWithoutDuplicates and its duplicate, missing, mismatch, and planned-entry peers pass in bundle://proof/SB01/passing-unit.txt.
- Changed source files: repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContributionContracts.cs and repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowExecutorContributions.cs.
- Production assertions: all seven standard categories register contributions through the shared extension, and the catalog and invoker consume the same validated scoped set.
- Red-team negative case: CoreServicesRejectDuplicateContributionIds, CoreServicesRejectRunnableLegacyDescriptorWithoutImplementation, and CoreServicesRejectMismatchedLegacyDescriptorAndImplementation reject shallow or inconsistent registrations.
- Downstream dependency check: SB02 and SB03 can add adapters through the same contribution seam; SB06 can trust runnable catalog metadata after bundle://proof/SB01/architecture-snapshot.txt and bundle://proof/SB01/anti-stub.txt.

## Plugin descriptor parity

- Invariant ID: SB01-PLUGIN-DESCRIPTOR-PARITY
- Source raw note: plugins add executors too, so their executor architecture and settings metadata must use the improved model.
- Expected behavior: bundled and runtime-package manifest metadata is projected from authoritative executor definitions, preserving defaults, schema, simulation, policy, source, and trust metadata while validating manifest/runtime identity.
- Disallowed shallow implementation: retain independent manifest copies with empty defaults, fabricate descriptors only in tests, or replace the inner plugin descriptor beyond its package source.
- Failing-first test: manifest implementation-count and runtime-ID mismatch tests are adversarial fixtures for the prior drift-prone boundary; the contract migration failure is recorded at bundle://proof/SB01/failing-first.txt.
- Passing test: PluginManifestProjectionPreservesStableContributionMetadata, RuntimePackageRegistrationRejectsManifestImplementationCountMismatch, RuntimePackageContributionRejectsManifestRuntimeIdMismatch, and the real email plugin suite pass in bundle://proof/SB01/passing-unit.txt and bundle://proof/SB01/passing-integration.txt.
- Changed source files: repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/PluginWorkflowExecutorRuntimeRegistration.cs, repo://src/plugins/Implementations/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutorDescriptors.cs, repo://src/plugins/Implementations/CanDoItAll.Plugin.Office365/Office365WorkflowExecutorDescriptors.cs, and repo://src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerWorkflowExecutorDescriptors.cs.
- Production assertions: Gmail, Office365, and Docker register through AddWorkflowExecutorContribution; bundled manifests project those definitions; runtime packages preserve definition metadata and replace only Source.
- Red-team negative case: count mismatch and runtime-ID mismatch produce typed PluginWorkflowExecutorActivationException diagnostics rather than partial activation or silent fallback.
- Downstream dependency check: SB03 can add plugin nodes without a second descriptor truth and SB06 can consume plugin settings schema/renderer metadata from the catalog after bundle://proof/SB01/anti-stub.txt.
