# SB01 Execution Manifest

Status: Completed. Focused source/test-design and integrated root reviews passed. The implementation-owner handoff below is historical; the final integrated bindings record the later deployment, UI, performance and preservation proof.

## Scope and build identity

- Source baseline: 3d5def561; baseline relevant SHA256 values: baseline/source-hashes.json.
- Owners changed: DurableFileWriter.cs, DurableFileWriterTests.cs. No policy/factory public contract, new project, sibling, journal or logging edit.
- .NET SDK10.0.303; net10.0/xUnit2/VSTest. Isolated outputs: .artifacts/agent-startup-performance/sb01-tests.
Local context only: - Local execution context: Windows repository working directory was C:/repositories/CanDoItAll.
- Linux proof uses the existing aspnet:10.0.10 runtime as non-root1654:1654, with no network or ports, read-only root/SDK/test binaries and isolated temporary/results mounts. Exact image, mount, runtime and runner arguments remain in bundle://proof/SB01/command-metadata.json and bundle://proof/SB01/transcripts/passing.txt.
- PostgreSQL-backed tests use the root-owned disposable loopback52049 server through the identity-checking Enter-IsolatedPostgresTestEnvironment.ps1 bootstrap and unique leased databases. No default5432/live database is touched.

## Selection and baseline

- Unit exact OR selector: FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.PhysicalFileSystemPathPolicyTests|FullyQualifiedName~CanDoItAll.Tests.Unit.Storage.DurableFileWriterTests.
- Integration exact OR selector: FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceStoreLockIntegrationTests|FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests.
- Unchanged discovery/execution:19unit Windows,19unit Linux,31integration Windows; all passed. Baseline discovery lists and TRX/logs are in baseline/.
- Eight new cases in the existing DurableFileWriterTests class yield27unit cases; exact discovery saved under characterization/.
- Before optimization,27new+existing cases ran:24passed, expected3performance failures. The depth cases observed D0=8, D6=20, D12=32 policy constructions; case-variant/exact paths still had equal construction counts before the optimization. These expected failures are preserved separately under characterization/.

## Command shape and exact execution metadata

The templates below show command structure, not literal executed commands. Exact argument arrays, working directory, exit codes and original TRX timestamps are recorded in command-metadata.json; historical metadata reconstruction is explicitly identified there. For each project, first build/discover:
    dotnet test <project> --configuration Release --artifacts-path .artifacts/agent-startup-performance/sb01-tests --list-tests --filter <exact selector>

Subsequent current-output builds add --no-restore. Execution uses the identical selector:
    dotnet test <project> --configuration Release --no-build --artifacts-path .artifacts/agent-startup-performance/sb01-tests --filter <exact selector> --logger trx;LogFileName=<phase>.trx --results-directory .artifacts/agent-startup-performance/sb01/results

Integration execution first dot-sources .artifacts/agent-startup-performance/test-postgres/Enter-IsolatedPostgresTestEnvironment.ps1 in the same PowerShell process. No connection secret is printed or copied into this proof.

Linux uses the same selected Unit cases through the VSTest runner and the isolated mounted outputs above. The exact executed runner switches and container-only paths are retained in bundle://proof/SB01/command-metadata.json and bundle://proof/SB01/transcripts/passing.txt; they are command context, not bundle artifacts.

## Invalidation and downstream unlock

Production/test hashes, factory/public contracts, path/case/creation/flush/coordination/callback behavior, SDK/package/dependency state and test platform invalidate affected proof. Baseline test executables were frozen before the writer change. New candidate proof must be reviewed before SB03 starts. Both-host UI/performance closure remains owned by the integrated bundle gate.
## Candidate result and foundation handoff

- Windows27/27 and Linux27/27 Unit cases passed; downstream Windows31/31 integration cases passed. Discovery identities/counts match the selection. See results-summary.json and candidate/.
- D0/D6/D12 are all8 policy constructions after the change, versus8/20/32 before. For actual insensitive Windows paths, exact descendants use8 and case-variant descendants retain12 in the depth2 exclusion case.
- Actual Linux root symlink replacement and same-path recreation assertions passed. Linux private-mode/symlink cases and an independent capability probe ran as uid1654. Windows case-variant path assertions ran affirmatively. The case-setting privilege limitation is explicitly assessed in semantic-invariants.md; no real Windows flag change is claimed.
- Stage assertions prove retained commit-stage sequence, not physical flush syscall counts. Existing payload flush code is unchanged.
- Source and Unit/Integration Infrastructure binary hashes are in candidate/source-binary-hashes.json. Both test-output Infrastructure DLL hashes must agree.
- Root independently reviewed the exact ordinal eligibility/security argument and new test design as Pass before this final31-case result. At that historical handoff, root acceptance and combined real5032/5214 UI/performance were still outstanding; the final integrated bindings below record their later acceptance.
Exact executed argument arrays, cwd, exit codes and original TRX timestamps: command-metadata.json. Stable invariant IDs with concrete test/artifact mappings: semantic-invariants.md.

## Governed artifact index

- Owned raw notes: N001/N004; requirements R01/R04/R08/R10.
- Status: Completed. Root accepted focused and integrated behavioral, performance and final host proof. Historical handoff/no-deployment notes above describe this source owner's earlier phase, not the current integrated state.
- Semantic invariant contract: bundle://proof/SB01/semantic-invariants.json
- Changed source/test before-and-after hash inventory: bundle://proof/SB01/changed-source-hashes.json
- Representative production SHA256: EA65BB573D0FE78A43793A4CB5947C701A1357EE026F0E68A39EA4E5F5E098A4
- Failing-first count proof: bundle://proof/SB01/transcripts/failing-first.txt
- Passing candidate proof: bundle://proof/SB01/transcripts/passing.txt
- Anti-stub audit: no matching production stub/fixture markers; bundle://proof/SB01/transcripts/anti-stub.txt
- Exact original command metadata: bundle://proof/SB01/command-metadata.json
- Source owner: repo://src/Foundation/CanDoItAll.Infrastructure/FileSystem/DurableFileWriter.cs
- Downstream performance proof: bundle://proof/SB03/performance/independent-result-verification.json
- Controlled host proof: bundle://proof/deployment/runtime-replacement/independent-review.json
- Resolved and validated protected-file authorization record: bundle://proof/SB03/ui/file-transmission-approval-blocker.json
- Independent implementation review: bundle://proof/SB01/independent-review.md
- Final integrated evidence: bundle://proof/SB03/ui/validation-summary.md, bundle://proof/SB03/ui/independent-visual-review-file-validation.md and bundle://proof/deployment/final-checkpoint.json. Root accepted original asset/source preservation, real file/error tools, approval acceptance/rejection and history; broad-test exceptions remain in bundle://reviews/01-execution-report.md.

Transcript presentations retain actual commands, exit codes and immutable original TRX case outcomes. Editorial invariant mappings are explicitly distinguished from original test output. No tests were rerun or original artifacts rewritten to satisfy the schema.

Final closure review: bundle://proof/SB03/ui/independent-file-acceptance-review.md. Canonical completed-stage validator: bundle://proof/closure-preparation/completed-validator.log and bundle://proof/closure-preparation/completed-validator.command.json; passed without changing validator or behavioral proof.
