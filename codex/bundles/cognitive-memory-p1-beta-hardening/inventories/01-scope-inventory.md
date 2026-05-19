# Scope Inventory

| Area | Current Source | P1 Use |
| --- | --- | --- |
| API mapping | `src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs` | Contract/version metadata, examples, retention endpoint. |
| Operations services | `src/CanDoItAll.Modules.CognitiveMemory/Operations/*` | Retention cleanup and provider failure proof. |
| Review UI service | `src/CanDoItAll.Modules.CognitiveMemory/ReviewUi/*` | Operator audit snapshot additions. |
| Blazor operator page | `src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/*` | Render audit/retention signals if needed. |
| External source ingestion | `src/CanDoItAll.Modules.CognitiveMemory/Settings/*` | Limits, extraction detail, sensitive-content policy. |
| Docs | `docs/cognitive-memory/**` | Stage, roadmap, architecture, runbook, and mermaid updates. |
| Tests | `tests/CanDoItAll.Tests.Unit`, `tests/CanDoItAll.Tests.Integration`, `tests/CanDoItAll.Tests.Components` | Deterministic proof. |
