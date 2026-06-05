# SB16 Proof Manifest

## Scope

SB16 proves the facade wiring with build, focused tests, and runtime source scans.

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs` SHA-256 834a586f3b806d4434fbb92c10f2b0d60b13ee8ca85136ab0d8d563b532a2fc7
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` SHA-256 e202b81b6e7f9317b80dc682940eb5839c1aabd12ff5c863ece182f9d22d7d8c
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` SHA-256 76fa61a52587deafba8f3f7cf9d8368c8d2cf29e43cd7cb6fb5b4072861a8499

## Semantic Contract

- `bundle://proof/SB16/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB15/transcripts/sb15-focused-tests.txt`
- Build transcript: `bundle://proof/SB15/transcripts/sb15-build-after-pre-execution-facade.txt`
- Source assertion transcript: `bundle://proof/SB16/transcripts/sb16-source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB16/transcripts/sb16-runtime-smoke-source-scans.txt`
- Failing-first proof: N/A - process/runtime smoke gate; no production behavior change was intended beyond local delegation.

