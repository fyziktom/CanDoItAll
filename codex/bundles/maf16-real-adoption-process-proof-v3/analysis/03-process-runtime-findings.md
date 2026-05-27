# Process Runtime Findings

## Dedupe scope

Reviewed source in `ProcessesService.Runtime.Operations.cs` shows `RecordArtifactAsync` checking existing artifacts by `ProcessRunId + ProjectionIdentityHash` and `ProcessRunId + ExternalReferenceKey`. This may be too broad.

Required fix/proof:

- Same run + same identity + same step/expectation should dedupe.
- Same run + same identity + different step should not silently return the old artifact.
- Same run + same external reference + different expectation should not satisfy the new expectation.
- If a collision is detected, return a validation error or create a distinct record with collision-safe lineage, depending on intended semantics.

## Required narrative content

The first failed live run involved a required `Brief` / delivery contract artifact. For strict process definitions, such critical narrative artifacts should be content-backed unless explicitly marked as manual/no-file.

Required fix/proof:

- Strict required narrative artifacts with managed path must have readable content.
- Manual decision-only artifacts can remain no-file if type/contract says so.
- UI/API must show `ContentUnavailable`/`ContentMissing` rather than full satisfaction when content is not readable.

## Read model vs finalizer

Step detail must not say an artifact is simply satisfied when finalizer would reject it. It may say `RecordedButInvalid`, `ContentUnavailable`, `WrongRun`, etc.
