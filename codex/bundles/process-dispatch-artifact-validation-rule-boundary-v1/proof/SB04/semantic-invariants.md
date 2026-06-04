# SB04 Semantic Invariants

- Invariant ID: `SB04-INV-001`
- Source raw note: "Split into phases/subbundles and enforce refactor gates every few subbundles."
- Expected behavior: Gate A blocks downstream behavior movement unless live inventory, snapshot seam, no-core/no-driver guardrails, and large-screen-only proof policy are all enforceable.
- Disallowed shallow implementation: Proceeding to SB05 because SB03 compiled, without a gate that checks inventory freshness, driver policy, and viewport proof policy for this bundle.
- Failing-first test: Gate A test fails if the inventory lacks the SB02 refreshed status/counts, if driver API names appear in the driver-readiness map, or if prohibited viewport proof paths appear under the current proof root.
- Passing test: `bundle://proof/SB04/transcripts/gate-a-architecture-tests.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB04/source-assertions/gate-a-guardrails.md`
- Red-team negative case: `bundle://proof/SB04/transcripts/gate-a-production-only-scan.txt` proves no production Core/driver tokens exist under `src`.
- Downstream dependency check: SB05 may start because Gate A passed and no prohibited boundary or viewport proof drift was found.

- Raw note owned: Enforce Gate A before behavior movement.
- Shipped behavior: No runtime behavior changed in SB04.
- Source proof: `bundle://proof/SB04/source-assertions/gate-a-guardrails.md`
- Test proof: `bundle://proof/SB04/transcripts/gate-a-architecture-tests.txt`
- Shallow-pass trap: A gate that only asserts files exist would miss stale inventory and driver-readiness drift.
- Adversarial negative proof: `bundle://proof/SB04/transcripts/gate-a-production-only-scan.txt`
- Semantic positive proof: `bundle://proof/SB04/source-assertions/gate-a-guardrails.md`
- Anti-stub audit: `bundle://proof/SB04/transcripts/gate-a-production-only-scan.txt`
