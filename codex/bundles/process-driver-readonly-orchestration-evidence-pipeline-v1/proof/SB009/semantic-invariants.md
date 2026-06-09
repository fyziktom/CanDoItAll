# SB009 Semantic Invariants

## Invariant SB009-ADAPTER-DECOMPOSITION
- Invariant ID: `SB009-ADAPTER-DECOMPOSITION`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The broad process-domain adapter file no longer owns lane implementations; artifact, Office, business-analysis, and aggregation lanes live in focused files, with shared deterministic request and observed-at helpers.
- Disallowed shallow implementation: A split that leaves implementation in `ProcessDomainEvidenceReadOnlyAdapters.cs`, keeps direct alpha verifier construction, or fails due namespace collisions after moving code.
- Failing-first test: bundle://proof/SB009/transcripts/build-adapter-split.txt
- Passing test: bundle://proof/SB009/transcripts/build-adapter-split-fixed.txt, bundle://proof/SB009/transcripts/focused-p03-unit-tests.txt, bundle://proof/SB009/transcripts/focused-p03-integration-tests.txt, and bundle://proof/SB009/transcripts/full-unit-p03.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactEvidenceReadOnlyAdapter.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessOfficeEvidenceReadOnlyAdapter.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessBusinessAnalysisReadOnlyAdapter.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDriverObservationAggregationReadOnlyAdapter.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyObservationClock.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationRequestFactory.cs
- Production assertions: Focused lane files own their adapters; shared helpers are used by transcript, runtime, artifact, Office, business-analysis, and aggregation paths; direct alpha construction is absent in the changed adapter surface.
- Red-team negative case: bundle://proof/SB009/transcripts/p03-source-scans.txt checks the retained marker line count and denies direct alpha construction or generic runtime dispatch tokens.
- Downstream dependency check: P04 may start because the process adapter surface is now split enough for typed batch gateway and process orchestration work to avoid editing a broad multipurpose file.
