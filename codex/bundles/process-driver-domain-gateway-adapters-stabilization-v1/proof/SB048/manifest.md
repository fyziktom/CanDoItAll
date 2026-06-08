# SB048 Proof Manifest

## Summary
- Status: Completed
- Gate: SB048 - Gate P broad smoke closure
- Source proof: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs
- Test proof: bundle://proof/validation/full-unit-tests-no-restore.txt and bundle://proof/validation/focused-driver-unit-matrix-no-restore.txt
- Passing transcript: bundle://proof/transcripts/passing-validation-summary.txt
- Failing-first transcript: bundle://proof/transcripts/failing-first-summary.txt
- Anti-stub audit transcript: bundle://proof/transcripts/anti-stub-source-scan-summary.txt
- Semantic invariant contract: bundle://proof/SB048/semantic-invariants.md
- Source scans: bundle://proof/source-scans/core-reverse-driver-dependency-scan.txt, bundle://proof/source-scans/driver-packages-forbidden-dependencies-scan.txt, bundle://proof/source-scans/gateway-adapter-runtime-hook-scan.txt, bundle://proof/source-scans/unit-skip-ledger-scan.txt, bundle://proof/source-scans/ui-media-drift-scan.txt

## Changed File Hashes

| File | SHA-256 |
| --- | --- |
| repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs | 48b43ae9ace87a49b07c2da313b27dde3a9eb36193aa88f8a7ea9fb27366cf83 |
| repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/CanDoItAll.Processes.Drivers.VerificationGateway.csproj | 309582cadd92f0e033bac592a5d568e2463d1e9c2306a4d49e4a687379ed5c5e |
| repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/README.md | a381d404bc5e174f78db78a39d1158cb4d557be0e220f5108c71f0261899bc4b |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs | a143a9c0a5c8407bf75b11848b756f4000ac1c5c2ce654b94b641a2a172a0724 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationOperationPolicy.cs | 501499582e3422737c937b6ba88e677a906bd3c72e0f026e39e19a186c82d46b |
| repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj | e708fa90d478ef5a064ce96095df02313ac914bb2b024e5f0dbc227c2dc5166f |
| repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs | 00e97a03cb7a7da395d81935e5de1d29f0a1485ac2c911162f7f96bd0e4b2b19 |
| repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs | 0deab452d03b26e427a817a46a471efa141dc07334d55738ecf9caece9f81ada |
| repo://tests/CanDoItAll.Tests.Unit/ProcessDriverObservationAggregationTests.cs | d7fc69567bf8c8c150d5f23c23dc8b1198cc8ad3fcc09eefb534737e4188b327 |
| repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs | 66cf2dd8946b5a3e41dfbd4f7fdf524e902e2c093163886a57cb20c7a8944243 |
| repo://tests/CanDoItAll.Tests.Unit/ProcessDriverPackageReadmeSamplesTests.cs | e7461a4ced8c3705ea6db5d47e51da81c5c1c6472efbaac4596af72b3f221435 |
| repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs | bcb2766827dab57441533a5d12acd82714f845d5bef6d70a69fd9464a00ea1c6 |
| repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs | 577bc6149ece614e2d2293cb84d38afb8aa2540588c537c1a9bad2fc4c413ff6 |
| repo://tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | b9fa2d4eb50eca6d24fdea2f6baa028d3418d89a2b076796bc595a934a4e81f2 |

## Semantic Proof
- Invariant ID: SB048-INV-001
- Semantic positive proof: bundle://proof/transcripts/passing-validation-summary.txt
- Adversarial negative proof: bundle://proof/transcripts/failing-first-summary.txt
- Anti-stub audit: bundle://proof/transcripts/anti-stub-source-scan-summary.txt
- Portable references: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/README.md and bundle://proof/changed-file-hashes.md
