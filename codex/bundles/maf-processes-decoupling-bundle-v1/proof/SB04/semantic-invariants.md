# SB04 Semantic Invariants

## Invariant `SB04-INV-001`

- Invariant ID: SB04-INV-001

- Source raw note: MAF and Processes must be decoupled without simplifying or omitting process behavior, and the work must move in small safe steps.
- Expected behavior: The existing process tool builder behavior is owned by the Processes module through `ProcessAgentRuntimeToolProvider`; when Processes is registered, MAF receives process tools through the provider seam and no longer needs the legacy internal process path.
- Disallowed shallow implementation: Passing build by only registering an empty provider, proving only a count, dropping policy constants, weakening access checks, or leaving MAF to attach duplicate legacy process tools.
- Failing-first test and transcript: A missing or renamed process tool would fail `bundle://proof/SB04/transcripts/process-agent-runtime-tool-provider-parity-test.txt`; read/write/definition-scope bypasses would fail `bundle://proof/SB04/transcripts/process-agent-runtime-tool-provider-access-test.txt`.
- Passing test and transcript: Both targeted integration transcripts pass, and `bundle://proof/SB04/transcripts/solution-build.txt` proves the full solution builds.
- Changed source files and hashes: `bundle://proof/SB04/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB04/source-assertions/tool-parity-source-assertion.txt`, `provider-registration-source-assertion.txt`, `maf-provider-fallback-source-assertion.txt`, and `dispatcher-unchanged.txt`.
- Red-team negative case: A fake provider, hidden legacy attachment, missing tool, or loosened access check would be caught by exact-name parity, progress-message assertions, and direct provider invocation errors.
- Downstream dependency check: SB05 can start because process tools now work through the provider path and the legacy MAF process builder is only a zero-provider fallback.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB04 moves runtime tool construction and DI wiring; it introduces no persisted production signal, state, record, or event. | N/A | N/A | N/A |
