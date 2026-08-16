# Findings register

## F-001 — ChatWorkspacePanel mixes reusable conversation presentation with agent execution concerns

- Severity: **critical**
- Impact: Moving or reusing the component directly would pull AgentDefinition, execution records, approvals, voice, attachments, and agent policy into Simple Chat UI.
- Evidence:
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatMarkdownRenderer.cs`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatPromptTextArea.razor`

## F-002 — AgentChatPanel owns presentation, workspace state, backend orchestration, execution, voice, and attachment concerns

- Severity: **critical**
- Impact: A safe extraction must create independent presentation contracts and adapters rather than add more partial files or source switches.
- Evidence:
  - `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
  - `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`

## F-003 — Participant listing components expose AgentDefinition directly

- Severity: **high**
- Impact: Adding Simple Chat conditionals would create a source-switch component instead of a reusable participant surface.
- Evidence:
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactList.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactListItem.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSwitchDialog.razor`

## F-004 — AgentDetailsDialog combines reusable identity/runtime fields with many agent-only policies

- Severity: **high**
- Impact: Only field groups and editor chrome should be extracted; Memory, tools, capabilities, approvals, workload, history, and governance remain agent-owned.
- Evidence:
  - `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThinkingEffortSettings.razor`

## F-005 — FloatingAgentChatHost is an agent-only coordinator and catalog

- Severity: **high**
- Impact: Only presentation and lifecycle-field seams should move in Phase 1. Coordinator, context, handles, preparation, and history remain agent-owned.
- Evidence:
  - `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor`
  - `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor.cs`
  - `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatSettingsPanel.razor`

## F-006 — ChatWorkspacePanel has consumers outside the main AgentFramework page

- Severity: **high**
- Impact: Compatibility cannot be judged only on AgentsHomePage; Process and contextual workspace consumers are part of the regression surface.
- Evidence:
  - `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
  - `src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`

## F-007 — AppComponents is neutral but broad; a focused Conversation Components project is a justified boundary

- Severity: **medium**
- Impact: A focused neutral Razor project prevents MAF components from acquiring the wider AppComponents/FileTools dependency set and creates an independent test boundary.
- Evidence:
  - `src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
  - `src/UI/CanDoItAll.AppComponents/README.md`
  - `src/UI/README.md`
  - `docs/architecture/overview.md`

## F-008 — Existing bUnit coverage provides owner-test anchors but test selection must come from the real diff

- Severity: **medium**
- Impact: The bundle must name likely owners as context but defer authoritative selectors to code_analytics_impacted_tests_get.
- Evidence:
  - `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
  - `tests/Solutions/CanDoItAll.Tests.Components.slnx`
  - `tests/Components/CanDoItAll.Tests.Components/ChatWorkspacePanelTests.cs`
  - `tests/Components/CanDoItAll.Tests.Components/AgentChatPanelResponsivenessTests.cs`
  - `tests/Components/CanDoItAll.Tests.Components/AgentCatalogPanelTests.cs`
  - `tests/Components/CanDoItAll.Tests.Components/AgentCompactListTests.cs`

## F-009 — SharedInfo now requires diff- and line-range-based impacted-test selection

- Severity: **high**
- Impact: Broad test loops and zero-discovery filters are invalid proof. Each production-changing subbundle needs a fresh impacted-test request.
- Evidence:
  - `fyziktom/CanDoItAll.SharedInfo@7b7808e8591d7219f40826cf0e5624e182981d90`
  - `codex/skills/candoitall-codeanalytics-mcp/SKILL.md`

## F-010 — Simple Chat backend already has the future identity/runtime overlap but is a non-Razor product module

- Severity: **high**
- Impact: Phase 1 may shape neutral labels and slots around the overlap, but any direct type reference or product activation would collapse the intended phase boundary.
- Evidence:
  - `src/Modules/CanDoItAll.Modules.LlmChats/Definitions/LlmChatDefinition.cs`
  - `src/Modules/CanDoItAll.Modules.LlmChats/Definitions/LlmChatDefinitionRevision.cs`
  - `src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj`

## F-011 — Visual and automation selectors are an effective compatibility contract

- Severity: **high**
- Impact: Internal markup may be reorganized only with explicit parity evidence for accessible names, stable selectors, visual hierarchy, scroll, and overlays.
- Evidence:
  - `data-testid usage across AgentChatPanel, AgentSelectionCard, AgentCompactList, AgentDetailsDialog, FloatingAgentChatHost`
  - `bUnit and Playwright owner tests`

## F-012 — The easiest implementation path would create a generic component with agent/simple switches

- Severity: **high**
- Impact: The bundle requires typed projections, adapters, and focused slots and rejects kind switches, service location, and boolean explosion.
- Evidence:
  - `Direct AgentDefinition parameters`
  - `Agent-only execution and settings fields embedded in current components`
