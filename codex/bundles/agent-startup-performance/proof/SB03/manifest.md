# SB03 Execution Manifest

Status: Completed. Independent root/code-agent source/test-design reviews, candidate20unit and89unique integration cases passed with exact discovery/TRX reconciliation and no skipped cases. Later integrated UI/performance/host acceptance is linked below; the source-owner handoff remains historical.

## Accepted boundary

The immediate existing-run commit passes the ephemeral internal ExistingRunDetailCommitOrigin.Prepared value; recovery explicitly passes RecoveredJournal. This value is not serialized or inferred from journal contents. Four execution-slice payload methods share a private typed helper; chat-index projection retains its own complete rebuild and validation. Only a Prepared payload that differs from its freshly read typed target bypasses the second raw-text comparison and uses the existing atomic JSON write method.

Both journal validations, all fresh conflict reads, all collection validation/diffs, workspace-index behavior, stage callbacks, post-journal CancellationToken.None and the entire recovery route remain. Matching typed targets and all recovered journals retain WriteJsonIfChangedAsync, including formatting/unknown-property canonicalization. FileSandboxWorkspaceJsonStore and ProviderUsageObservation/history paths are unchanged.

The smaller number of physical reads has a CPU tradeoff: the Prepared branch performs an additional typed payload comparison/serialization. Read counts are mechanistic evidence, not a measured net-latency claim. The existing race against a noncooperating writer after validation remains: installing the exact target during that interval can cause one additional identical candidate atomic write where the old raw comparison might have skipped it. Logical stages and final canonical content remain equivalent; exact physical-write parity under this race is not claimed.

## Source and execution identity

- Baseline repository HEAD:3d5def561; the four relevant baseline persistence source hashes are in baseline/source-hashes.json.
- Candidate owner files: Storage/FileSandboxWorkspaceStore.cs, FileSandboxWorkspaceExecutionSliceStore.cs, FileSandboxWorkspaceChatProjectionStore.cs in CanDoItAll.AgentFramework.Persistence. No new project, partial, schema, public API, DI registration, global cache or sibling change.
- New tests: tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspacePreparedCommitReadIntegrationTests.cs; exact source hashes and production diff are in candidate/.
Local context only: - SDK10.0.303; xUnit2/VSTest/net10.0; Release; repository cwd C:/repositories/CanDoItAll. Isolated output tree .artifacts/agent-startup-performance/sb01-tests; no5032/watch output.
- PostgreSQL-dependent existing cases use only the owned loopback52049 test server, identity-checked Enter-IsolatedPostgresTestEnvironment.ps1 bootstrap, process-only connection setting and unique fixture database leases. Published artifacts omit connection secrets.

## Baseline and failing-first proof

- Existing projection unit selection:20/20passed.
- Existing storage integration selection:70discovered;69passed and one existing deadline case failed during concurrent SB02 build/test load. Its original error and TRX are retained without modification.
- The exact unchanged File_source_checkpoints_large_manifest_within_host_deadline_and_resumes_after_restart case subsequently passed alone after all agent builds/tests/CodeAnalytics work stopped. Its original two-second per-pass budget was not changed. Evidence: baseline/baseline-deadline-isolated-command.json, log and TRX. This is70unique existing cases with one documented retry, not71.
- New19cases were discovered and run before production persistence changes. Exactly the two append-progress read-count cases failed,17other cases passed. Each scale (one or32existing logs) observed one fresh deserialization and one redundant raw comparison for each of five supported paths. Evidence: characterization/unoptimized-new-tests.log/.trx.
- Candidate19/19passed with the same assertions (four Assert.Single calls were only changed to the equivalent predicate overload to eliminate new analyzer warnings). Each supported path now has one fresh deserialization and zero redundant raw comparisons in the ordinary Prepared append-progress interval.

## Candidate selections and proof

The new19-case class and existing six-class70-case storage union are separate selections, total89unique integration cases. Existing20projection unit cases are separate. Candidate existing storage execution is split into69cases excluding the exact deadline method, then that exact one-case selector during a coordinated quiet interval. Discovery identities must match actual TRX identities with no omitted or counted-twice cases. The combined startup/provider-failure Theory is separately owned and is not counted in89.

Original command logs and TRX are copied into baseline/, characterization/ and candidate/. Exact prospective argument arrays/cwd/start/end/exit metadata accompany each candidate command. Historical baseline metadata reconstruction is explicitly identified rather than inventing missing wall-clock values. Source and tested-binary hashes invalidate proof on change. Stable invariant IDs map to concrete methods/tests in semantic-invariants.md.

Actual5032/5214 Playwright MCP interaction, provider/tool behavior, before/after startup timing and final frozen-scope gates remain owned by the parent bundle.

## Completed handoff

- Candidate new19/19, existing69/69 plus isolated deadline1/1, projection unit20/20: all passed. Exact discovered and executed identities match baseline/current selections; no duplicated counting. See results-summary.json.
- The final deadline test ran alone after all agent test/build/CodeAnalytics/Docker-build work was held. It passed without changing its two-second per-pass budget; its original baseline failure remains visible.
- Candidate production/test hashes match the independently reviewed hashes. Persistence and Infrastructure dependency DLL hashes match across Unit and Integration outputs. No source changes followed those tests.
- All owned isolated test/build processes ended before the output tree was handed to the runtime agent for root-authorized Frozen Integration work. No live app was started, stopped or deployed by this owner.
- Exact command metadata is consolidated in command-metadata.json; candidate/read-count-evidence.json contains original TRX output for both Prepared scales and recovery before/after. Baseline and candidate sources/tested binaries are hashed. Root owns final subbundle/status closure and both-host real UI/performance acceptance.

## Governed artifact index

- Owned raw notes: N001/N003/N004; requirements R03/R04/R05/R06/R07/R08/R10.
- Status: Completed. Root accepted focused and integrated behavioral, performance and final host proof. Historical handoff/no-deployment notes above describe this source owner's earlier phase, not the current integrated state.
- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.json
- Changed source/test before-and-after hash inventory: bundle://proof/SB03/changed-source-hashes.json
- Representative production SHA256: EEC080B7ABFA8CB156DF1550EFA4BDD3D83E77438EF79002BD38B735A681780F
- Failing-first count proof: bundle://proof/SB03/transcripts/failing-first.txt
- Passing candidate proof: bundle://proof/SB03/transcripts/passing.txt
- Anti-stub audit: no matching production stub/fixture markers; bundle://proof/SB03/transcripts/anti-stub.txt
- Exact original command metadata: bundle://proof/SB03/command-metadata.json
- Source owner: repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs
- Downstream performance proof: bundle://proof/SB03/performance/independent-result-verification.json
- Controlled host proof: bundle://proof/deployment/runtime-replacement/independent-review.json
- Resolved and validated protected-file authorization record: bundle://proof/SB03/ui/file-transmission-approval-blocker.json
- Independent implementation review: bundle://proof/SB03/independent-implementation-review.md
- Final integrated evidence: bundle://proof/SB03/ui/validation-summary.md, bundle://proof/SB03/ui/independent-visual-review-file-validation.md and bundle://proof/deployment/final-checkpoint.json. Root accepted original asset/source preservation, real file/error tools, approval acceptance/rejection and history; broad-test exceptions remain in bundle://reviews/01-execution-report.md.

Transcript presentations retain actual commands, exit codes and immutable original TRX case outcomes. Editorial invariant mappings are explicitly distinguished from original test output. No tests were rerun or original artifacts rewritten to satisfy the schema.

Final closure review: bundle://proof/SB03/ui/independent-file-acceptance-review.md. Canonical completed-stage validator: bundle://proof/closure-preparation/completed-validator.log and bundle://proof/closure-preparation/completed-validator.command.json; passed without changing validator or behavioral proof.
