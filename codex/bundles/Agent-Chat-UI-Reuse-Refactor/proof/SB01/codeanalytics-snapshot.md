# SB01 CodeAnalytics snapshot

## Product snapshot

- Snapshot: `snap-20260816102508-c82f9e5f`
- Solution: `CanDoItAll.slnx`
- Scope: `CanDoItAll.AgentFramework.Components`, `CanDoItAll.AppComponents`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Processes`
- Health: 4 projects, 320 documents, 764 types, 7,473 members, 307 findings, 6 non-blocking diagnostics.
- Exact types resolved: `AgentChatPanel`, `FloatingAgentChatHost`, `AgentCatalogPanel`, and `AgentDetailsDialog`.
- Razor-only components were supplemented with exact `rg` references because the C# snapshot does not model Razor-generated component types reliably.
- Hotspots: `AgentChatPanel.razor.cs` has 2,108 lines/146 members; `FloatingAgentChatHost.razor.cs` has 656 lines/69 members; `FloatingAgentChatCoordinator` has 566 lines.

## Test snapshot

- Snapshot: `snap-20260816102634-8595e261`
- Solution: `tests/Solutions/CanDoItAll.Tests.Components.slnx`
- Health: 1 project, 167 documents, 448 types, 3,013 members, no diagnostics.

SB01 has no production diff, so an impacted-test request is not required. One exact existing test was discovered and executed only to prove the local test environment.

