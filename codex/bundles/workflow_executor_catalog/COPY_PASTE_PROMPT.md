You are Codex working in `C:\repositories\CanDoItAll` on branch `processes-hardening`.

Implement `codex/bundles/workflow-executor-catalog-followup` subbundles in order. Do not skip validation gates. Start with SB01 and stop if validator/catalog/runtime guardrails cannot honestly pass.

Main goals:
- Fix the workflow validator/catalog injection regression risk.
- Prove or implement workflow artifact content persistence and retrieval.
- Expand practical workflow executors/helper nodes, especially local workspace folder/file operations.
- Implement the highest-value planned executors (`json.transform`, `markdown.render`, `utility.delay`, explicit approval/control helpers) without breaking existing templates.
- Add workflow templates and UI authoring coverage for local folder/file and data-shaping scenarios.

Rules:
- Keep CanDoItAll workflow definitions as the canonical product model.
- Keep dynamic user-authored graphs on the existing MAF adapter strategy unless a subbundle explicitly says otherwise.
- Comments in source code must be in English.
- Preserve existing managed workflow seeds and user-managed workflow definitions.
- Record failing-first tests and proof transcripts in this bundle.
