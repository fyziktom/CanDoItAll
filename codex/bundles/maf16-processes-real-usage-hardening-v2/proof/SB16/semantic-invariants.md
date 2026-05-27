# SB16 Semantic Invariants

- Invariant ID: SB16-INV-001
- Source raw note: Execute MAF/process hardening with source-backed proof, app startup, and simple agent communication.
- Expected behavior: Runtime stabilization closes with no new cross-boundary coupling or placeholder logic.
- Disallowed shallow implementation: A version-only package bump, placeholder proof, screenshots without HTTP/browser evidence, or tests that do not exercise the relevant MAF/process boundary.
- Failing-first test: bundle://proof/SB16/transcripts/failing-first.txt proves the adversarial placeholder/unsafe shape is rejected or absent with a non-zero command result.
- Passing test: bundle://proof/SB16/transcripts/passing.txt records the relevant restore/build/test/browser or agent-smoke command result with exit code 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs.
- Production assertions: Runtime behavior is proven by source assertions, targeted tests, and no-stub scans under bundle://proof/SB16/transcripts/.
- Red-team negative case: bundle://proof/SB16/transcripts/failing-first.txt documents the rejected shallow or unsafe case for SB16-INV-001.
- Downstream dependency check: Restore, build, unit bucket, integration bucket, component bucket, static audits, web smoke, and agent smoke all closed before SB18.
