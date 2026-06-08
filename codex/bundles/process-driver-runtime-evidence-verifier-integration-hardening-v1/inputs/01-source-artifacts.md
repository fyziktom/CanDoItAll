# Source Artifacts Checked

- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/reviews/01-execution-report.md` — Architect input claimed this prior bundle execution report existed and reported SB001-SB048 completed. It is not present in the current checkout, so SB001-SB003 must re-verify live source and fresh proof instead of relying on this missing artifact.
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs` — Production alpha verifier implementation; inspected for monolithic responsibilities, no-mutation behavior, redaction, audit, hash policy.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs` — Process module read-only adapter; inspected for runtime/DI/file/network absence and preflight denial behavior.
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/CanDoItAll.Processes.Drivers.TranscriptVerification.csproj` — Transcript verifier package dependency boundary.
- `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` — Process module dependency on transcript verifier; no DI registration found in project file.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` — Unit tests covering diagnostics, denials, hashes, no-runtime-hook architecture assertions.
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/proof/shared/transcripts/passing-source-scans.txt` — Architect input claimed this source scan existed. It is not present in the current checkout and must be regenerated during execution.
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/architecture/08-next-bundle-decision.md` — Architect input claimed this roadmap artifact existed. It is not present in the current checkout; this bundle carries the same decision explicitly in `architecture/03-runtime-evidence-consistency-verifier.md` and `architecture/06-runtime-host-deferral.md`.
- `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md` — Development branch bundle preparation rules used for this bundle shape.
