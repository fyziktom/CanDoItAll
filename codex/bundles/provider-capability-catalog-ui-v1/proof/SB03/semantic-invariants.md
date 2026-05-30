# SB03 Semantic Invariants

## Invariant SB03-MCP-SKILL-WIZARD

- Invariant ID: `SB03-MCP-SKILL-WIZARD`
- Source raw note: `N07`, `N08`, and `N10`
- Expected behavior: The capability page opens a multi-step wizard for MCP servers and Skills using the existing `Steps` component, accepts tags, supports MCP parameters and Skill setup modes, and saves catalog capability editor output through the existing service path.
- Disallowed shallow implementation: A one-screen static dialog or visual-only proposal without catalog save output would not let users add MCP servers or Skills.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-wizard.txt` records that the old source did not contain `CapabilitySetupWizardDialog`.
- Passing test: `bundle://proof/SB03/transcripts/wizard-build-and-browser.txt` proves Web build and running MCP wizard configure-step layout.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.css`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor`, and `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt` cites `Steps`, `InputFile`, New MCP/New skill launch buttons, and catalog save output.
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub-audit.txt` verifies no static-only wizard placeholder or stubbed save path.
- Downstream dependency check: `bundle://proof/SB03/transcripts/wizard-build-and-browser.txt` proves the Web project builds after scoped CSS and wizard integration.

## Invariant SB03-NO-CHAT-TAG-SHORTCUT

- Invariant ID: `SB03-NO-CHAT-TAG-SHORTCUT`
- Source raw note: `N12`
- Expected behavior: Capability tags are persisted for future chat-time selection, but no skills-tag parser or chat behavior is added in this repair.
- Disallowed shallow implementation: Adding a partial chat shortcut parser without request coverage or tests would expand scope and risk runtime regressions.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-wizard.txt` records the old missing wizard while preserving the no-chat-shortcut boundary.
- Passing test: `bundle://proof/SB03/transcripts/source-assertions.txt` records no skills-tag source additions.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs`, and `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt` cites no chat shortcut/parser changes and confirms tag persistence stays in catalog UI.
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub-audit.txt` verifies no skills-tag parser/runtime behavior was introduced.
- Downstream dependency check: `bundle://proof/SB03/transcripts/wizard-build-and-browser.txt` proves the Web project builds after wizard integration.
