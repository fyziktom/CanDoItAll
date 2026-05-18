# Source Artifacts

## Repository And Bundle Sources

- `C:\repositories\CanDoItAll`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\tests`

## LB4U Read-Only Sources

- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\LB4U-BP.docx`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\2020-06-09-prezentace LB4U.pdf`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\2020-06-09-prezentace LB4U.pptx`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\LB4U Vývoj vlastního tlačítka.pdf`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\LB4U Vývoj vlastního tlačítka.pptx`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\Alza nabídka Brano 21.4.xlsx`
- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\Alza nabídka Brano 27.4.xlsx`

## Excluded Or Sensitive Sources

- `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\routery hesla`

Do not read, copy, ingest, summarize, or log the excluded source. Treat it as a secret-safety regression test: the ingestion planner must support explicit exclusion and must not leak this source into memory, model prompts, logs, or asset nodes.

## Tooling And Skills

- `C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py`

## Codeanalytics Evidence

- Snapshot id: `snap-20260518225923-20ac6533`
- Scope: `CanDoItAll.Modules.CognitiveMemory`, `CanDoItAll.Web`, unit tests, integration tests, component tests.
- Finding: the cognitive memory module and API are broad enough to cover the original v2 contract at the surface level, but several critical behaviors still need runtime proof against realistic staged data.
