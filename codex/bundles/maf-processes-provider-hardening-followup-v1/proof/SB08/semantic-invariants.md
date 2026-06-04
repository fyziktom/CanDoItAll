# SB08 Semantic Invariants

- Invariant ID: `SB08-INVARIANT-001`
- Source raw note: `RQ-009` Process provider must handle purpose/access policy explicitly before manager-verification and driver work.
- Expected behavior: InteractiveChat, GovernedProcessAutomation, AutoApprovedNonInteractive, and A2AEndpoint contexts expose process read tools only when process read access is configured, expose mutation tools only when explicit process write access is configured, and expose no tools for unsupported purposes.
- Disallowed shallow implementation: Continuing to expose all process mutation tools to read-only agents, ignoring provider purpose, dropping process automation mutation tools for explicitly write-enabled agents, or weakening existing provider composition failure diagnostics.
- Failing-first test: `ProcessAgentRuntimeToolProviderTests` purpose matrix tests fail on read/write exposure drift; the integration parity test fails if write-enabled process automation loses any of the 23 tool names.
- Passing test: `bundle://proof/SB08/transcripts/process-provider-purpose-unit-tests.txt`, `bundle://proof/SB08/transcripts/runtime-provider-composition-unit-tests.txt`, `bundle://proof/SB08/transcripts/process-provider-access-integration-tests.txt`, and `bundle://proof/SB08/transcripts/process-runtime-provider-parity-tests.txt`.
- Changed source files: `bundle://proof/SB08/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB08/source-assertions/process-provider-purpose-source-assertions.txt`.
- Red-team negative case: A read-only process manager context cannot see mutation tools; a no-access agent cannot see process tools; explicit write still preserves governed automation mutation capability.
- Downstream dependency check: SB09 may start because provider purpose/access behavior is explicit and observable before adding observability/receipt tagging.
