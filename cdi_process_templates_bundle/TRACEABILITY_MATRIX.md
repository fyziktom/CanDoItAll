# Traceability matrix

| Requirement | Bundle artifact(s) | Status |
|---|---|---|
| File-driven templates, not hardcoded | `repo-overlay/output/process-template-pack/**/*`, `ProcessTemplatePackLoader.cs` | Covered |
| Shared and local resources across templates | `shared/`, `processes/<key>/{roles,artifacts,checklists,validations,prompts}` | Covered |
| Detailed roles with precise knowledge/experience | role resource JSON + markdown, workbook role tabs | Covered |
| Mermaid flowchart and sequence export with supporting files | `ProcessTemplateMermaidExporter.cs`, per-process Mermaid exports | Covered |
| Strict architecture review phases and corrective-subbundle rule | `QUALITY_GATES.md`, `subbundles/*`, workbook `ArchitectureReviewPhases` and `Subbundles` tabs | Covered |
| Baseline scenarios aligned to current repo | `seed-catalog/baseline-scenarios.json`, loader/projection tests | Covered |
| Current-architecture dependency and artifact-input parity | process definitions, import envelopes, parity tests, validator script | Covered |
| Unit-test safety net for import/export regression | `repo-overlay/tests/**/*`, validator script | Covered |
| Corrective action for remaining canvas chrome hardcode | `subbundles/08-corrective-canvas-chrome-dehardcode/*`, `ProcessCanvasChromeCatalogService.cs`, `toolbox/chrome-actions.json`, component/browser validation | Implemented and validated |
