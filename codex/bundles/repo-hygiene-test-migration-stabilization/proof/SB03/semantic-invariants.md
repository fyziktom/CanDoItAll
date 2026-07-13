# SB03 Semantic Invariants

- Invariant ID: `SB03-PROCESS-BRANCH-OUTCOME`
- Source raw note: RH-005 and RH-006 required template assertions and completed process branch recovery to match current runtime behavior.
- Expected behavior: declared unambiguous branch outcomes are recovered from completed output, while ambiguous decision prose is rejected.
- Disallowed shallow implementation: accepting every markdown heading as an outcome or deleting the negative parser coverage.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB03/transcripts/passing.txt`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- Production assertions: parser helpers keep branch outcome extraction line-oriented and decision-section aware.
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub.txt` covers the ambiguous validation decision section.
- Downstream dependency check: SB05 targeted and full unit proof include these process runtime tests.

