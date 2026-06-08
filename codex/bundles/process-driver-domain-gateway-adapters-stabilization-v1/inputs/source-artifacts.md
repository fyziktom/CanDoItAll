# Source Artifacts To Recheck During Implementation

Use live repository code, not memory or this document only.

## Current bundle/proof artifacts
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/proof/SB060/manifest.md
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/proof/SB006/remaining-debt-ledger.md
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/12-stable-process-core-roadmap.md
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/13-domain-driver-roadmap.md
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/14-next-bundle-runtime-host-decision.md
- repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/15-next-backlog-candidates-and-reopen-triggers.md

## Production code
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Drivers.Abstractions
- repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification
- repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence
- repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence
- repo://src/CanDoItAll.Processes.Drivers.OfficeEvidence
- repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis
- repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation
- repo://src/CanDoItAll.Processes.Drivers.VerificationGateway
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch
- repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj

## Tests
- repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- repo://tests/CanDoItAll.Tests.Unit/TuningRequestServiceTests.cs

## Bundle skill
- repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md on development branch.
