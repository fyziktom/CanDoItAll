# Retained execution transcript: query-failing-first

This retrospective presentation is assembled from the retained command metadata, original log and TRX. It is not a rerun and does not modify those artifacts.

Run label: integration-query-red
Command: dotnet ["test", "tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj", "--configuration", "Release", "--artifacts-path", "repo://.artifacts/agent-startup-performance/sb02-tests", "--filter", "FullyQualifiedName=CanDoItAll.Tests.Integration.SharedProviders.SharedProviderRuntimeProjectionIntegrationTests.Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries", "--verbosity", "quiet", "--no-build", "--logger", "trx;LogFileName=selected.trx", "--results-directory", "repo://.artifacts/agent-startup-performance/sb02-proof/integration-query-red-results"]
Working directory: repo://.
Original working directory and exact argument array: bundle://proof/SB02/transcripts/integration-query-red-execution.log.command.json
StartedUtc: 2026-08-31T14:05:30.7245857+00:00
CompletedUtc: 2026-08-31T14:05:44.1472551+00:00
ExitCode: 1
Invariant mapping: SB02-I09

## Original evidence identities

- bundle://proof/SB02/transcripts/integration-query-red-execution.log.command.json SHA256 4E17881600229938C973B7EB93493FCFB17F590CE00809BEDA93B01775F87C99
- bundle://proof/SB02/transcripts/integration-query-red-execution.log SHA256 522EA82FAEDD7A9113D6C11972757F51028228E71B241082B551A751CC754878
- bundle://proof/SB02/transcripts/integration-query-red.trx SHA256 367A80C6EA4474C41D3C783ADF31194A129CAE7E8F221EC7E822A9FCA6BAB73C

## Recorded test outcomes

Failed: CanDoItAll.Tests.Integration.SharedProviders.SharedProviderRuntimeProjectionIntegrationTests.Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries
Assert.Equal() Failure: Values differ
Expected: 2
Actual:   3

## Interpretation

The concrete selected revision query assertion failed against the old implementation: expected2 commands, actual3. The preserved integrity cases were not failing-first semantics tests; they characterized existing behavior before and after optimization.
