# Target Solution

## Cluster Search UI

The Cognitive Memory page gains a dedicated `Cluster Search` tab. The tab is a large desktop operator surface with a compact filter band, paged results, and previews that are useful for validation work. It does not add mobile or medium-screen CSS.

## Review UI Service Contract

The Review UI query adds a strongly typed cluster-search filter object:

- text query
- key family
- readiness
- risk

The Review UI snapshot adds paged cluster-search result views and query-specific total counts. The service clamps page indexes through the existing paging normalization path before loading data.

## Data Access

The query layer filters `CognitiveMemoryQualityClusterRecord` rows and uses `CognitiveMemoryQualityClusterKeyRecord` for searchable key/display text. Result page IDs are loaded first, then key/member previews are loaded only for that page. Preview counts and page sizes remain bounded.

## Validation Architecture

Validation uses the web app and public Cognitive Memory API:

- read `/api/cognitive-memory/status`
- inspect database profiles and settings
- create/switch a clean PostgreSQL profile if the environment supports it
- verify Qdrant health when available
- use supported ingestion, consolidation, review decision, probe, and recall endpoints
- record operation IDs, trace IDs, counts, and mismatches in the workbook and execution report

## Follow-Up Architecture Output

If clean transfer, long-running dreaming, or source-truth validation is blocked, the bundle closes with a new follow-up architecture bundle that names the missing capability and proposes the smallest maintainable fix.
