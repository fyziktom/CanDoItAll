# Test impact and classification

## Primary baseline

| Test class/filter | Baseline discovered cases | Classification |
|---|---:|---|
| `AgentsHomePageTests` | 6 | keep behavior; adapt service setup to new seams |
| `AgentCatalogPanelTests` | 10 | keep 8 direct behaviors; rewrite 2 private-shape cases |
| `AgentDetailsDialogDeletionTests` | 5 | keep behavior; rewrite harness |
| `AgentDetailsDialogCapabilityTests` | 3 | keep behavior; rewrite harness/typed section |
| `AgentDetailsDialogThinkingEffortTests` | 6 | keep behavior; rewrite harness/typed section |
| `AgentDetailsDialogAvatarGenerationTests` | 2 | keep behavior; rewrite harness |
| `AgentDetailsDialogProjectStructureAccessTests` | 2 | keep behavior; rewrite harness |
| `AgentDetailsDialogSettingsTests` | 12 | keep behavior; rewrite harness/typed sections |
| **Primary component total** | **46** | temporary execution baseline, not a permanent count assertion |
| `AgentFrameworkSimpleChatsRouteTests` | 10 | keep unchanged behavior; adapt typed mapping as needed |

## Tests to rewrite explicitly

### AgentCatalogPanelTests

- replace private-field `openedRequestedAgentId` assertion with public intent + page
  open-once behavior;
- replace private `HandleAgentDialogSavedAsync` invocation/private selected ID read with a
  page/controller result flow assertion.

### Six AgentDetailsDialog test classes

- remove all `TestAgentDetailsDialog` subclasses;
- remove `BindingFlags.NonPublic`, `GetField`, and private state mutation;
- remove raw numeric `selectedTabIndex` setup;
- remove `RuntimeHelpers.GetUninitializedObject` for Projects/Secrets;
- use one shared `AgentDetailsDialogTestHarness`, `AgentEditorSession`, typed section, and
  fake controller.

### WorkflowsPageTests adjacent case

Rewrite the case that reflects private `AgentsHomePage.OpenWorkflows` so it clicks
`agents-shell-open-workflows` and asserts navigation. The rest of the large workflow test
class is out of scope.

## Tests to add

Add a small direct Unit slice totaling 18 planned cases across:

- `AgentsWorkspaceStateTests` — 3;
- `AgentsOverviewQueryTests` — 3;
- `AgentCatalogControllerTests` — 4;
- `AgentEditorControllerTests` — 5;
- `AgentUiDependencyBoundaryTests` — 3.

If a theory changes discovery, SB01/SB02 must update the expected count before execution
and explain why. Do not add source-shape tests.

## Temporary migration proof

Use proof transcripts/checks, not permanent product tests, to show:

- moved service calls are absent from Razor;
- private-reflection patterns are absent from target tests;
- DI registrations resolve the three seams;
- no new partial/project reference was added.
