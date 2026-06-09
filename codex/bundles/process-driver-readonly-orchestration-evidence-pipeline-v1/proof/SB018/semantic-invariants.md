# SB018 Semantic Invariants

## Invariant SB018-PAYLOAD-BUILDERS-NO-FILE-STORAGE
- Invariant ID: `SB018-PAYLOAD-BUILDERS-NO-FILE-STORAGE`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Process payload builders construct transcript, runtime evidence, artifact evidence, Office evidence, and business-analysis process payload records from already supplied in-memory facts while preserving evidence hashes, content type, byte size, and read-only capability scopes.
- Disallowed shallow implementation: File, directory, workspace, storage, database, network, runtime host, DI/service lookup, manager command, object/dynamic dispatch, direct verifier construction, mutation, or payload records that omit supplied-content contract proof.
- Failing-first test: No genuine P06 failing-first production test was produced; the initial source scan calibration failure is recorded separately at bundle://proof/SB018/transcripts/p06-source-scans.txt.
- Passing test: bundle://proof/SB018/transcripts/build-payload-builders.txt, bundle://proof/SB018/transcripts/focused-p06-integration-tests.txt, bundle://proof/SB018/transcripts/focused-p06-boundary-unit-tests.txt, and bundle://proof/SB018/transcripts/full-unit-p06.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationPayloadBuilder.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs
- Production assertions: Builders create read-only scopes, normalize requested operations through the existing process operation policy, create evidence references from supplied strings/descriptors/items, and do not access external content sources.
- Red-team negative case: bundle://proof/SB018/transcripts/p06-source-scans-fixed.txt rejects file/storage/workspace/network APIs, runtime host/DI/manager tokens, object/dynamic dispatch, direct verifier construction, Core reverse dependencies, UI/media drift, and stubs.
- Downstream dependency check: P07 may start because process orchestration now has typed in-memory payload builders feeding supplied payload records without external reads or mutation.
