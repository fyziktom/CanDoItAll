# SB01 Semantic Invariants

- Invariant ID: SB01-INV-001
- Source raw note: Missing and obsolete API, DTO, docs, and skills coverage must be mapped in XLSX.
- Expected behavior: The workbook regenerates route, DTO, docs, skills, tool parity, gap closure, and validation sheets from source-backed inputs.
- Disallowed shallow implementation: A static spreadsheet or prose-only list that can drift from route source.
- Failing-first test: N/A, non-production inventory artifact; stale source counts reopen SB01.
- Passing test: `bundle://proof/SB01/transcripts/workbook-generation.md`
- Changed source files: `bundle://inventories/build-gap-map.mjs`, `bundle://inventories/api-docs-skills-gap-map.xlsx`
- Production assertions: No runtime production behavior changed in SB01.
- Red-team negative case: `bundle://proof/SB01/transcripts/source-audit.md` checks the builder still names all owned route surfaces.
- Downstream dependency check: SB02 through SB06 used the regenerated workbook and reran it after route appendix and tool-count corrections.

