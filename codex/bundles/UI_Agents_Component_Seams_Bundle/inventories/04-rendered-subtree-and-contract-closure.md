# Rendered subtree, contracts and extraction closure

This is the minimum observed inventory, not a completed extraction proof. SB01/SB04 refresh it from real markup, conditional sections, event handlers, nested dialogs, injections and service constructors. Each executed scenario records actual registrations/calls/assets and a disposition: isolated now, existing fakeable boundary, or named follow-up blocker.

Paths are repository-relative unless marked sibling. Tests must render real owned descendants; suppressing a child is valid only for an explicitly narrower scenario, never proof of that child's isolation.

| Surface / source | Hidden or transitive dependency | Scenario and intended disposition |
|---|---|---|
| AgentCatalogPanel and MAF/Common/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor | Model graph, selected-card rendering, host callbacks and UI assets | Catalog can emit host intents without importing real editor/team hosts into a catalog-only sandbox; audit both variants |
| AgentDetailsDialog real ten-section body | All conditional children, cascading dialog and render mode | Typed section must trigger real content and I/O; zero parent injection count is insufficient |
| AgentFramework/Pages/Components/ExternalWorkspaceRootSelectionField.razor.cs | IExternalTargetPathRegistryFactory created during parameter lifecycle; infrastructure types | Add/validate/remove/persist roots. Same-module seam may be needed; inventory both alias and binding data |
| Workspace/Pages/Components/StorageCatalogSelectionField.razor.cs and StorageCatalogSelectionDialog | IStorageCatalogSelectionSource and DialogService; saved names and nested selection | Existing source can be faked. Exercise nested dialog; cross-module assembly edge remains follow-up unless separately scoped |
| AgentFramework/Pages/Components/SharedProviderRefreshButton.razor | ISharedProviderManagementService ListSources/SynchronizeSource | Real refresh button, success/failure, parent reload without erasing draft; existing service boundary |
| MAF/Common/CanDoItAll.AgentFramework.Components/AvatarPicker.razor | IAvatarGenerationGateway, notifications, declarative Dialog and CSS | Real picker, generate/select, cancel/error; use existing gateway fake |
| AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs | Workspace, IAgentCapabilitySetupFlowService, notification and setup rendering | Wizard creation vs assignment/save are separate effects; preserve current commit semantics and audit nested content |
| AgentFramework/Pages/Components/AgentMemorySettingsPanel.razor and AgentMemorySettingsPanelState.cs | IMemoryProviderProfileStore, IEnumerable<IMemoryProviderDriver>, logging, Memory.Application/Abstractions models | Healthy/enabled/capable provider choices, deterministic ordering, aliases/settings/errors; real child with registered fake store/drivers |
| MAF provider/model selector family | ProviderProfile and shared conversation selector UI | Actual supported model/effort choices and invalid selection behavior; inspect full nested type/asset graph |
| BaseLib Tabs (sibling Components) | SelectedIndex and existing server render behavior | Map enum to index; no sibling change required for semantic section |
| BaseLib Modals/DialogService.cs (sibling Components) | LocationChanged closes all; parameters copied in DialogReference | Existing host semantics now; later route/session retention has explicit host solution |
| AgentEditorModel in MAF Common Models/Editors/EditorModels.cs | Mutable nested settings, ExpectedUpdatedAtUtc | Instance ownership/copy/version; not an immutable shared snapshot |
| Projects/ProjectModels.cs ProjectAccessListItem | Projects implementation assembly and its service graph | Use only required read metadata through justified projection or later owned lightweight contract |
| Security/SecurityModels.cs SecretListItem | Security implementation assembly and secret services | UI identity/label/access metadata only; never raw secret values |
| ProviderProfile and other reused models | Nested settings/configuration and credential binding metadata | Audit sensitivity and complete defining assembly graph before extraction |
| Web App.razor, module assets and Directory.Build.targets | InteractiveServer composition, CSS isolation, JS, Templates, package-to-sibling substitution | Evaluate real references and assets for production and proposed sandbox; references on paper are not watch proof |

For every new operation, also record its constructor graph and which concrete adapter integration remains. Faking a top-level controller does not prove that a production ProjectsService/SecretService-backed adapter works.

## Closure fields per candidate

Record candidate root/scenarios; every child and on-demand dialog; public type owners; direct/evaluated transitive references; static assets and render mode; external capabilities/fakes; production registration; behavior coverage; remaining blocker and owner; measured evidence.

Required Agents behavior cannot be deferred merely to claim sandbox progress. However, a catalog-only sandbox may legitimately exclude editor workflows and record only open/edit intents, while the production Agents host still proves those workflows elsewhere. Start small and label the scenario honestly.
