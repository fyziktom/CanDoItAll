# SB20 Proof Manifest

## Scope

SB20 performs final red-team closure and records the next cutline.

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs` SHA-256 834a586f3b806d4434fbb92c10f2b0d60b13ee8ca85136ab0d8d563b532a2fc7
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs` SHA-256 b51da451d02dbfe83e753155be58c83b0eec6f4c1e9ec0a3037b7d3fd2244a56
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` SHA-256 e202b81b6e7f9317b80dc682940eb5839c1aabd12ff5c863ece182f9d22d7d8c

## Semantic Contract

- `bundle://proof/SB20/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB20/transcripts/sb20-source-assertions.txt`
- Red-team transcript: `bundle://proof/SB20/transcripts/sb20-final-red-team.txt`
- Completed validator transcript: `bundle://proof/SB20/transcripts/sb20-completed-validator.txt`
- Build transcript: `bundle://proof/SB15/transcripts/sb15-build-after-pre-execution-facade.txt`
- Focused test transcript: `bundle://proof/SB15/transcripts/sb15-focused-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB19/transcripts/sb19-final-source-scans.txt`
- Failing-first proof: N/A - process/non-production final closure gate; no Process Core or driver production API was created.
