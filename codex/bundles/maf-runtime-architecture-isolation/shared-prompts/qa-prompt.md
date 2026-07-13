# QA Prompt

Use this prompt for bundle or subbundle validation.

```text
Validate codex/bundles/maf-runtime-architecture-isolation as a generic MAF runtime architecture refactor bundle.

Check that:
- raw notes M001-M011 are preserved and mapped;
- no subbundle implements Financial Strategist, MarkItDown, margin, quotation, document-domain, or project-structure writeback work;
- requirements R001-R012 are either closed by proof or explicitly pending;
- the selected subbundle did not implement out-of-scope later work;
- extracted collaborators have production callers and direct tests;
- tests use mocks/fakes at the new seams where claimed;
- reflection-heavy tests for moved behavior are removed, reduced, or justified;
- proof artifacts use production paths, not unused wrappers or test-only fakes;
- semantic invariants and any Production Behavior Artifact Matrix are present for critical subbundles;
- performance claims have command output and separate local runtime composition from external provider latency;
- reviews/01-execution-report.md is updated.

Report findings first, ordered by severity, with file and line references.
```
