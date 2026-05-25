# SB04 Semantic Invariants

- Invariant ID: `WEB-SB04-001`
- Source raw note: `REQ-WF-001`.
- Expected behavior: Workflows page navigation avoids example-catalog seeding and component/provider listing until the user opens a section or command that needs the component library.
- Disallowed shallow implementation: Hiding component counts while still loading all component/provider data during page initialization, or rendering editor/template commands without loading their required data first.
- Failing-first test: N/A process because the reported failure was page-load latency; `bundle://proof/SB04/transcripts/negative-probe.md` guards against returning the page-init seeding call.
- Passing test: `Workflows_page_defers_component_library_until_component_sections_need_it` proves zero component/provider list calls on initial load and one lazy load when the editor section is opened.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`, and `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`.
- Production assertions: Starter workflow creation and editor/template sections call the explicit load gate before using component/provider data.
- Red-team negative case: The counting decorator would fail the test if initial navigation called `ListComponentsAsync` or `ListProviderOptionsAsync`.
- Downstream dependency check: SB05 runs workflow regression tests for starter workflow creation and canvas save behavior after the lazy gate change.
