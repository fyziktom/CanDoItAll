# SB03 Manifest

## Summary

SB03 consolidated artifact finalizer, operator read-model, health-audit, and run-detail UI status projection semantics behind `ProcessArtifactStatusProjectionService`. The change removes duplicated local mapping helpers and adds focused integration coverage for every finalizer status and required-artifact satisfaction state.

## Changed File Hashes

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactStatusProjectionService.cs SHA-256 EAD979DACA4805835C5A3425E14F40B6F33E12199105AA9BEF381773373D8DB0
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs SHA-256 8DDF3E46061F37F6B7BC12E89DC82D176550419E9C9F2BD3CF5B1785F065409A
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs SHA-256 E0CDB0BA033B1D98F7096A4B890AF6723E3622ABF6627610343312EC434FCB9F
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs SHA-256 7D1F1F2C2A2221E10D7ABC050FBA874DF975D81E2F06DAEB3BEDFFD32AD7348A
- repo://tests/CanDoItAll.Tests.Integration/ProcessArtifactStatusProjectionServiceTests.cs SHA-256 7C7EA33BE52E0DD2F2CBF37C24B0CB8CA3A5933A0B09CB9D422BC811FF38CCFE

## Artifact References

- Shared projection service: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactStatusProjectionService.cs
- Operator read-model call sites: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- Health-audit call site: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs
- Run-detail UI loader call site: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs
- Projection matrix tests: repo://tests/CanDoItAll.Tests.Integration/ProcessArtifactStatusProjectionServiceTests.cs
- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.md
- Source assertions transcript: bundle://proof/SB03/transcripts/sb03-source-assertions.txt
- Adversarial duplicate-removal transcript: bundle://proof/SB03/transcripts/sb03-adversarial-duplicate-mapping-removed.txt
- Passing projection test transcript: bundle://proof/SB03/transcripts/sb03-projection-service-tests.txt
- Passing read-model regression transcript: bundle://proof/SB03/transcripts/sb03-read-model-regression-tests.txt
- Anti-stub audit transcript: bundle://proof/SB03/transcripts/sb03-anti-stub-audit.txt
- Changed-file hash transcript: bundle://proof/SB03/transcripts/sb03-changed-file-hashes.txt

## Semantic Evidence

- Raw note owned: RN03
- Shipped behavior: finalizer status mapping, validation status mapping, health invariant unsatisfied-required counting, and run-detail loader unsatisfied-required counting now share one strongly typed projection service.
- Source proof: bundle://proof/SB03/transcripts/sb03-source-assertions.txt records SB03-INV-001 and the exact call sites.
- Test proof: bundle://proof/SB03/transcripts/sb03-projection-service-tests.txt records 22 passing projection matrix tests; bundle://proof/SB03/transcripts/sb03-read-model-regression-tests.txt records 20 passing operator read-model regression tests.
- Shallow-pass trap: changing only one read-model call site while leaving separate health/UI status sets that diverge later.
- Adversarial negative proof: bundle://proof/SB03/transcripts/sb03-adversarial-duplicate-mapping-removed.txt records SB03-INV-002 and exits 1 because the old duplicate helper definitions no longer exist.
- Semantic positive proof: bundle://proof/SB03/transcripts/sb03-projection-service-tests.txt and bundle://proof/SB03/transcripts/sb03-read-model-regression-tests.txt.
- Passing transcript: bundle://proof/SB03/transcripts/sb03-projection-service-tests.txt
- Anti-stub audit: bundle://proof/SB03/transcripts/sb03-anti-stub-audit.txt records concrete mappings for `PlaceholderOnly`, `ContentUnavailable`, and `ContentHashMismatch` with no TODO or `NotImplementedException` hit.
- Browser validation: N/A - SB03 changed shared projection logic and a run-detail loader call site, not markup, styling, routing, or visible UI layout.
