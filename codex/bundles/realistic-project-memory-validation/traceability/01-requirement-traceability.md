# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R1 extraction | `scripts/extract_project_sources.py`, `inputs/extracted` | `01-source-extraction-and-truth-structuring` | Extractor run and `inputs/extracted/source-index.json` | Supports DOCX/PDF/XLSX/PPTX/XMind/GraphML/image/media. |
| R2 time slicing | `source-truth/*-time-sliced.md` | `01-source-extraction-and-truth-structuring` | Prepared bundle validator and manifest parse | Five stages per project. |
| R3 financial and operational detail | `source-truth/*-time-sliced.md` | `01-source-extraction-and-truth-structuring` | Source-truth review and recall required terms | Includes investments, team growth, construction/facilities, CAPEX/OPEX. |
| R4 deep project structure | `validation/load-realistic-project-memory-validation.ps1` | `02-project-structure-api-load` | Structure readback JSON | Heading hierarchy becomes nested nodes. |
| R5 API-only load | `validation/evidence/<runId>` | `02-project-structure-api-load` | API evidence files | No direct database or app-code data insertion. |
| R6 review decisions | `validation/evidence/<runId>` | `03-cognitive-memory-ingestion-and-consolidation-validation` | Review decision evidence | Approve source-backed candidates; reject duplicates/non-memory links. |
| R7 recall validation | `source-truth/source-manifest.json`, `validation/analyze-realistic-project-memory-quality.ps1` | `04-recall-probing-and-implementation-repair` | `95-memory-quality-analysis.json` and `96-memory-quality-analysis.md` | Checks context, source locator, and required terms. |
| R8 implementation repair | App source only if evidence requires it | `04-recall-probing-and-implementation-repair` | Build/test and rerun evidence | Not started unless validation fails for app-root-cause reasons. |
