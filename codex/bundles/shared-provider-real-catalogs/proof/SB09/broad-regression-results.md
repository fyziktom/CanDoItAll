# SB09 broad regression checkpoint

CodeAnalytics returned AllSuppliedSuites for unresolved member/dispatch analysis.
Run-Broad.ps1 executes each supplied suite once using frozen Debug test binaries.
Original discovery, console and TRX files are retained in transcripts/.

| Suite | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Unit | 7037 | 1 | 0 | 7038 |
| Components | 1110 | 52 | 0 | 1162 |
| Integration | 1133 | 10 | 1 | 1144 |

## Reviewed completed failures

- Unit: WorkflowCatalogTests.ComponentLibraryAcceptsStructuredOutputForOllama is
  the same missing llama3.2 price-row fixture failure recorded in SB06/SB07.
- Components: 46 fixtures omit IProviderRuntimeAdministrationService, three seed
  fixtures lack required gpt-5.4-mini prices, two project-structure fixtures refer
  to a missing secret, and one WorkflowsPageTests case times out. All 52 exact
  failed test identities occur in SB07. Their production/fixture owners are unchanged
  by this repair; the new provider editor/refresh and agent thinking cases pass.
- Integration: five existing missing-price-row failures in seed, plugin and process
  fixtures; three existing seed/catalog assertions; two backend checkpoint fixtures
  missing required agentFrameworkProviderKind publication metadata. All ten exact
  identities and first-line failure causes match SB07 (apart from generated IDs).
  Their relevant fixture/production owners are unchanged by this repair.

There are no new failed test identities across the three broad runs. This does not
make the repository green: 63 failures and one opt-in skip remain. Integration took
about 61 minutes. Its frozen discovery has 1139 entries; one deferred plugin theory
expands into six original result rows, accounting for the final 1144 total. The
collector verifies every discovered case and every original result, including that
explicit expansion; no test is dropped or counted as passed without execution.

Final focused scope is 138 Unit, 35 Components and 56 Integration, all passed.
The final layout-only change was revalidated through 35 Components cases, actual
model selection in agent/Simple Chat dialogs, five-tab layout, and real Sol High.
No repeated unfiltered run was started after that bounded invalidation.

Windows DLL-lock build attempts are explicitly failed/no-test attempts. The active
broad testhost kept its loaded binaries; the final component rerun was delayed until
it exited. No unrelated fixture or pricing catalog was rewritten to green this gate.

Credential-shaped fixture strings are mechanically redacted after the corresponding
run completes, with before/after hashes in redaction.csv. Focused results need no
redaction. Collector output and broad-comparison.json record exact identity comparison.
