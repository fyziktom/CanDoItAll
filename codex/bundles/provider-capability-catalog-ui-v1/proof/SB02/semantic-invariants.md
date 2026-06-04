# SB02 Semantic Invariants

## Invariant SB02-CAPABILITY-TREE-FILTERS

- Invariant ID: `SB02-CAPABILITY-TREE-FILTERS`
- Source raw note: `N04`, `N05`, and `N06`
- Expected behavior: The capability tab uses the agent tree as the selection surface and filters a compact desktop card grid by search text, tags, assignment state, and capability type.
- Disallowed shallow implementation: Rendering a static list with unused filter controls would look close but would not let users find and assign capabilities by catalog metadata.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-capability-panel.txt` proves the old source audit could not find the required tree/filter/details surface.
- Passing test: `bundle://proof/SB02/transcripts/browser-proof.txt` proves the tree, filters, and multiple cards per row in the running app.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.cs`, and `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.css`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt` cites `TreeView`, `TagEditor`, assignment and type filters, and card grid CSS.
- Red-team negative case: `bundle://proof/SB02/transcripts/anti-stub-audit.txt` verifies no stubbed filter handler or fake-only card data path.
- Downstream dependency check: `bundle://proof/SB02/transcripts/capability-tests-and-build.txt` proves component and integration projects build after the filter/grid changes.

## Invariant SB02-CAPABILITY-DETAILS-TAGS-CONFIG

- Invariant ID: `SB02-CAPABILITY-DETAILS-TAGS-CONFIG`
- Source raw note: `N09`, `N10`, and `N11`
- Expected behavior: Each capability can open a details dialog; tags are editable for built-in tools, and MCP/Skill capability configuration is edited through typed fields that round-trip to catalog JSON.
- Disallowed shallow implementation: A read-only information modal, or a raw JSON textbox without typed MCP/Skill fields, would not meet the editability requirement.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-capability-panel.txt` records that the old source did not contain `CapabilityDetailsDialog`.
- Passing test: `bundle://proof/SB02/transcripts/capability-tests-and-build.txt` proves `Capability_catalog_save_normalizes_and_persists_tags`.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityDetailsDialog.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityDetailsDialog.razor.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityDetailsDialog.razor.css`, and `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityConfigurationEditorSupport.cs`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt` cites the details dialog, `TagEditor`, and typed MCP/Skill configuration support.
- Red-team negative case: `bundle://proof/SB02/transcripts/anti-stub-audit.txt` verifies no disabled-only dialog or stubbed save implementation.
- Downstream dependency check: `bundle://proof/SB02/transcripts/capability-tests-and-build.txt` proves the Web, component-test, and integration-test build targets pass.
