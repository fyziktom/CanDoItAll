# Subbundle 08-human-review-ui

## Objective

Add review queue, memory item detail, recall trace viewer, consolidation run viewer, and procedure library V1.

## Inputs

- Existing CanDoItAll main solution.
- Existing RAG/Qdrant driver where relevant.
- Existing SemanticCompletion/semantic driver where relevant.
- This architecture bundle.

## Implementation Rules

- Preserve source provenance.
- Do not make Qdrant the source of truth.
- Keep comments in source code in English.
- Keep module boundaries explicit.
- Add unit tests for non-happy paths.
- Prefer typed models over unstructured JSON, but allow JSON metadata for future extension.

## Required Output

- Code changes.
- Tests.
- Short implementation report.
- List of any architectural deviations.
- Evidence of build/test results when applicable.

## QA Questions

1. Does the implementation preserve raw source references?
2. Can derived data be rebuilt?
3. Is access/redaction policy respected?
4. Are failures and edge cases tested?
5. Does the implementation avoid merging semantically similar but context-separated knowledge incorrectly?
