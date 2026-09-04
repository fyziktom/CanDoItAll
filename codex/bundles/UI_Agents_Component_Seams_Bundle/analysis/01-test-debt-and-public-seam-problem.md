# Test debt and public-seam problem

## Valuable behavior already covered

The current tests protect important outcomes: route fallback, tab behavior, managed-agent
identity checks, confirmations, selection semantics, save/delete behavior, capability
workflows, project/workspace access normalization, storage selection, thinking effort,
auto-approval, and avatar generation.

These behavior assertions should remain.

## Incidental implementation coupling to remove

### AgentCatalogPanelTests

- reads private `openedRequestedAgentId` through reflection;
- invokes private `HandleAgentDialogSavedAsync` through reflection;
- reads private `selectedAgentId` through reflection;
- constructs broad service proxies only because the component owns data and host actions.

Replace these cases with public assertions:

- a card emits a typed `OpenAgentDetails` intent;
- page-owned requested state opens one dialog and suppresses duplicate echo;
- a public delete result causes controller reload and page-owned selection clear.

### AgentDetailsDialog*Tests

The six current test classes repeatedly:

- derive `TestAgentDetailsDialog`;
- reflect private fields such as `editorModel`, `providers`, `capabilities`, `isLoading`,
  and `selectedTabIndex`;
- register `ProjectsService` and `SecretService` through
  `RuntimeHelpers.GetUninitializedObject`;
- create broad DispatchProxy implementations for methods unrelated to the scenario.

Replace this with:

- explicit `AgentEditorSession` input;
- typed `AgentDetailsSection` input;
- a small fake `IAgentEditorController`;
- a shared test harness that renders the real component, not a subclass;
- direct tests of controller workflows and pure state policies.

### WorkflowsPageTests adjacency

One current case uses reflection to locate and invoke private
`AgentsHomePage.OpenWorkflows`. Because this bundle touches the page contract, rewrite the
case to click the public `agents-shell-open-workflows` action and assert the resulting
navigation. Do not preserve private method names.

## Test-count policy

The current primary component slice discovers 46 behavior cases:

- 6 `AgentsHomePageTests`;
- 10 `AgentCatalogPanelTests`;
- 30 cases across six `AgentDetailsDialog*Tests` classes.

The count is an execution baseline only, not a permanent architecture assertion. Replace
private-shape cases one-for-one where the user behavior remains relevant. Add direct unit
coverage only for the new durable state/controller boundaries. Do not create tests merely
to restate every architecture sentence.
