# Broader checkpoint results

The final affected scope is green: 206 Unit, 46 Components and 56 Integration cases.
Exact discovered/executed name equality is in discovery-verification.json. These
results do not mean the entire repository is green.

CodeAnalytics returned AllSuppliedSuites for changed public catalog contracts and
unresolved dynamic dispatch (impact-selection.json). Run-Broad.ps1 ran each supplied
project once at the frozen SB07 checkpoint. All three runs finished naturally:

| Suite | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Unit | 7014 | 1 | 0 | 7015 |
| Components | 1103 | 53 | 0 | 1156 |
| Integration | 1121 | 18 | 1 | 1140 |

Each broad command exited 1. Original TRX records, discovery and console output are
retained as *-broad.*; test-results.csv and broad-failures.csv derive from those records.

## Classification and subsequent repairs

- Unit: the same WorkflowCatalogTests.ComponentLibraryAcceptsStructuredOutputForOllama
  failure recorded in SB06: its fixture lacks the required llama3.2 price row.
- Components: 46 isolated fixtures omit IProviderRuntimeAdministrationService; three
  seed fixtures omit required gpt-5.4-mini prices; two project-structure fixtures refer
  to a missing secret; one WorkflowsPageTests case times out. Those owners are unchanged
  by this task. This is source-based classification, not a pre-edit measured baseline.
  The remaining presentation-mapper assertion expected the old model suggestions;
  it was updated for the requested shortlist and passes in the final 46-case scope.
- Integration: seven streaming fixtures lacked IProviderInferenceRelayRuntime. The
  existing deterministic adapter was registered in that isolated harness; no production
  fallback was added. These cases and the new terminal-event cases pass in the final
  56-case scope. The newly affected FunctionTools_RoundTripWithoutCentralExecution
  fixture now supplies explicit known thinking metadata and passes. The remaining ten
  failures match SB06's eight pricing/seed/plugin cases and two missing provider-kind
  metadata fixtures. See ../SB06/broad-regression-results.md for the earlier evidence.
- The opt-in installed-Ollama integration case was skipped, not passed. Separate real
  Playwright-configured Ollama Low/High requests provide the task's live proof in SB08.

The broad checkpoint predates the live-discovered temperature, SDK envelope and
Responses terminal-event fixes. These named invalidations were revalidated by the
final focused suites and final-image real requests, not another unfiltered run.
post-live-components.txt is a binary-lock build failure and executed no tests.

## Artifact privacy

Credential-shaped fixture values in three broad artifacts were mechanically redacted.
redaction.txt records replacement counts and before/after hashes. No unredacted copy is
retained. Test names/counters/outcomes are otherwise preserved; final focused discovery
and TRX artifacts need no redaction. The closure audit scans proof for credential shapes.
