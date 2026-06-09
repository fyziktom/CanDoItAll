# SB036 Proof Manifest

## Scope
- Critical P12 gate for Core descriptor consumer allow-list hardening.
- Refreshes the process-module Core descriptor consumer map with a source-derived exact list.
- Tightens architecture and integration tests so stale Core/driver allow-list entries are rejected.
- Adds production global-using drift and Core-to-driver reverse dependency guards.
- Keeps production behavior unchanged.

## Changed-File Hashes
- repo://codex/bundles/process-driver-readonly-orchestration-evidence-pipeline-v1/architecture/05-process-module-core-descriptor-consumer-map.md SHA-256 02485223A8F1B8159863000358B9C0F0AAF309409A9AA73E98B543087D2DFE3B
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs SHA-256 23CE068CC5AF0CD351F22454342EEB05DCDB01CECCD304911A25974436715035
- repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 8627F665766F144D89900D6C3519EBD4332AA000DF1363A237458C38F99ABFBF
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs SHA-256 7DC622803764AA66B7633B8DE671B62023F0C42373D0B0DC9B36067C4F23459D

## Command Transcripts
- Passing build transcript: bundle://proof/SB036/transcripts/build-core-boundary-hardening.txt
- Passing focused Core boundary unit transcript: bundle://proof/SB036/transcripts/focused-p12-core-boundary-unit-tests.txt
- Passing focused driver allow-list integration transcript: bundle://proof/SB036/transcripts/focused-p12-driver-allowlist-integration-test.txt
- Passing full unit transcript: bundle://proof/SB036/transcripts/full-unit-p12.txt
- Source scan and anti-stub audit transcript: bundle://proof/SB036/transcripts/p12-source-scans.txt
- Source assertions transcript: bundle://proof/SB036/transcripts/source-assertions.txt
- Prepared validator after P12 bundle updates: bundle://proof/SB036/transcripts/prepared-validator-after-p12.txt

## Semantic Adequacy
- Semantic invariant contract: bundle://proof/SB036/semantic-invariants.md
- Shallow-pass trap: updating prose only, keeping a stale allow-list entry, allowing Core/driver global usings, or leaving Core reverse dependency proof as a report-only scan.
- Failing-first proof: No deliberate P12 production failure was produced; this phase adds documentation and boundary tests without changing production behavior. The removed stale allow-list entry is protected by exact-set assertions.
- Semantic positive proof: bundle://proof/SB036/transcripts/build-core-boundary-hardening.txt, bundle://proof/SB036/transcripts/focused-p12-core-boundary-unit-tests.txt, bundle://proof/SB036/transcripts/focused-p12-driver-allowlist-integration-test.txt, and bundle://proof/SB036/transcripts/full-unit-p12.txt
- Adversarial negative proof: bundle://proof/SB036/transcripts/p12-source-scans.txt, `Process_driver_readonly_orchestration_SB034_SB035_INV_001_refreshes_core_consumer_map_and_rejects_global_using_drift`, and `Process_runtime_evidence_readonly_adapter_SB030_INV_003_keeps_driver_references_allowlisted_and_unregistered`.
- Anti-stub audit: bundle://proof/SB036/transcripts/p12-source-scans.txt

## Source Assertions
- Process module Core consumer map has exactly 25 files and excludes `ProcessDomainEvidenceReadOnlyAdapters.cs`.
- Architecture test compares the source-derived Core consumer set, the allow-list, and the map document.
- Production global usings for Process Core and driver namespaces are denied.
- Core project/source must not reference `CanDoItAll.Processes.Drivers`.
- Integration driver-consumer allow-list now uses exact-set proof and rejects stale marker drift.
- Source scans reject runtime host, DI, file/network/storage/workspace, object/dynamic dispatch, direct process-module verifier construction, stubs, Core reverse dependency, global using drift, and UI/media drift.

## Production Behavior Artifact Matrix
- New production records/signals: N/A. P12 introduced documentation and tests only.
- Existing production boundary governed:
  - Producer: `CanDoItAll.Processes.Core` descriptor types; consumer: approved process-module dispatch edge files listed in `architecture/05-process-module-core-descriptor-consumer-map.md`; lifecycle: already-resolved process facts -> Core descriptor adapters/rules -> read-only verification payloads/diagnostics.
  - Producer: process-module read-only driver adapters; consumer: typed verification gateway calls; lifecycle: supplied payloads -> read-only request factory/builders -> typed gateway methods -> verification observations.
  - Boundary signal: source-derived allow-lists in unit/integration tests; consumer: CI/unit validation; lifecycle: source scan -> exact expected file set -> failed test on drift.

## Browser And Host Proof
- Browser proof: N/A because P12 touched no UI or media surface.
- Host proof: N/A because P12 introduced no local process launch, file open, elevation, or desktop integration behavior.

## Raw Note Closure
- Raw note owned: Stable Process Core with domain drivers.
- Closure status: Partially solved for Core descriptor consumer allow-list hardening; shared harness, runtime-host denial, docs, release gates, final validation, and roadmap handoff remain owned by SB037-SB054.
