# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `REQ-001` Architecture Beta Mermaid failure | `requirements/01-normalized-requirements.md`; `architecture/01-target-solution.md` | `subbundles/01-architecture-api-doc-refresh` | inspect `docs/architecture-beta.md`; run stale syntax search | Must remove `architecture-beta` block entirely. |
| `REQ-002` API-first technical docs | `requirements/01-normalized-requirements.md`; `analysis/01-current-state.md` | `subbundles/01-architecture-api-doc-refresh` | inspect docs and source links | Depends on current Web API mappings. |
| `REQ-003` Suppressed MCP transition | `requirements/01-normalized-requirements.md`; `analysis/01-current-state.md` | `subbundles/01-architecture-api-doc-refresh` | source search for removed MCP active setup claims | Processes and ProjectStructure MCP may return later, but not active now. |
| `REQ-004` Customer/wiki content | `requirements/01-normalized-requirements.md`; `architecture/01-target-solution.md` | `subbundles/02-enterprise-wiki-and-infographics` | inspect `docs/enterprise-operating-system.md` | Must be useful to less-technical enterprise readers. |
| `REQ-005` Four infographics | `requirements/01-normalized-requirements.md`; `inputs/02-structured-input.md` | `subbundles/02-enterprise-wiki-and-infographics` | confirm four files under `docs/images` and Markdown references | Use generated raster images, not SVG placeholders. |
| `REQ-006` Process concepts | `requirements/01-normalized-requirements.md` | `subbundles/02-enterprise-wiki-and-infographics` | inspect customer doc for Plan, Execute, Validate, Audit plus escalations and HR matching | Keep detail in Markdown captions and sections. |
| `REQ-007` Economy ledger direction | `requirements/01-normalized-requirements.md`; `inputs/02-structured-input.md` | `subbundles/02-enterprise-wiki-and-infographics` | inspect wording for external/adjoining status | Avoid claiming it is shipped in this repo. |
| `REQ-008` Validation | `reviews/01-execution-report.md` | `subbundles/03-validation-and-closure` | validator output, `git diff --check`, stale-reference searches | Final closure must update raw-note closure. |
