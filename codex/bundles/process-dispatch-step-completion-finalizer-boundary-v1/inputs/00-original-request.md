# Original Request

The user asked for a review of the completed `maf-processes-refactor` branch, then a new ZIP bundle. Key constraints:

- Do not rush Process Core extraction unless it is clearly ready.
- Continue smaller isolation steps that move toward Process Core.
- Long dispatcher files should be decomposed gradually.
- Prefer abstraction/seam bundles first, then use those seams for concrete extraction.
- Also remember this is preparation for future process helper drivers, not only Process Core extraction.
- Split work into phases so Codex can work longer without getting lost.
- Enforce refactoring gates every few subbundles.
- Provide the bundle as a ZIP with detailed checklist workbook.
