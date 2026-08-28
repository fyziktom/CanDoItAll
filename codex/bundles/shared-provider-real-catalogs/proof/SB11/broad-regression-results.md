# SB11 final regression review

Status: Requested-scope gate passed with unchanged reviewed baseline failures.
The full repository is not green. No failed case was reclassified as passing.

CodeAnalytics actual-diff analysis required the supplied Unit and Integration suites
because static dispatch/reference information was incomplete. Original discovery,
console results, TRX and deferred-theory mappings are retained in transcripts/.

| Final suite | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Unit, vision-unit-complete | 7059 | 1 | 0 | 7060 |
| Integration, vision-integration-complete | 1133 | 10 | 1 | 1144 |

The separate focused gates pass all 129 cases (69 final-all-focused plus 60
vision-budget-green). Real UI image creation, attachment, preview and analysis
through the source instance also pass; see manifest.md for exact runs and usage.

## Baseline comparison

transcripts/final-regression-comparison.json compares the original final results
with SB09 unit-broad.trx and integration-broad.trx by exact failed test identity.
No new failed identities or causes occur in either suite. Complete failure messages
match after normalizing generated GUIDs and one ephemeral loopback Host port.
The unnormalized comparison remains visible; it is not silently overwritten.

- Unit: WorkflowCatalogTests.ComponentLibraryAcceptsStructuredOutputForOllama
  still lacks the llama3.2 price-row fixture, as in SB06/SB07/SB09.
- Integration: five existing missing-price-row failures in seed, plugin and process
  fixtures; three existing seed/catalog assertions; two backend checkpoint fixtures
  lacking required agentFrameworkProviderKind publication metadata. All ten exact
  identities and causes match SB09. Their fixture and failing ownership paths were
  not changed to make this repair pass.
- The one opt-in skip remains LiveLocalOllamaThinkingEffortIntegrationTests.
  Installed_catalog_and_native_effort_mapping_match_thinking_capabilities. It is
  not counted as passing, nor substituted by this incident's OpenAI image proof.

## Discovery and intermediate results

Final Unit freezes 7026 discovered entries. Three deferred theories expand into 37
runtime cases, accounting for all 7060 original results. Integration freezes 1139
entries; one deferred theory expands into six cases, accounting for all 1144.
The runner reconciles every original identity; zero, missing and unselected tests
fail the collector. Integration ran to completion in about one hour, without a
timeout truncation or narrower rerun.

VSTest's notExecuted summary counter is zero despite the skipped result row. The
report uses the original NotExecuted result identity and console count: one skip.

Earlier final-unit-complete had an additional LocalWorkspaceProcessHost cancellation
timing failure while Docker was building. All 13 class cases passed in isolation,
and that case passed again in the final full Unit run. The earlier failed TRX is
retained; it is not relabeled. The earlier image-budget fixture compilation error
is likewise retained separately from the subsequent failing-first behavior cases.

## Privacy and reproducibility

Two JWT-shaped fixture strings in the completed Integration TRX were mechanically
replaced with [REDACTED-JWT]. transcripts/credential-redaction.json retains original
and redacted file SHA256 values and confirms unchanged test identities and counters.
No real source bearer is retained in proof. Token renewal used existing UI controls
and least-privilege scopes; API authorization was not weakened.

changed-files.csv contains before/after hashes of all eight changed source/test
files. proof-hashes.csv binds the final bundle, evidence and deployment metadata.
No Components suite or solution-wide gate is claimed for this no-component-code
change. Final semantic/source review is by the primary implementation agent, not
an independent reviewer: bundle://reviews/05-project-image-final-verifier.md.
