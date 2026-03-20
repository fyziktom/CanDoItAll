# Prompt 02 — Normalization And Grouping Profiles

## Objective

Implement the grouping profile generation layer with strict vs loose normalization and structured extraction.

## Tasks

1. Replace or wrap the current `WorkKeyNormalizer` with a richer normalization stack.
2. Implement profile generation for:
   - strict/loose title
   - strict/loose composer
   - composer surname key
   - catalog parsing
   - movement markers
   - key signature extraction
   - work-type extraction
   - arrangement/editorial flags
   - embedding input text
3. Add a versioned profile-refresh path.
4. Update indexing or post-index refresh flow so grouping profiles can be built incrementally.
5. Preserve existing search behavior while preparing richer fields.

## Boundaries

- Do not implement embeddings yet unless a helper skeleton is needed.
- Do not implement clustering yet.
- Do not remove the old fields until compatibility is proven.

## Expected outputs

- profile generation services
- normalization helpers
- updated indexing/profile refresh plumbing
- tests for normalization and extraction

## Likely files

- `src/App.PdmxTool/Services/WorkKeyNormalizer.cs` or replacement files
- new grouping services under `Services/Grouping`
- `src/App.PdmxTool/Services/PdmxIndexingService.cs`
- model / DTO updates as needed

## Required tests

Add focused tests for:
- title normalization
- composer normalization
- `Lastname, Firstname`
- diacritic-insensitive loose form
- catalog token extraction
- movement extraction
- arrangement marker extraction
- strict vs loose non-equivalence where appropriate

## Review checklist

- [ ] raw source values preserved
- [ ] multiple normalized forms exist
- [ ] structured signals extracted
- [ ] risky normalization not silently treated as strict truth
- [ ] tests cover at least 20 realistic title/composer variants
