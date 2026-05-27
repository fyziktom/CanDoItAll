# SB02 Semantic Invariants

- Invariant ID: `SB02-INV-001`
- Source raw note: `repo://codex/bundles/maf16-real-adoption-process-proof-v3/requirements/01-normalized-requirements.md` RQ02.
- Expected behavior: The unit test reflects loaded MAF 1.6 assemblies and proves expected symbols are present while known deferred symbols are absent.
- Disallowed shallow implementation: Source-only proof, package-only proof, or local stub classes that mimic package symbols.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB02/transcripts/passing.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` keeps MAF 1.6 package references and the test reflects loaded assemblies.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` records the failed exact-symbol assumption before the reflection test was corrected.
- Downstream dependency check: SB03 and SB18 consume only reflected symbols recorded by this proof.
