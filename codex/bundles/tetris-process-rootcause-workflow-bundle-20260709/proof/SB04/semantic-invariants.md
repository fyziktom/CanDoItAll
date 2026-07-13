# SB04 Semantic Invariants

- Invariant ID: `SB04-INV-routed-issues`
- Source raw note: GPTPro RC2 and RC4 required completion-gate failures to route to configured branch outcomes instead of same-step retries.
- Expected behavior: A completion issue matching route metadata can emit a routed result and append gate findings for the repair path.
- Disallowed shallow implementation: Treating all completion issues as manager retries or using branch names hardcoded in the adapter.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `QualityAccepted_with_full_browser_receipts_requires_acceptance_criteria_ids` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- Production assertions: Completion issue route parsing and result conversion operate from launch variable metadata.
- Red-team negative case: A repair route still requires deterministic defect evidence before the adapter accepts that branch.
- Downstream dependency check: SB07 template metadata supplies the route map consumed by this adapter path.
