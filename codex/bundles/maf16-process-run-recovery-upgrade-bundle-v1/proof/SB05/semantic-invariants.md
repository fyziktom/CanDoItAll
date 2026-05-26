# SB05 Semantic Invariants

- Invariant ID: SB05-INV-001
- Source raw note: MAF 1.6 upgrade, failed process run artifact validation, and web-app run validation are mapped through bundle://traceability/01-requirement-traceability.md.
- Expected behavior: Preserve tool approval, middleware, instrumentation, and finalizer capture semantics.
- Disallowed shallow implementation: Do not special-case the captured run, bypass validation, add prompt-only gates, or mask content/lineage errors as success.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first.txt rejects branch-specific live-run hardcoding with exit code 1.
- Passing test: bundle://proof/SB05/transcripts/passing.txt records the passing command matrix for the implemented behavior.
- Changed source files: bundle://proof/SB05/transcripts/changed-file-hashes.txt records SHA-256 hashes for the changed source and test files.
- Production assertions: Runtime behavior is asserted by repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs plus the integration/component/build/web proof cited in bundle://proof/SB05/manifest.md.
- Red-team negative case: bundle://proof/SB05/transcripts/anti-stub-audit.txt rejects live-run hardcoding and implementation stubs.
- Downstream dependency check: SB05 closure is reflected in bundle://reviews/01-execution-report.md and downstream rows are marked pass only after dependent validation completed.
