# SB06 semantic changed-file inventory

State: `PASS` for the focused product/test delta.

This inventory compares current file hashes with the completed SB05 changed-file manifest. The
cumulative worktree already contains SB00-SB05 and will be captured separately by the final generated
`changed-files.md` after all SB06/root evidence edits settle.

## Composition

- `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `src/App/CanDoItAll.Composition/SharedProviderRuntimeAccessContextHandler.cs`
- `src/App/CanDoItAll.Composition/SharedProviderRuntimeHttpClientSelector.cs`

## Connector-neutral AgentFramework and MAF seams

- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationCache.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/AgentExecutionPreparationService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeFailureBoundaryExceptions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderDispatchModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Diagnostics/ProviderFailureDisclosurePolicy.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/ConcreteProviderDriverRegistration.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/IProviderHttpClientSelector.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/ProviderDriverProtocol.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Runtime/ProviderRuntimeContracts.cs`

## Outer Workspace and AgentFramework adapters

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentVoiceSettingsPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderRuntimeGateway.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/Credentials/SecretStoreAgentProviderCredentialResolver.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderCatalogProjectionCommitObserver.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderConnectorManifestSource.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderProfileOwnershipPolicy.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderReconciliationCoordinator.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs`

## Focused tests

- `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderHybridSelectionTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderRuntimeProjectionIntegrationTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionPreparationServiceTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ConcreteProviderDriverTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentProviderCredentialDispatchScopeTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ConnectorPluginRegistryTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafProviderTransportBoundaryChatClientTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowExecutorFailureDiagnosticsTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProviderCatalogProjectionFailureTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRuntimeProfileMaterializerTests.cs`

No project file, migration, Web route, new Razor component, UI resource, Docker topology, or broad-
test surface was added by SB06. The existing voice settings component received only the fail-closed
source-managed provider eligibility guard. Existing project references were sufficient.

## August 25 downstream overlay

SB07 subsequently changed `WorkspaceModels.cs` to preserve a first-save structured-output default
without allowing an existing or unsupported profile to widen its capability, plus SB04-owned relay
policy/application files. Those changes add no project/reference edge or alternate runtime. The
current Release materializer, runtime-projection, and hybrid-selection lanes pass 18/18, 16/16, and
10/10 against that overlay; see
`architecture/sb04-downstream-invalidation-release-revalidation.md`.
