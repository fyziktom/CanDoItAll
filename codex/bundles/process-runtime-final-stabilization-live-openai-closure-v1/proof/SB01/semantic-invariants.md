# SB01 Semantic Invariants

## Invariant SB01_INV_011
- Invariant ID: `SB01_INV_011`
- Source raw note: RN-001 asks whether processes work like before, and RN-004 requires stabilization before further runtime extraction.
- Expected behavior: A failed code-first ratio is an advisory churn signal when deterministic runtime proof, UI proof, live OpenAI proof, and boundary scans are green; it becomes a blocker only when it indicates missing source/test evidence or proof-only closure.
- Disallowed shallow implementation: Reporting `not-runtime-stable` solely because the code-first ratio failed while functional release evidence is green.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` proves baseline `HEAD` lacks `SB01_INV_011`.
- Passing test: `bundle://proof/SB01/transcripts/focused-guard-test.txt` proves `Process_runtime_host_codefirst_SB01_INV_011_ratio_failure_is_advisory_when_runtime_release_evidence_is_green` passes.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` before SHA-256 `a92dda19dd04f99621660a942e327375aeb83d94d414a17c0133436c2ea39fe5`, after SHA-256 `0e267c8114195ddbd7103e41e050e0a41cde67ccfa0f7ff397b4a9ab082c9d67`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` proves the classifier has distinct advisory and functional-blocker reasons; `bundle://proof/SB01/transcripts/source-coupling-scan.txt` proves no concrete bundle-path production coupling under `repo://src`.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` rejects the shallow baseline that lacks the advisory-ratio policy guard.
- Downstream dependency check: SB02 may proceed because SB01 now records the blocker taxonomy that SB06 must use for the final release decision.
