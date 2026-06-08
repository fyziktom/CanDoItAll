# Source Hotspots

| Area | Source | Concern |
| --- | --- | --- |
| Transcript verifier | `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs` | Currently owns validation, parsing, redaction, audit, hash/evidence policy. |
| Process adapter | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs` | Owns preflight, URI allowlist, observation envelope, denied audit facts. |
| Driver abstractions | `repo://src/CanDoItAll.Processes.Drivers.Abstractions` | Contract shape must remain immutable and runtime-free. |
| Core descriptors | `repo://src/CanDoItAll.Processes.Core` | Core must not reference drivers or runtime services. |
| Tests | `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` | Existing tests are good baseline; add stronger malicious corpus and runtime evidence tests. |
