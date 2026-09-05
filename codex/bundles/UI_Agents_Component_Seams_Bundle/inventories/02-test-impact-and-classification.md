# Test impact and scenario ownership

## Historical baseline, to rediscover at SB01

| Class / case | Observed cases | Treatment |
|---|---:|---|
| AgentsHomePageTests | 6 | Preserve behavior; migrate service setup and context/selection composition |
| AgentCatalogPanelTests | 10 | Includes two AgentSelectionCard cases; preserve legitimate behavior and replace private catalog assertions |
| AgentDetailsDialogDeletionTests | 5 | Preserve delete/confirmation/result behavior; public harness |
| AgentDetailsDialogCapabilityTests | 3 | Preserve existing/new capability commit semantics |
| AgentDetailsDialogThinkingEffortTests | 6 | Typed section and public controls |
| AgentDetailsDialogAvatarGenerationTests | 2 | Real avatar child and fake gateway |
| AgentDetailsDialogProjectStructureAccessTests | 2 | Preserve permission granularity |
| AgentDetailsDialogSettingsTests | 12 | Preserve all covered settings/normalization |
| Primary component baseline | 46 | Historical inventory only, not acceptance quota |
| AgentFrameworkSimpleChatsRouteTests | 10 | Current codec compatibility |
| ProviderAdministrationLayoutTests.History_hosts_make_no_aggregate_or_history_reads_until_requested | 2 | Explicitly add Providers and RequestHistory host regressions to focused scope |
| WorkflowsPageTests.Agents_shell_exposes_workflows_page_navigation | 1 | Replace private OpenWorkflows reflection with public agents-shell-open-workflows click |

All classes above live in tests/Components/CanDoItAll.Tests.Components except route tests in tests/Unit/CanDoItAll.Tests.Unit. Component namespaces use CanDoItAll.Tests.Components.AgentFramework; route namespace is CanDoItAll.Tests.Unit.AgentFramework. Verify exact discovery, including theory cases, before execution.

## Additional existing evidence to map, not overlook

- AgentFrameworkModuleChatContextBuilderTests: Request_history_context_has_no_invented_summary_or_inherited_agent_selection; Agents_builder_exposes_only_selections_relevant_to_the_active_view; Agents_builder_uses_validated_component_selection_labels. Select these for moved Agents context ownership; other Workflows cases require separate impact justification.
- ExternalWorkspaceRootSelectionFieldTests: alias/binding round-trip, invalid relative path, removal, unresolved alias and duplicate validation.
- SharedProviderRefreshButtonTests.Refresh_preserves_selected_imports_and_notifies_only_on_success (success/failure).
- AgentMemorySettingsPanelTests, AgentMemorySettingsPanelOrderingTests and AgentMemorySettingsPanelTestBase: actual memory choices/order/error/binding behavior.
- StorageCatalogSelectionComponentsTests and EmptyStorageCatalogSelectionSource: existing storage child scenarios and fixture.

Freeze exact methods/data cases needed from descendant classes at SB01/SB04; do not run entire unrelated classes solely because names are adjacent. Existing child unit/component tests supplement an editor integration scenario; they do not prove parent wiring.

## Progressive migration

Update AgentsHomePageTestExtensions with page seams. Replace private catalog openedRequestedAgentId/HandleAgentDialogSavedAsync checks with real page intent/result behavior. Replace details TestAgentDetailsDialog subclasses, numeric selectedTabIndex seeding, BindingFlags.NonPublic and RuntimeHelpers.GetUninitializedObject with real production components and deterministic operations. Consolidate meaningful common setup; do not create a test-only production session path.

Migrate each test with its owning seam in SB02–SB05. SB06 audits all touched helpers plus the Workflows navigation case. Keep existing assertions that protect user behavior; discard only private shape assumptions with recorded replacement coverage.

## New proof by responsibility

Add named cases for workspace transitions/lazy query regions, real catalog/editor operations, normal constructor composition, subtree interaction, save/conflict/partial refresh, per-editor session and stale completions, and dependency direction. Use the behavior matrix B01–B30 to name scenarios before implementation, then map actual fully qualified test methods and data cases.

There is no fixed new-test budget. Before each phase runs tests, record exact selectors, expected names/data cases and derived count; reconcile discovery differences before accepting execution. A zero-match filter is failure. Case counts may change when valid behaviors move or parameterize; the behavior map must remain complete.
