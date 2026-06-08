# Current Issues And Improvements

## Blocking
None observed in the latest source-backed scope.

## Non-blocking but important
1. `ProcessDomainEvidenceReadOnlyAdapters.cs` is broad and should be split into separate files.
2. Process module directly references multiple domain driver packages; this should either be justified with an allow-list or narrowed behind the verification gateway where safe.
3. Gateway has explicit single-lane methods, but no typed batch orchestration yet.
4. There is no process-level multi-domain verification orchestration that takes supplied payloads and returns aggregate observations.
5. Runtime host remains correctly denied, but as the read-only pipeline grows, docs/tests must keep rejecting accidental host/registry/DI drift.
