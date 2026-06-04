# SB01 Proof Manifest

## Summary

- Subbundle: `SB01`
- Status: `Completed`
- Scope: Provider catalog parity, provider tags, local Ollama seed, and default OpenAI model.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor` | `514D4C4501A4DB5AAB22B4438B20286A798D6FBFF6CD4775D4F91606D703AB07` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor` | `94A0D24D638A42938B2664FF9AC58702295D98943762416EAC455E581D85B747` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs` | `B00271541E3EE67029A918EB538AD20C608343EAF020C02820F9B8AFE4665903` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderProfileTreeNodeBuilder.cs` | `5B45DD2B1170D66FB9259C294EE8A9BCF8EA2EC3DC6AAFEC28FB85E09B285A6C` |
| `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs` | `7FBCC1AFF41AD99BCDF538D599DCFBCDEB5F8C9BFA1B3F250D7F3E925FDA4C5A` |
| `repo://src/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs` | `A4CADC5E86FF6F1535BBF8731CDC4C3CD359469C3230CB2E3E1AF3AE5270EAFD` |
| `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` | `1C6DD474243232BE911E6F0E64295FB85055D7D353220FEA9CE2A676FFBCF4CF` |
| `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs` | `941AE1D19ECB45050EA6431E68E91EB7E29D4AA227C474B58E4B44D504F6411C` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderMetadata.cs` | `307026945C0F61D0109835E19599731983AA44B08648C7625AF8C1495B0F139A` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs` | `9546F856C3A1EC490BA00A447DF9CD0E51C7AE80273502EB18A3DB99F01EA83E` |
| `repo://tests/CanDoItAll.Tests.Components/AiAgentsPageTests.cs` | `C17FC7838EB841DF0244B49D3EEB9E2B5ADE15861101AF3906A21A0F81C8ECB6` |
| `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | `94FC3DEEEB255B9D9E168B834E758109667F602DA2C8960FAD00FD585655340B` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-provider-tab.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/provider-tests-and-build.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Browser proof transcript: `bundle://proof/SB01/transcripts/browser-proof.txt`

## Source Assertions

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor` renders `AgentProviderProfilesPanel` for the provider tab.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor` uses `TreeView` and `TagEditor`.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` seeds `Local Ollama`.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs` sets `OpenAiDefaultModel` to `gpt-5.4-mini`.

## Semantic Adequacy

- Raw note owned: `N01-N03`.
- Shallow-pass trap: changing only the badge text or only the old Workspace provider list would not close the catalog source mismatch.
- Negative-case proof summary: `bundle://proof/SB01/transcripts/failing-first-provider-tab.txt` records the old missing AgentFramework provider panel.
- Semantic positive proof: `bundle://proof/SB01/transcripts/provider-tests-and-build.txt` proves seed defaults and provider tree rendering; `bundle://proof/SB01/transcripts/browser-proof.txt` proves the running provider tab.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` states no hard-coded provider count and no `TODO` or `NotImplemented` stubs.

## Failing-First Exemption

- Failing-first transcript is present because this is production behavior, not a process-only proof.
