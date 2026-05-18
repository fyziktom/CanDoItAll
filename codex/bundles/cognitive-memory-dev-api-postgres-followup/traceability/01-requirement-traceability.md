# Requirement Traceability

| Requirement | Evidence |
| --- | --- |
| R1 PostgreSQL-first gate | Skill guardrails; loader checks `/api/cognitive-memory/status`; PostgreSQL smoke evidence pending |
| R2 Previous-bundle state assessment | `analysis/01-current-state.md` |
| R3 Developer API | `src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`; route mapping; OpenAPI test assertions |
| R4 Codex skill | `C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md` |
| R5 Sample source data | `sample-source-data/*.md`, `sample-source-data/*.mmd`, `sample-projects.json` |
| R6 Behavior smoke | `sample-source-data/Load-CognitiveMemorySamples.ps1`; evidence pending under `evidence/` |
| R7 Explicit limitations | Skill instructions and execution report must record provider-unavailable recall/projection responses |
