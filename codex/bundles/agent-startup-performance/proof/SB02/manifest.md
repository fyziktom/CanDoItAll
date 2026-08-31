# SB02 execution manifest

Status: Completed. Candidate implementation and focused proof passed; root accepted the later integrated UI/performance/host evidence. No production app, provider configuration or live database was changed by the SB02 source owner.

## Scope and identity

Baseline source: `3d5def561`. Six owned source/test/project files and ten unchanged context files are identified in `source-binary-hashes.json`; normalized baseline hashes are obtained from Git blobs, not retrospectively claimed baseline executable or worktree hashes. Candidate Unit and Integration copies of both affected production assemblies have identical hashes.

SDK 10.0.303, net10.0, xUnit 2/VSTest, Release. Working directory: repository root (exact original cwd retained in command metadata). Isolated build output: `.artifacts/agent-startup-performance/sb02-tests`. No sibling, public API, ProjectReference, DI registration, migration, startup pipeline or progress batching change.

Integration tests used only the owned disposable PostgreSQL server on loopback 52049, through the identity-checking `Enter-IsolatedPostgresTestEnvironment.ps1` bootstrap and unique leased databases. The bootstrap supplied process-only credentials without printing them. No default port 5432 or live application database was used. The duplicate-import fixture temporarily removed one uniqueness index in its own lease, cleaned its owned duplicate and invalid field in `finally`, recreated the captured index definition, and asserted exact definition equality before returning.

## Executed selections and results

The exact Unit selector is the OR of `FullyQualifiedName~` and these class names:

- `CanDoItAll.Tests.Unit.AgentFramework.ProviderRuntimeProfileSnapshotServiceTests`
- `CanDoItAll.Tests.Unit.SharedProviderRuntimeProfileMaterializerTests`
- `CanDoItAll.Tests.Unit.AgentFramework.ProviderCatalogProjectionFailureTests`
- `CanDoItAll.Tests.Unit.AgentFramework.AgentExecutionPreparationServiceTests`
- `CanDoItAll.Tests.Unit.AgentFramework.AgentProviderCredentialDispatchScopeTests`
- `CanDoItAll.Tests.Unit.ProviderManagementBoundaryTests`
- `CanDoItAll.Tests.Unit.SharedProviderArchitectureCharacterizationTests`

The exact Integration selector is the OR of `FullyQualifiedName~CanDoItAll.Tests.Integration.SharedProviders.SharedProviderRuntimeProjectionIntegrationTests` and `FullyQualifiedName~CanDoItAll.Tests.Integration.AgentFramework.ProviderInitializationIntegrationTests`.

| Phase | Discovered/executed | Result |
| --- | ---: | --- |
| Unchanged Unit baseline | 73/73 | 73 passed |
| Unchanged Integration baseline | 23/23 | 23 passed |
| Added Integration characterization before production edits | 34/34 | 34 passed |
| Tightened selected shared revision query assertion before optimization | 1/1 | Expected failure: expected 2 commands, actual 3 |
| Candidate Unit (three added cases) | 76/76 | 76 passed |
| Candidate Integration (twelve added cases) | 35/35 | 35 passed |

No passing phase had skipped/unexecuted cases. `results-summary.json` lists original test identities and outcomes; original TRX, logs and command metadata are in `transcripts/`. The failed-first query assertion used the exact FQN `CanDoItAll.Tests.Integration.SharedProviders.SharedProviderRuntimeProjectionIntegrationTests.Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries`.

A candidate integration discovery/build initially encountered another agent's in-progress SB03 fixture compile errors; its failed transcript is retained separately. That owner corrected the fixture before the successful 35-case discovery/build. One initial runner-discovery attempt accidentally captured bootstrap status output in the selector; the runner rejected zero tests and was corrected before recorded successful baseline discovery. The overwritten failed runner log is not claimed as preserved evidence. The first Unit baseline discovery was run directly and has its original log but no structured command metadata; subsequent commands have exact argument arrays, UTC bounds, exit code and cwd recorded.

## Reproduction recipe

For each selected project, build and discover first:

```text
dotnet test <project.csproj> --configuration Release --artifacts-path <isolated-output> --filter <exact-selector> --verbosity quiet --list-tests
```

Execute the identical selection from those outputs:

```text
dotnet test <project.csproj> --configuration Release --artifacts-path <isolated-output> --filter <exact-selector> --verbosity quiet --no-build --logger trx;LogFileName=selected.trx --results-directory <phase-results>
```

Integration commands first dot-source `.artifacts/agent-startup-performance/test-postgres/Enter-IsolatedPostgresTestEnvironment.ps1` in the same PowerShell process. `transcripts/*.command.json` supplies actual argument arrays, including selectors and paths. Discovery and execution reject zero or unexpected counts.

## Demonstrated effect and limits

The selected shared revision probe now issues two database commands, compared with the unchanged full-load oracle's three. Its server query contains a cardinality subquery; this is a round-trip reduction, not a claim of fewer SQL subqueries. Set-wide revision loading deliberately retains three database commands and typed EF rows so unrelated malformed source conversion errors still surface; it no longer builds effective profile/model/catalog copies just to obtain revisions.

The isolated allocation assertion measured eight operations on a 64-model publication: validation allocated 2,061,440 bytes; full materialization allocated 2,308,608 bytes. The difference was 247,168 bytes (30,896 per operation, about 10.7%). This compares validation with materialization only; it is not a startup latency benchmark or a measurement of the complete runtime mapper/database path. Both still perform canonical validation. No end-to-end speedup is claimed here.

## Invalidation and handoff

Changes to the six owned files, the ten hashed context files, EF provider/converters, canonical publication schema/hash behavior, revision composition, source/import/profile relationships, credential/policy ownership, test dependencies/platform or assembly collaboration invalidate affected proof. See `bundle://proof/SB02/semantic-invariants.md`, `bundle://proof/SB02/architecture-review.md`, and the compact before/after CodeAnalytics evidence. Root must accept the foundation proof and integrated host evidence before final closure.

## Governed proof bindings

The following changed-file SHA256 values bind the retained behavior proof to the current source. Detailed unchanged-context and binary identities remain in `bundle://proof/SB02/source-binary-hashes.json`.

| Changed file | SHA256 |
| --- | --- |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs | 51DDB2B68D86377B67F3472CA3549CDFBEA1C45D9EB2C497189E84C5C7AE708F |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs | B9C8D484CB5C31E125E97BDE2FD55A6A9BCC30176689E8742A96522081C0DCE6 |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderValidatedRuntimeShape.cs | FDA60F7F5C22D82C3BE5F532107F18D476C5F7EFBE1B9BE3CC025575B3B946C2 |
| repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj | 76CEF3BE42BF97761D4829EBCF32239944A8231B65C1466E063DE1927559F507 |
| repo://tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRuntimeProfileMaterializerTests.cs | 3AC121B9230FFAD305AAA6554D5548DCBF141E05C9D01F33B32479AB07FE8437 |
| repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderRuntimeProjectionIntegrationTests.cs | BAF74E5BE2195A4253FDFBB6462DD063B8C7EF37B6D06A9ED56F0B68D768A289 |

- Semantic invariant contract: bundle://proof/SB02/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB02/transcripts/query-failing-first.md
- Passing transcript: bundle://proof/SB02/transcripts/integration-passing.md
- Passing transcript: bundle://proof/SB02/transcripts/unit-passing.md
- Anti-stub audit transcript: bundle://proof/SB02/transcripts/anti-stub-audit.log

The normalized transcript presentations preserve original command metadata/UTC bounds/exit codes/test identities and explicitly distinguish the query regression failure from preservation assertions that already passed. They are retrospective evidence indexes, not fabricated reruns.

## Integrated closure

Root accepted bundle://proof/SB03/ui/validation-summary.md, bundle://proof/SB03/performance/independent-result-verification.json and bundle://proof/deployment/final-checkpoint.json. These establish actual both-host file/error tools, fresh follow-up, history, applicable approvals, measured startup improvement and preserved original files/source/configuration. The earlier focused handoff remains historical. Broad-test exceptions and exact final validator result remain explicit in bundle://reviews/01-execution-report.md; no all-green broad claim is made.

Final closure review: bundle://proof/SB03/ui/independent-file-acceptance-review.md. Canonical completed-stage validator: bundle://proof/closure-preparation/completed-validator.log and bundle://proof/closure-preparation/completed-validator.command.json; passed without changing validator or behavioral proof.
