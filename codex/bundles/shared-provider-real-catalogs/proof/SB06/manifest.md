# SB06 governed proof manifest

Completed. Behavioral/security, architecture and desktop proof pass.
Owned inputs: N009/R9 and N010/R10. Invariants:
TOKEN-SCOPES, TOKEN-LIFECYCLE, TOKEN-ADMIN, TOKEN-PRIVACY and FRESH-5214.

- Semantic contract: bundle://proof/SB06/semantic-invariants.md.
- Changed-source hashes: bundle://proof/SB06/changed-files.csv.
- Exact pre-edit captures of six existing token owners: bundle://proof/SB06/before-hashes.csv.
  New files have no pre-edit baseline. Provider phase proof is bundle://proof/SB05/manifest.md.
- Failing-first transcript: bundle://proof/SB06/transcripts/failing-first.txt.
- Original red output and TRX: bundle://proof/SB06/regression-red.txt; bundle://proof/SB06/regression-red.trx.
- Passing evidence-verification transcript: bundle://proof/SB06/transcripts/verification.txt.
- Anti-stub audit transcript: bundle://proof/SB06/transcripts/verification.txt, including the actual source audit and exact hash checks.
- Original focused passing runs: bundle://proof/SB06/unit-tests-final.trx; bundle://proof/SB06/component-tests-final.trx; bundle://proof/SB06/integration-tests.trx.
- Frozen discovery: bundle://proof/SB06/unit-discovery.txt; bundle://proof/SB06/component-discovery.txt; bundle://proof/SB06/integration-discovery.txt.
- Desktop MCP evidence: bundle://proof/SB06/browser-validation.md; bundle://proof/SB06/mcp-lifecycle-result.json; final image repeat bundle://proof/SB06/mcp-image2-result.json.
- Architecture/limits: bundle://proof/SB06/architecture-review.md; bundle://proof/SB06/codeanalytics-summary.json.
- Reset and runtime: bundle://proof/SB06/reset-5214.txt; bundle://proof/SB06/runtime-final.txt; final image bundle://proof/SB06/runtime-image2-final.txt.
- Build: bundle://proof/SB06/build-final.txt; bundle://proof/SB06/docker-build-final.txt.
- Broader outcomes and unchanged-owner audit: bundle://proof/SB06/broad-regression-results.md;
  bundle://proof/SB06/transcripts/broad-regression.txt. Both full runs finished naturally.
- Durable proof hashes: bundle://proof/SB06/proof-artifacts.csv.
- Broad-TRX credential redaction only: bundle://proof/SB06/transcripts/redaction.txt;
  exact before/after hashes and unchanged outcomes. No original credential-bearing copy retained.

Exact SHA-256 of repo://src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs:
DB5971253D6BF7B397ADC90525AF6B1196E46745B70C825BECD287180659A997.
The full changed-source index is authoritative for the remaining files.

Forty distinct focused cases: SB05 component11; SB06 registry6, component4 and HTTP19.
No zero-test selection or skipped case supplies focused acceptance. Empty-scope red was
an actual failed behavior assertion, not a build failure. Collector transcripts validate
the ORIGINAL immutable TRX files and copied raw output; they are not new dotnet test runs.
The final registry run additionally checks searching the shortened UI ID and full GUID.
This one-line search predicate change invalidated registry search tests and the browser
search flow, both rerun on final image2. JWT lookup, issuance and authorization code did
not change; the original 19-case HTTP scope remains valid and live image2 admission was
also repeated. An extra HTTP rebuild attempt hit Windows file locks held by the broad
test run (bundle://proof/SB06/integration-rebuild-file-lock.txt); no test result is claimed
for that attempt and no running test was killed to release its binaries.

The same-token live Playwright path returned 200, cancel200, revoke401, delete401; the
test record was deleted through UI. A real source token was selected through the picker
and saved into the existing client's secret; Test/Discover then returned its full catalog.
Fresh 5214 has zero providers/sources/imports/secrets/tokens. Old DB and data are recoverable.

Broader Unit/Integration suites were also executed after conservative CodeAnalytics
fallback: Unit 6988 pass/1 fail, Integration 1121 pass/17 fail/1 opt-in skip.
Their unchanged pricing/seed/plugin and outdated shared-provider fixture failures are recorded separately; this
manifest does not claim the whole repository is green. No fake model fixtures were restored.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| ApiTokenRecord | repo://src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs; registry registration before return; bundle://proof/SB06/integration-tests.trx | repo://src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs and token dialog; bundle://proof/SB06/mcp-lifecycle-result.json | repo://src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs; create/reopen/revoke/delete in bundle://proof/SB06/unit-tests-final.trx | Missing/corrupt/deleted denies real HTTP; bundle://proof/SB06/integration-tests.trx; unauthorized UI service in bundle://proof/SB06/component-tests-final.trx |
