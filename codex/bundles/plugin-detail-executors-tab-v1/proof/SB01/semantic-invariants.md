# SB01 Semantic Invariants

## Invariant SB01-DYNAMIC-DESCRIPTOR

- Invariant ID: `SB01-DYNAMIC-DESCRIPTOR`
- Source raw note: `N001`, `N002`, `N003`, and `N004`
- Expected behavior: The selected plugin detail renders an `Executors` tab whose rows come from `selectedPlugin.Descriptor.WorkflowExecutors`, and each row shows descriptor-owned name, executor id, category, and description.
- Disallowed shallow implementation: A UI that hard-codes Office365, Gmail, Docker, or any other known plugin executor text would pass for one fixture but fail the request because each plugin must carry its own executor metadata.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-old-page-no-executors-tab.txt` proves the old page did not expose `plugins-tab-executors`.
- Passing test: `bundle://proof/SB01/transcripts/component-tests-plugins-page.txt` proves the descriptor-backed executor rows render, and `bundle://proof/SB01/transcripts/browser-proof.txt` proves the tab is readable in the app.
- Changed source files: `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor`, `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs`, and `repo://tests/CanDoItAll.Tests.Components/PluginsPageTests.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` cites `selectedPlugin.Descriptor.WorkflowExecutors`, `plugins-tab-executors`, and `BuildExecutorRowTestId`.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` verifies changed production UI files do not contain hard-coded plugin executor names or `TODO`/`NotImplemented` stubs.
- Downstream dependency check: `bundle://proof/SB01/transcripts/plugin-module-build.txt` confirms the Plugins module compiles after the UI/helper changes.

## Invariant SB01-NO-EXECUTOR-EMPTY-STATE

- Invariant ID: `SB01-NO-EXECUTOR-EMPTY-STATE`
- Source raw note: `N002` and `N004`
- Expected behavior: A plugin descriptor with no workflow executors renders a no-executors empty state instead of blank content.
- Disallowed shallow implementation: Rendering a static executor section regardless of descriptor contents, or leaving the tab empty when the descriptor has no workflow executors.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-old-page-no-executors-tab.txt` proves the old page had no executor tab at all.
- Passing test: `bundle://proof/SB01/transcripts/component-tests-plugins-page.txt` covers `Plugins_page_shows_empty_executor_state_for_plugins_without_workflow_executors`.
- Changed source files: `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor`, `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs`, and `repo://tests/CanDoItAll.Tests.Components/PluginsPageTests.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` cites the no-executors empty state and descriptor row rendering hooks.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` verifies there are no hard-coded plugin-specific executor rows in changed production UI files.
- Downstream dependency check: `bundle://proof/SB01/transcripts/plugin-module-build.txt` confirms the Plugins module compiles after the empty-state and helper changes.
