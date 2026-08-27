# Broader regression results

The requested administration acceptance is green: 40 distinct focused cases and actual
1920x1080 Playwright MCP on the final image. These broader runs are separate evidence,
expanded after CodeAnalytics returned AllSuppliedSuites because of unresolved dispatch.
They do not support a clean-whole-repository claim.

## Run state

- Unit: completed, 6,988 passed, 1 failed, 6,989 total, zero skipped.
  Original artifacts: unit-suite.txt and unit-suite.trx.
- Integration: completed, 1,121 passed, 17 failed, one opt-in skip, 1,139 total.
  Original artifacts: integration-suite.txt and integration-suite.trx.
  The run is serial by the project's existing CollectionBehavior setting; it finished
  naturally at 22:09:27Z after approximately one hour. The later bounded-stop check found
  no running test process, so no stop was performed. No result is partial or aborted.
- Both test commands exited 1. The collector's exit 0 means extraction/audit succeeded,
  not that either suite passed. Exact names and first errors are in
  transcripts/broad-regression.txt. The TRX counter reports notExecuted=0, but its actual
  Integration result records and console summary contain one skipped case.

## Failure classification

The full unit failure is WorkflowCatalogTests.ComponentLibraryAcceptsStructuredOutputForOllama:
its provider fixture lacks the required llama3.2 price row.

All 17 integration failures fall into these unchanged owners:

- Seven seed/default/model-selection cases expect price rows or seeded membership that
  the current fixtures do not supply. They include managed Luna assignments, workspace
  seed expectations, the Office365 workflow catalog and process agent selection.
- One image-provider migration case expects preservation of a fixture's customer
  configuration while the existing seed repair replaces its extra-settings catalog.
- Seven SharedProviderStreamingIntegrationTests fail during DispatcherHarness creation:
  the isolated service collection does not register IProviderInferenceRelayRuntime,
  which the existing SharedProviderHttpRelayClient constructor requires. That harness
  does not use the changed Infrastructure or Workspace token registrations.
- Two SharedProviderBackendCheckpointIntegrationTests construct projection eligibility
  without the agentFrameworkProviderKind metadata required by the unchanged projector.
  They fail before any token UI or HTTP admission path.

The opt-in live Ollama thinking-effort case is skipped; it is not passing acceptance.
No fake models, default prices, fixture memberships or unrelated runtime code were
changed to satisfy these tests. git diff confirms the listed fixtures and primary
failure owners are unchanged relative to HEAD. There was no pre-edit full-suite run,
so this is source-based classification, not a measured pre-existing full-suite baseline.

## Scope and provenance

- Privacy exception to raw artifact preservation: two credential-shaped strings in each
  broad TRX were mechanically replaced with [REDACTED_TEST_CREDENTIAL]. No test counts,
  outcomes or record counts changed. The original and redacted file SHA-256 values and
  replacement counts are in transcripts/redaction.txt; no unredacted duplicate is kept.
  Focused proof files required no redaction. No bearer/key value is displayed in evidence.
- Focused discovery and original passing TRX artifacts are in manifest.md. The final
  short-ID search change reran its registry tests and real desktop search; actual token
  admission was repeated on the same final Docker image.
- An extra HTTP rebuild attempt failed on binary locks held by the broad run. Its output
  is preserved in integration-rebuild-file-lock.txt; it executed no tests and contributes
  no passing proof. The original 19-case HTTP run remains the focused artifact.
- The broad run began before the final private ID-search predicate change. It owns broad
  compatibility evidence for that compiled checkpoint, not the final predicate; the
  later registry and final-image MCP artifacts own that predicate's acceptance.
- transcripts/Collect-BroadRegression.ps1 extracts actual outcomes and checks unchanged
  paths. Its collector success cannot convert failed test outcomes into passes.
