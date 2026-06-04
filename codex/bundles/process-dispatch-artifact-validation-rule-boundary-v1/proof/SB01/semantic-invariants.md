# SB01 Semantic Invariants

- Invariant ID: `SB01-INV-001`
- Source raw note: "Continue smaller dispatcher isolation steps first" and "Do not rush Process Core extraction unless clearly ready."
- Expected behavior: SB01 confirms the current repo is on the intended branch, the validation-boundary source files exist, and no Process Core or driver-pack boundary has already appeared.
- Disallowed shallow implementation: Marking the bundle ready from prepared prose without checking the current branch, source files, and guardrail scans.
- Failing-first test: N/A because SB01 is audit-only and changes no production behavior.
- Passing test: `bundle://proof/SB01/transcripts/entry-audit.txt`
- Changed source files: N/A.
- Production assertions: `bundle://proof/SB01/source-assertions/entry-audit.md`
- Red-team negative case: The guardrail scan would expose actual Process Core or driver-pack production files; current hits are limited to architecture-test guard text.
- Downstream dependency check: SB02 may start because required source/test paths exist and the stale line-count fact is documented.

- Raw note owned: Preserve prior boundaries before validation extraction.
- Shipped behavior: No production behavior changed in SB01.
- Source proof: `bundle://proof/SB01/source-assertions/entry-audit.md`
- Test proof: N/A, audit-only.
- Shallow-pass trap: Treating stale bundle facts as current proof.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/entry-audit.txt` includes no-core/no-driver scan output.
- Semantic positive proof: `bundle://proof/SB01/source-assertions/entry-audit.md` records current branch, source existence, line count, and write-boundary preservation.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
