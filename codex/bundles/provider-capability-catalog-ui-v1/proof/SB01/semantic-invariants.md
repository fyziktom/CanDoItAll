# SB01 Semantic Invariants

## Invariant SB01-PROVIDER-PARITY-TAGS

- Invariant ID: `SB01-PROVIDER-PARITY-TAGS`
- Source raw note: `N01` and `N03`
- Expected behavior: The Agents provider tab renders provider rows from the AgentFramework provider catalog, groups them by durable tags, and lets users edit provider tags through the same catalog metadata path.
- Disallowed shallow implementation: Updating only a visible badge, or rendering the old Workspace provider list beside an AgentFramework badge, would still allow count/list divergence.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-provider-tab.txt` proves the old source audit could not find `AgentProviderProfilesPanel`.
- Passing test: `bundle://proof/SB01/transcripts/provider-tests-and-build.txt` proves `Providers_tab_renders_agentframework_provider_tree_with_seeded_local_ollama`.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderProfileTreeNodeBuilder.cs`, and provider model/metadata files.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` cites `AgentProviderProfilesPanel`, `TreeView`, `TagEditor`, and provider tag metadata.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` verifies no hard-coded provider count and no stubbed provider save path.
- Downstream dependency check: `bundle://proof/SB01/transcripts/provider-tests-and-build.txt` proves the AgentFramework module, component-test project, and integration-test project build.

## Invariant SB01-LOCAL-OLLAMA-OPENAI-DEFAULT

- Invariant ID: `SB01-LOCAL-OLLAMA-OPENAI-DEFAULT`
- Source raw note: `N02`
- Expected behavior: Clean development seeding includes a local Ollama provider and the default OpenAI model is `gpt-5.4-mini`.
- Disallowed shallow implementation: Showing `Local Ollama` only as UI text or adding `gpt-5.4-mini` only to a dropdown without changing default seed/fallback contracts.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-provider-tab.txt` records the previous missing local provider/default source assertions.
- Passing test: `bundle://proof/SB01/transcripts/provider-tests-and-build.txt` proves `Organization_workspace_seeds_tagged_openai_and_local_ollama_provider_catalog`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`, `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs`, and `repo://src/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` cites `Local Ollama` and `gpt-5.4-mini`.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` verifies no hard-coded browser-only provider count or placeholder implementation.
- Downstream dependency check: `bundle://proof/SB01/transcripts/provider-tests-and-build.txt` proves provider seed changes compile and tests pass.
