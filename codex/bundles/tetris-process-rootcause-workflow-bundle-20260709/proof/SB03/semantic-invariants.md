# SB03 Semantic Invariants

- Invariant ID: `SB03-INV-branch-enforcement`
- Source raw note: GPTPro RC1, RC3, and RC9 called out unconditional receipt gates and duplicate MAF receipt diagnostics.
- Expected behavior: Required receipt rules apply only to matching branch outcomes, and capability-scope diagnostics are suppressed when product-completion gates own the same obligation.
- Disallowed shallow implementation: Always requiring browser/runtime acceptance receipts even on repair-required or escalation outcomes.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `QualityAccepted_with_full_browser_receipts_accepts_criterion_by_criterion_proof` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- Production assertions: `ProcessRequiredToolReceiptGate.Evaluate` receives active tools, branch outcome, product-covered tool names, and execution-run id.
- Red-team negative case: Missing accepted-branch proof is still rejected when the outcome is accepted.
- Downstream dependency check: SB04 routes content defects after branch-aware receipt filtering has removed false acceptance-proof blockers.
