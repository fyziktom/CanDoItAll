# SB04 Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: Do not rush Process Core, preserve original functionality, and force refactor gates before moving execution/retry/provider logic.
- Expected behavior: The execution/retry/provider bundle starts with module-local architecture guardrails, no Process Core project, no production driver API tokens, no dispatch stubs, and no prohibited viewport proof artifacts.
- Disallowed shallow implementation: A superficial wrapper pass that adds Process Core or driver vocabulary, hides side effects behind pure-looking helpers, leaves TODO or NotImplementedException stubs, or records browser/mobile proof for a runtime-only refactor is rejected.
- Failing-first test: N/A - process non-production architecture guard with no production behavior change; bundle://proof/SB04/transcripts/source-assertions-and-scans.txt rejects the shallow boundary drift cases.
- Passing test: bundle://proof/SB04/transcripts/focused-sb04-architecture-test.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs plus bundle docs listed in bundle://proof/SB04/manifest.md.
- Production assertions: bundle://proof/SB04/transcripts/source-assertions-and-scans.txt proves no Core/driver API tokens, no dispatch stubs, and no prohibited viewport proof paths.
- Red-team negative case: bundle://proof/SB04/transcripts/source-assertions-and-scans.txt scans for forbidden Core, driver, stub, and viewport terms.
- Downstream dependency check: SB05-SB08 may proceed only because SB04 has a passing focused guard and clean source scans.
